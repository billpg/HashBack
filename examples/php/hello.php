<?php
/**
 * Minimal PHP reference implementation of a HashBack "hello" endpoint.
 *
 * Given an Authorization: HashBack <base64> header, this verifies the caller's claim by
 * fetching the hash they've published at their own Verify URL and comparing it against
 * one computed locally from the claim - proving the caller controls that URL, without
 * either side ever exchanging a shared secret.
 *
 * This is deliberately minimal, for two reasons: it's meant to be read and adapted, and
 * it's stateless (no database, no session, no cache) so it can be dropped into any plain
 * PHP host. On purpose, it does NOT implement:
 *   - Unus replay tracking (needs persistent storage across requests to be meaningful -
 *     a real deployment wanting replay protection would need a database or cache for it)
 *   - caller-IP failure-count blocking, or any other rate limiting
 *   - cookies / a fast-path for repeat callers
 * The only restriction here beyond the core protocol is Step 6 below, which narrows
 * accepted Verify URLs to billpg's own demo service - see that step for why, and what to
 * change if you're adapting this for your own use.
 */

// ====================== Configuration - change these for your own use ======================

// This server's own identity, as it must appear in the "Host" field of an incoming claim.
// A claim built for a different Host must be rejected here, or a claim minted to
// authenticate to some *other* HashBack-speaking server could be replayed against this one.
$myHost = 'your-domain.example'; // <-- CHANGE THIS to this server's own domain.

// How much clock drift between this server and the caller is tolerated, in seconds.
$nowToleranceSeconds = 500;

// ====================== HashBack protocol constants - do not change these ======================

// Fixed, public salts defined by the HashBack specification itself for each protocol
// version. These are not secrets - they exist so a verification hash can't be trivially
// precomputed outside the protocol, not to hide anything. Byte-for-byte copies of the
// constants in billpg.HashBackCore/Helpers.cs.
$saltsByVersion = [
    'BILLPG_DRAFT_4.2' => pack('C*', 48,106,239,61,141,188,122,117,71,242,89,164,154,89,44,47,
                                      20,42,34,245,250,230,139,30,56,240,40,168,35,184,92,252),
    'BILLPG_DRAFT_4.1' => pack('C*', 113,218,98,9,6,165,151,157,46,28,229,16,66,91,91,72,
                                      150,246,69,83,216,235,21,239,162,229,139,163,6,73,175,201),
];

// ====================== Helper functions ======================

function fail($status, $message)
{
    http_response_code($status);
    header('Content-Type: text/plain');
    echo $message . "\n";
    exit;
}

/** PHP/Apache setups often strip the Authorization header by default. If this keeps
 * returning null even though the client is definitely sending one, add to your Apache
 * config (httpd.conf or .htaccess):
 *   SetEnvIfNoCase ^Authorization$ "(.+)" HTTP_AUTHORIZATION=$1
 * or, under nginx + PHP-FPM, make sure your fastcgi_param block passes it through:
 *   fastcgi_param HTTP_AUTHORIZATION $http_authorization;
 */
function get_authorization_header(): ?string
{
    if (function_exists('getallheaders')) {
        foreach (getallheaders() as $name => $value) {
            if (strcasecmp($name, 'Authorization') === 0)
                return $value;
        }
    }
    if (isset($_SERVER['HTTP_AUTHORIZATION']))
        return $_SERVER['HTTP_AUTHORIZATION'];
    if (isset($_SERVER['REDIRECT_HTTP_AUTHORIZATION']))
        return $_SERVER['REDIRECT_HTTP_AUTHORIZATION'];
    return null;
}

/** Tolerant base64 decode for a published hash: accepts either the standard alphabet or
 * the URL-safe (-/_) alphabet, with or without trailing "=" padding, since a hash
 * published by some other client's library may legitimately use either form. */
function decode_tolerant_base64(string $token): ?string
{
    $token = rtrim($token, '=');
    $token = strtr($token, '-_', '+/');
    $padded = $token . str_repeat('=', (4 - strlen($token) % 4) % 4);
    $decoded = base64_decode($padded, true);
    return $decoded === false ? null : $decoded;
}

// ====================== Step 1: Read the Authorization header ======================

$authHeader = get_authorization_header();
if ($authHeader === null)
    fail(401, 'Missing Authorization header.');

// The scheme prefix is "HashBack ", case-insensitive.
if (stripos($authHeader, 'HashBack ') === 0)
    $authHeader = trim(substr($authHeader, strlen('HashBack ')));
else
    $authHeader = trim($authHeader);

// ====================== Step 2: Base64-decode, keeping the RAW BYTES ======================

// Important: the verification hash in Step 7 is computed over these exact raw bytes, not
// a freshly re-serialized copy of the parsed claim below. If you decode into an array and
// then rebuild it with json_encode() before hashing, any difference in key order, number
// formatting, or whitespace versus the caller's original bytes will silently break every
// hash check. Keep $claimBytes untouched and use it - never a rebuilt JSON string - for
// hashing; use $claim (the decoded array) only for reading field values.
$claimBytes = base64_decode($authHeader, true);
if ($claimBytes === false)
    fail(400, 'Authorization value is not valid base64.');

$claim = json_decode($claimBytes, true);
if (!is_array($claim)
    || !isset($claim['Version'], $claim['Host'], $claim['Now'], $claim['Unus'], $claim['Verify']))
    fail(400, 'Authorization payload is not a valid HashBack claim.');

// ====================== Step 3: Check the protocol version, and pick the matching salt ======================

if (!isset($saltsByVersion[$claim['Version']]))
    fail(400, "Unrecognized HashBack version: {$claim['Version']}");
$salt = $saltsByVersion[$claim['Version']];

// ====================== Step 4: Check the Host field matches this server ======================

if (strcasecmp($claim['Host'], $myHost) !== 0)
    fail(400, "Host mismatch: this server is {$myHost}.");

// ====================== Step 5: Check the claim hasn't expired ======================

$claimAge = time() - (int)$claim['Now'];
if (abs($claimAge) > $nowToleranceSeconds)
    fail(400, 'Now is outside the accepted time window.');

// ====================== Step 6: Restrict which Verify URLs are acceptable ======================
//
// >>> THIS IS THE PART TO ADAPT FOR YOUR OWN USE. <<<
//
// The general HashBack model accepts any HTTPS Verify URL - that's the whole point, it's
// how a caller proves control of *their own* domain. This example instead only trusts
// hashes published under billpg's own demo service, so it can be pointed at
// https://demo.hashback.dev/call/ for testing without also having to stand up your own
// hash-hosting endpoint.
//
// If you're adapting this for a real deployment, you have two choices:
//   1. Drop this check entirely, accepting any HTTPS Verify URL - but then read up on the
//      SSRF precautions DemoService's own HttpGetter takes first (rejecting private/
//      loopback IP ranges, capping response size, timing out promptly), since you're now
//      fetching a URL an anonymous caller gave you.
//   2. Replace the prefix below with your own trusted verification-hosting domain, if you
//      only expect callers who publish hashes somewhere you control or otherwise trust.
$verifyUrlPrefix = 'https://demo.hashback.dev/hash/';
if (strpos($claim['Verify'], $verifyUrlPrefix) !== 0)
    fail(400, "Verify URL must start with {$verifyUrlPrefix}");

// ====================== Step 7: Compute the expected hash from the claim we received ======================

$expectedHash = base64_encode(hash('sha256', $salt . $claimBytes, true));

// ====================== Step 8: Fetch the Verify URL and read the published hash ======================

$context = stream_context_create(['http' => ['method' => 'GET', 'timeout' => 5]]);
$publishedBody = @file_get_contents($claim['Verify'], false, $context);
if ($publishedBody === false)
    fail(400, 'Could not fetch the Verify URL.');

// The published hash may be surrounded by other text - split on whitespace and take the
// first token that decodes to exactly 32 bytes (a SHA-256 digest), same as DemoService's
// own HelloController does.
$actualHash = null;
foreach (preg_split('/\s+/', trim($publishedBody)) as $token) {
    $decoded = decode_tolerant_base64($token);
    if ($decoded !== null && strlen($decoded) === 32) {
        $actualHash = base64_encode($decoded);
        break;
    }
}
if ($actualHash === null)
    fail(400, 'Verify URL did not return a usable base64-encoded 32-byte hash.');

// ====================== Step 9: Compare, and report the outcome ======================

// hash_equals() is a timing-safe comparison - appropriate here since we're comparing a
// value derived from a secret-ish computation, even though neither hash is itself secret.
if (!hash_equals($expectedHash, $actualHash))
    fail(401, 'Verification hash did not match.');

// Success - the caller has proven control of the Verify URL's own domain.
$callerDomain = parse_url($claim['Verify'], PHP_URL_HOST);
http_response_code(200);
header('Content-Type: text/plain');
echo "Hello {$callerDomain}!\n";
