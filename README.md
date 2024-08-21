# HashBack Authentication
A web authentication exchange where a caller proves their identity by publishing a hash value on their website.

This version of the document is a **public-draft** for review and discussion tagged as version **4.0**.
If you have any comments or notes, please open an issue on this project's public github.

This document is Copyright William Godfrey, 2024. You may use its contents under the terms of the Creative-Commons Attribution license.

## The elevator pitch.
(Alice calls Bob.)
- "Hi Bob. I'm Alice."
- "Prove it."
- "You know my number. Call me back."
               
(Bob calls Alice.)
- "Hi Alice. I'm Bob. Did you call me just now?"
- "That was me."

Did you notice what **didn't** happen? No-one needed a password, cryptographic tokens, or even recognizing each other's voice.

While a recipient of a call *can't* be certain who a caller is, the caller *can* be certain of who they are calling. By both parties calling each other, both can be reassured of each other's identity.

Now apply that thought to web authentication. The client can be sure (thanks to TLS) who the server is, but the server can't be sure who the client is, much like the analogy with phone calls. This document describes how the same "call me back" step could be used to authenticate a web API request.

## What is the problem this is meant to fix?

If you're running a service out in the cloud which interacts with an external service, you probably have cryptographic keys or a password or token squirreled away somewhere. This is probably encrypted or stored in a purpose built repository of secret keys and tokens. Either way, your code will need to unlock that material whenever it needs to interact with that external service.

This repository of secrets will need to be managed. The service won't be able to manage these things for itself because it'll need to identify itself to the service that issues these tokens, moving the problem one layer away without eliminating the problem itself. Either that or you make the decision that these secret tokens stay valid for long periods of time.

Repositories of secret tokens or keys. They have to be so secure that passers by can't access them, but so available that your code running in cloud can access them. 

## The Exchange
In a nutshell, a client proves their identity by publishing a short string on their TLS-secured website. The server downloads that string and thanks to TLS, is reassured that the client is indeed someone who is in control of that website.

To add a little more detail, the client builds a claim for authentication in the form of a JSON object. That object's bytes are themselves hashed and the hash result string is published on the client's website. To complete the loop, the server gets that string in its own separate HTTP/TLS transaction. Once the server can confirm that the hash published on the client's website matches its own calculated hash for the supplied JSON object's bytes, the server passes the request.

### "Isn't that like ACME?" (Let's Encrypt)

Yes, the exchange used by ACME has a lot in common with HashBack, especially the "call me back" verification step at its core. HashBack does have these significant differences:

*HashBack is a general purpose authentication mechanism*. You could use HashBack for any API that needs caller authentication. ACME is made for issuing TLS certificates and doesn't lend itself to other uses.

*HashBack is simpler*. You can complete the exchange with two transactions - one in each direction. It can do this because it relies on TLS having already been setup with mutually trusted CAs. ACME needs three request/response transactions to issue a certificate.

None of this is to denigrate ACME or Let's Encrypt. Indeed, this proposal is only possible because TLS has become ubiquitous and that's thanks to the Let's Encrypt project. We sitting on the shoulders of giants. 

I am very much open to the next version of this draft exchange reusing parts of ACME. Especially if we can keep it to two-transactions, or a security analysis reveals that we really do need that third transaction. 

### Ahead of time.
Before any of this can take place, the client's administrator (in their administrator role) will need to affirm to the remote server exactly what range of URLs the client has sole control over and wishes to use for HashBack authentication. Ideally, this would be a single fixed URL with a single query string parameter as only variation allowed. This URL must use TLS via the HTTPS scheme.

This exchange relies on the server having a clear mapping of which URLs belong to which clients, so it is important the range is not too broad.

### The Authorization header
The header is constructed as follows:
```
Authorization: HashBack (BASE64 encoded JSON) 
```

The BASE64 encoded block must be a single string with no spaces or end-of-line characters and must include the trailing `=` characters per the rules of BASE64. (The examples in this document split the string into multiple lines for clarity.) The bytes inside the BASE64 block are the UTF-8 representation of a JSON object with the properties listed below. All are required and the values are string type unless otherwise noted.

- `Version`
  - A string indicating the version of this exchange in use.
  - This version is indicated by the string `"BILLPG_DRAFT_4.0"`.
- `Host`
  - The full domain name of the server being called in this request.
  - Because load balancers and CDN systems might modify the `Host:` header, a copy is included here so there's no doubt exactly which string was used in the verification hash.
  - The recipient service must reject all requests that come with a name that belongs to someone else or generic names such as `localhost`, as this may be an attacker attempting to re-use a request that was made for a different server.
- `Now`
  - The current UTC time, expressed as an integer of the number of seconds since the start of 1970.
  - The recipient service should reject this request if timestamp is too far from its current time. This document does not specify a threshold in either direction but instead this is left to the service's configuration. (Finger in the air - ten seconds.)
  - The integer type should be greater than 32 bits to ensure this exchange will continue to work beyond the year 2038.
- `Unus`
  - 128 bits of cryptographic-quality randomness, encoded in BASE-64 including trailing `==`.
  - This is to make reversal of the verification hash practically impossible. The other JSON property values listed here are "predictable". The security of this exchange relies on this one value not being predictable.
  - The value must be unique for each request. Servers should reject any reused value within the allowed drift it places on the `Now` value.
  - I am English and I would prefer to not to name this property using a particular five letter word starting with N, as it has an unfortunate meaning in my culture.
- `Rounds`
  - An integer specifying the number of PBKDF2 rounds used to produce the verification hash. (See below.)
  - Must be a positive integer, at least 1.
- `Verify`
  - An `https://` URL belonging to the client where the verification hash may be retrieved with a GET request.
  - The URL must be one that server knows as belonging to a specific user. Exactly which URLs belong to which users is beyond the scope of this document.

If either or both of the two properties that include domain names (`Host` and `Verify`) uses IDN, those non-ASCII characters must be either UTF-8 encoded or use JSON's `\u` notation. Both properties must not use the `xn--` form.

For example:<!--1066_EXAMPLE_REQUEST-->
```
{
    "Version": "BILLPG_DRAFT_4.0",
    "Host": "server.example",
    "Now": 529297200,
    "Unus": "Rpgt4Fc5nMDq14LOps/hYQ==",
    "Rounds": 1,
    "Verify": "https://client.example/hashback?id=-925769"
}
```
This JSON string is BASE64 encoded and added to the end of the `Authorization:` header.<!--1066_EXAMPLE_AUTH_HEADER-->
```
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v
 dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiUm91bmRzIjox
 LCJWZXJpZnkiOiJodHRwczovL2NsaWVudC5leGFtcGxlL2hhc2hiYWNrP2lkPS05MjU3NjkifQ==
```

### Verification Hash Calculation and Publication

Once the client has built the request, it will need to find the JSON object's hash in order to publish it on their website. The server will also need to repeat this hashing process in order to verify the request is genuine.

The hashing process takes the following steps.
1. Call PBKDF2 with the following parameters:
   - Password: The bytes that the went into the BASE64 block.
   - Salt: The following 32 bytes.<!--FIXED_SALT-->
     - ```
       113,218,98,9,6,165,151,157,
       46,28,229,16,66,91,91,72,
       150,246,69,83,216,235,21,239,
       162,229,139,163,6,73,175,201
       ```
   - Hash Algorithm: SHA256
   - Rounds: The value specified in the JSON under `Rounds`.
   - Output: 256 bits / 32 bytes
2. Encode the hash result using BASE-64, including the trailing `=` character.

Note that the hash is performed on the same bytes that were encoded inside the BASE64 block. Because of this, the JSON itself may be flexible with formatting whitespace or JSON character encoding, as long as the JSON object is valid according to the requirements of JSON itself and the rules stated above.

The fixed salt is used to ensure that a valid hash is only meaningful in light of this document, as that salt is not sent over the wire with the request. For your convenience, here is the 32 byte fixed salt block in a variety of encodings:
- Base64: `cdpiCQall50uHOUQQltbSJb2RVPY6xXvouWLowZJr8k=`<!--FIXED_SALT_B64-->
- Hex: `71DA620906A5979D2E1CE510425B5B4896F64553D8EB15EFA2E58BA30649AFC9`<!--FIXED_SALT_HEX-->
- URL: `q%dab%09%06%a5%97%9d.%1c%e5%10B%5b%5bH%96%f6ES%d8%eb%15%ef%a2%e5%8b%a3%06I%af%c9`<!--FIXED_SALT_URL-->

Once the Caller has calculated the verification hash for itself, it then publishes the hash under the URL listed in the JSON with the type `text/plain`. The returned string itself must be one line with the BASE-64 encoded hash in ASCII as that only line. It must either have no end-of-line sequence, or end with either a single CR, LF, or CRLF end-of-line sequence.

The expected hash of the above example is: 
- `8UkPR3Vxjmj/xVe7inMT+O7ALKclnPILlt7puKQUGGI=`<!--1066_EXAMPLE_HASH-->

Once the service has downloaded that verification hash, it should compare it against the result of hashing the bytes inside the BASE64 block. If the two hashes match, the server may be reassured that the client is indeed the user identified by the URL from where the hash was downloaded and proceed to process the remainder of the request.

If there is any problem with the authentication process, including errors downloading the verification hash or that the supplied hash doesn't match the expected hash, the server must respond with an applicable error response. This should include sufficient detail to assist a reasonably experienced developer to fix the issue in question.

#### Generation of the fixed salt block
The salt string itself was generated by a PBKDF2 call with a high iteration count. For reference, the following parameters were used:
- Password: "To my Treacle." (14 bytes, summing to 1239.)<!--FIXED_SALT_PASSWORD-->
- Salt: "I love you to the moon and back." (32 bytes, summing to 2827.)<!--FIXED_SALT_DEDICATION-->
- Hash Algorithm: SHA512
- Iterations: 477708<!--FIXED_SALT_ITERATIONS-->
- Output: 256 bits / 32 bytes

## 401 responses and the WWW-Authenticate header
HTTP Authentication is typically triggered by the client first attempting to perform a particular transaction without any authentication, but for the response to reject that attempt with a `401` response and a `WWW-Authenticate` header that lists the many available authentication methods the client could use. (Or many such headers, each one listing an available method.)

For a server to respond when HashBack authentication is available, the `WWW-Authenticate` header must include an `<auth-scheme>` of `HashBack`. A `realm` parameter may be present but this is optional.

For example:
```
HTTP/1.1 401 Authentication Required
WWW-Authenticate: HashBack realm="My_Wonderful_Realm"
```

Clients may skip that initial transaction if it is already known that the server supports HashBack authentication.

## application/temporal-bearer-token+json
The above exchange does have the disadvantage of being expensive. While this may be acceptable for a once-off transaction, it would be prohibitively expensive to perform the full exchange for a large number of requests. 

This section describes an optional use of HashBack authentication that addresses this. An API that can be called once-off with a single HashBack transaction, that returns a temporal *Bearer token* that can be used until it expires. The use of an additional header, `Accept: application/temporal-bearer-token+json`, indicates the caller is requesting a token with metadata in this format.

For example: <!--BEARER_AUTH_HEADER-->
```
GET /api/tokens?startIn=1000&lifeSpan=3600 HTTP/1.1
Host: xn--tokensus-5fh.example
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJ0b2tlbnPRj3VzLmV4YW1wbGUi
 LCJOb3ciOjY4MjcxODUyMCwiVW51cyI6Ikt6SmsxTmcyRzBEWHZTb0V4RjJvV0E9PSIsIlJvdW5k
 cyI6MSwiVmVyaWZ5IjoiaHR0cHM6Ly90b2tlbnMtaS13YW50LmV4YW1wbGUvaGFzaGJhY2s/aWQ9
 ODIzNjE0MyJ9
Accept: application/temporal-bearer-token+json
```

The response body includes the requested Bearer token and optional metadata about that token. The JSON will have the following properties. Those expressing a time will be an integer time in Unix "1970" format. Only `BearerToken` is required to have a string (and not-null) value.

- `BearerToken`
  - This is the requested Bearer token. An opaque string of characters without (necessarily) any internal structure.
  - Because Bearer tokens are sent in ASCII-only HTTP headers, it must consist only of printable ASCII characters.
- `Id`
  - The ID of this issued token. If used, must have a string value. The value should be publishable without revealing or weakening the bearer token itself.
  - May be useful for auditing and to allow a token to be identified without revealing it.
- `IssuedAt`
  - The UTC time this token was issued.
- `NotBefore`
  - The UTC time that this token becomes (or became) valid.
  - This may be useful if a token is requested in advance to be used in the future. 
- `ExpiresAt`
  - The UTC time this Bearer token is due to expire.
- `DeleteUrl`
  - An optional string URL that may be `DELETE`'d to cause this bearer token to become invalid ahead of schedule.
  - If used, the DELETE operation must an `Authorization` header with this Bearer token.


For example:<!--BEARER_RESPONSE-->
```
Content-Type: application/temporal-bearer-token+json
{
    "Id": "3c14e547-6012-499f-8e32-8c501d3450fc",
    "BearerToken": "xygCgzNR.GKl0narP.DYKaXhKF.ZNk1go1M.jYNpHaD8.MhidPVnp",
    "NotBefore": 682719521,
    "IssuedAt": 682718521,
    "ExpiresAt": 682723121,
    "DeleteUrl": "https://tokens\u044Fus.example/tokens?id=3c14e547-6012-499f-8e32-8c501d3450fc"
}
```

If a service prefers to have clients go through HashBack to get a Bearer token, it may indicate this preference with a `WWW-Authenticate: Bearer` header and a `hashback` parameter. The parameter would be a URL for the client to send a GET request with an `Authorization: HashBack` header. This request could include an `Accept: application/temporal-bearer-token+json` or `Accept: application/jwt` (or both) depending on which format it prefers.

# An extended example.
**The Rutabaga Company** operates a website with an API designed for their customers to use. They publish a document for their customers that specifies how to use that API. One GET-able end-point is at `https://rutabaga.example/api/bearer_token` which returns a Bearer token in exchange for passing HashBack authentication. This end-points supports a number of query string parameters allowing the caller to request a particular desired life-span for the bearer token and if the request is for a token that will be used in a near future only.

**Carol** is a customer of the Rutabaga Company. She's recently signed up and logged into their customer portal. On her authentication page under the *HashBack Authentication* section, she's configured her account affirming that `https://carol.example/hashback/` is a folder under her sole control and where her verification hashes will be saved.

## Making the request.
Time passes and Carol needs to make a request to the Rutabaga Company API and needs a Bearer token. Her code builds a JSON object in memory:<!--CASE_STUDY_REQUEST-->
```
{
    "Version": "BILLPG_DRAFT_4.0",
    "Host": "rutabaga.example",
    "Now": 1111863600,
    "Unus": "sGhK1rIbEWjW6Sg25s+KPg==",
    "Rounds": 1,
    "Verify": "https://carol.example/api/hashback?ID=9c8091c9-bcd2-405a-8b23-9bf4c492f803"
}
```

The code calculates the verification hash from this JSON using the process outlined above. The result of hashing the above example request is:
- `Wh+1CucKXji7KZKjCFQ8GkiUbXrpRZrW/ATKZNwI3k4=`<!--CASE_STUDY_HASH-->

To complete the GET request, an `Authorization` header is constructed by encoding the JSON with BASE64. The complete request is as follows.<!--CASE_STUDY_AUTH_HEADER-->
```
GET /api/bearer-token?StartIn=1000&lifeSpan=3600 HTTP/1.1
Host: rutabaga.example
User-Agent: Carol's Magnificent Application Server.
Accept: application/temporal-bearer-token+json
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJydXRhYmFnYS5leGFtcGxlIiwi
 Tm93IjoxMTExODYzNjAwLCJVbnVzIjoic0doSzFySWJFV2pXNlNnMjVzK0tQZz09IiwiUm91bmRz
 IjoxLCJWZXJpZnkiOiJodHRwczovL2Nhcm9sLmV4YW1wbGUvYXBpL2hhc2hiYWNrP0lEPTljODA5
 MWM5LWJjZDItNDA1YS04YjIzLTliZjRjNDkyZjgwMyJ9
```

Because the hash needs only to be stored for a few seconds, The hash is recoded in the server's own memory cache. With this in place, the request for a Bearer token including the header can be sent to the API. The HTTP client library used to make the request will perform the necessary TLS handshake as part of making the connection.

## Checking the request
The Rutabaga Company website receives this request and validates the request body, performing the following checks:
- The request arrived via HTTPS.  :heavy_check_mark:
- The `Authorization` header is `HashBack` type with a BASE64-encoded JSON payload.  :heavy_check_mark:
- The `Host` value is a domain it owns - `rutabaga.example`.  :heavy_check_mark:
- The `Now` time-stamp is reasonably close to the server's internal clock.  :heavy_check_mark:
- The `Unus` value represents 128 bits encoded in base-64 and this value has never been seen before.  :heavy_check_mark:
- The `Rounds` value is within its acceptable 1-99 rounds.  :heavy_check_mark:
- The `Verify` value is an HTTPS URL belonging to a known user - *Carol*.  :heavy_check_mark:

The service has passed the request for basic validity, but it still doesn't know if the request has genuinely come from Carol's service or not. To perform this step, it proceeds to check the verification hash.

## Retrieval of the verification hash
Having the URL to get the client's verification hash, the Rutabaga Company's service performs a GET request for that URL. As part of the request, it makes the following checks:
- The URL lists a valid domain name.  :heavy_check_mark:
- The TLS handshake completes with a valid certificate.  :heavy_check_mark:
- The GET response code is 200.  :heavy_check_mark:
- The response's `Content-Type` is `text/plain`.  :heavy_check_mark:
- The text, once any CRLF bytes have been trimmed from the end, is 256 bits encoded in BASE-64.  :heavy_check_mark:

(If any of these tests had failed, the specific error would be indicated in a 400 error response to the initial request with the `Authorization` header. As the download was successful, that isn't needed.)

Having successfully retrieved a verification hash, it must now find the expected hash by itself hashing the bytes inside the BASE64 block inside the `Authorization` header.

## Checking the verification hash
The service performs the same PBKDF2 operation on the JSON request that the Caller performed earlier. With both the retrieved verification hash and the internally calculated expected hash, the service may compare the two strings. If they don't match, the service would make a 400 response to the original request complaining that the verification hash doesn't match the request body. In this case, they do indeed match and the service is reassured that the client is actually Carol.

Satisfied the request is genuine, the service generates a Bearer token and returns it to the caller as the response to the initial request, together with when it was issued and its expiry time.<!--CASE_STUDY_RESPONSE-->
```
HTTP/1.1 200 OK
Content-Type: application/temporal-bearer-token+json

{
    "Id": "13a862de-dc89-4f50-8709-0e7ed1cb6293",
    "BearerToken": "jTqkkDGt.IGu55JOH.cGlsgwiC.8Y2GZRQE.g4CR9icp.GB0XDinI",
    "NotBefore": 1111864601,
    "IssuedAt": 1111863601,
    "ExpiresAt": 1111868201,
    "DeleteUrl": "https://rutabaga.example/tokens?id=13a862de-dc89-4f50-8709-0e7ed1cb6293"
}
```

She may now use the issued Bearer token to call the Rutabaga API until that token expires. Additionally, the verification hash file can be deleted from the website if she so wishes.

## Answers to Anticipated Questions

### What's wrong with keeping a pre-shared secret long term?
They require management and secure storage. Your server-side code will need a way to access them without access to your master passwords or MFA codes. There are solutions for secure password storage that your unattended service code can use but they still need to be managed while this exchange utilises TLS (which both sides will have already made an investment in) to secure the exchange.

### I don't have a web server.
Then this exchange is not for you. It works by having two web servers make requests to each other.

### I have a web server on the other side of the Internet but not the same machine.
Your web site needs to be covered by TLS, and for your code to be able to publish a small static file to a folder on it. If you can be reasonably certain that no-one else can publish files on that folder, it'll be suitable for this exchange.

### What sort of range should be allowed for identifying a verification hash URL to a single user.
I recommend keeping it tight to either a file inside a single folder or to a single URL with a single query string parameter.

For example, if a user affirms they are in control of `https://example.com/hashback/`, then allow `https://example.com/hashback/1234.txt`, but reject any sub-folders or URLs with query strings. Similarly, if a user affirms they are in control of `https://example.com/hashback?ID=` then allow variations of URLs with that query string parameter changing, rejecting any requests with sub-folders or additional query string parameters.

Ultimately, it is up to the code performing this exchange to agree what URLs identify each user. This document does not proscribe that scope.

### TLS supports client-side certificates.
To use client-side certificates, the client side would need access to the private key. This would need secure storage for the key which the caller code has access to. Avoidance of this is the main motivation of this exchange.

### What if an attacker attempts to eavesdrop on either request?"
The attacker can't eavesdrop because TLS is securing the channel.

### What if either HTTP transaction uses a self-signed TLS certificate or one signed by an untrusted root?
If a connection to an untrusted TLS certificate is found, abandon the request and maybe log an error. Fortunately, this is default of most (all?) HTTP client libraries.

If you want to allow for self-signed TLS certificates, since this exchange relies on a pre-existing relationship, you could perhaps allow for "pinned" TLS certificates to be configured.

### What if an attacker has a TLS certificate signed by a trusted CA?
Then the attacker has broken TLS itself and we have bigger problems.

If this is a serious concern, then you could keep your own collection of trusted TLS certificates and refuse of recognize any TLS certificates not on your list. You'd effectively be running your own CA if you can't trust the ones built into your HTTP library.

### What if an attacker sends a fake Authorization header?
The recipient will attempt to retrieve a verification hash file from the real client's website. As there won't be a verification hash that matches the fake header, the attempt will fail.

### What if an attacker can predict the verification hash URL or has a verification hash intended for another server?"
Let them.

Suppose an attacker knows a current request's verification hash URL. They would be able to make that GET request and from that know the verification hash. Additionally, they could construct their own Authorization header to a genuine server, using the known `Verify` value with knowledge the genuine client's website will respond again to a second GET request with the same known verification hash.

To successfully perform this attack, the attacker will need to construct the JSON block such that its hash will match the verification hash, or else the server will reject the request. This will require finding the value of the `Unus` property which is unpredictable because it was generated from cryptographic-quality-randomness, sent over a TLS protected channel to the genuine server, and is never reused. 

For an attacker to exploit knowing a current verification hash, they would need to be able to reverse that hash back into the original JSON request, including the unpredictable `Unus` property. Reversing SHA256 (as part of PBKDF2) is considered practically impossible.

Nonetheless, it is trivial to make the verification hash URL unpredictable by using cryptographic-quality randomness and it may be considered prudent to do so. (Note to anyone performing a security analysis, please assume the URL *is* predictable and thus the verification hash may be exposed to attackers.)

### Does it matter if any part of the Authorization header is predictable?
Only the value of the `Unus` property needs to be unpredictable. All of the other values may be completely predictable to an attacker because only one unpredictable element is enough to make the verification hash secure.

### What if a client sends a legitimate Authorization header to a server, but that server is evil and it copies that request along to a different server?
The second server will reject the request because they will observe the `Host` property of the request is for the first server, not itself. For this reason it is important that servers reject all requests with a `Host` value other than the domain belonging to them, including "localhost" and similar.

### What if an attacker floods the POST request URL with many fake requests?
Any number of fake requests will all be rejected by the server because the real user is not publishing hashes that match these fake requests.

Despite this, the fact that a request for authentication will trigger a second GET request might be used as a denial-of-service attack. For this reason, it may be prudent for an server to track IP address blocks with a history of making bad authentication requests and rejecting subsequent requests that originate from these blocks, or even requiring that clients be at a pre-agreed range of IPs and rejecting anyone outside this range. (Note that I suggest this only as a means to prevent abuse. The security of the authentication method is not dependent on any IP block analysis.)

### What if there's a website that will host files from anyone?
Maybe don't claim that website as one that you have exclusive control over.

At its a core, you pass authentication by being someone who was able to demonstrate control of a particular URL. If the group of people who have that control is "anyone" than that's who can pass authentication.

### What if a malicious Caller supplies a verification URL that keeps the request open?
[I am grateful to "buzer" of Hacker News for asking this question.](https://news.ycombinator.com/item?id=38110536)

Suppose an attacker sets themselves up and configures their website to host verification hash files. However, instead of responding with verification hashes, this website keeps the GET request open and never closes it. As a result, the server is left holding two TCP connections open - the original authentication request and the GET request that won't end. If this happens many times it could cause a denial-of-service by the many opened connections being kept alive.

We're used to web services making calls to databases or file systems and waiting for those external systems to respond before responding to its own received request. The difference in this scenario is that the external system we're waiting for is controlled by someone else who may be hostile.

This can be mitigated by the server configuring a low timeout for the request that fetches the verification hash. The allowed time only needs to be long enough to perform the hash and the usual roundtrip overhead of a request. If the verification hash requests takes too long the overall transaction can be abandoned.

Nonetheless, I have a separate proposal that will allow for the POST request to use a 202 "Accepted" response where the underlying connection can be closed and reopened later. Instead of keeping the POST request open, the Issuer can close the request and the Caller may reopen it at a later time.

### Why does the PBKDF2 operation have a fixed salt?
The fixed hash only appears in this document and does not go over the wire as a request is made, so any hash produced which passes validation must have been calculated by someone reading this document. Any hashes produced will have no value outside of this documented exchange.

### Why use PBKDF2 at all?
PBKDF2 (which wraps SHA256) is used to allow for additional rounds of hashing to make an attack looking for a JSON string that hashes to a known verification hash much harder.

I don't think this is necessary (indeed, all of the examples in this document use `"Rounds":1`) because the `Unus` property is already 256 bits of unpredictable cryptographic quality randomness. For an attack exercising knowledge of a verification hash, looping through all possible `Unus` values, is already a colossally impractical exercise, even without additional rounds of PBKDF2. A previous draft of this proposal used a single round of SHA256, but I ultimately switched to PBKDF2 to allow for added rounds without needing a substantially updated new version of this protocol and for all implementations needing significant updates. For now, I'm going to continue using 1 as the default number of rounds. 

As this proposal is still in the public-draft phase, I am open to be persuaded that PBKDF2 is not needed and a single round of SHA256 is quite sufficient thank you very much. I'm also open to be persuaded that the default number of rounds needs to be significantly higher.

### Never mind PBKDF2, why use a hash at all? Why not make the request a URL and the string expected at that URL?
(I am grateful to m'colleague Rob Armitage for asking this question.)

It is necessary for the file retrieved from the client's website to be a hash (instead of a random string) to prevent an attack when a valid authorization request is fraudulently passed along to a third party. For example:

A-to-B: "I am server A. To prove it, I have placed "ABC" at https://A.example/hashback?id=123"
B-to-C: "I am server A. To prove it, I have placed "ABC" at https://A.example/hashback?id=123"
C-to-A: "GET https://A.example/hashback?id=123", to which A will return "ABC".
C-to-B: "You are successfully authorized."

From A's point of view, they made a valid request and the request resulted in a single expected GET to the verification URL, presumably from B. As far as A is concerned, there's nothing untoward going on at all. By requiring a hash of the authorization header and checking that the 'Host' property is correct, this passing-along attack is prevented.

### Why BASE64 the JSON in the `Authorization` header?
To ensure there's an unambiguous sequence of bytes to feed into the hash. By transferring the JSON block in an encoded set of bytes, the recipient can simply pass the decoded byte array into the PBKDF2 function as the password parameter.

### Shouldn't you have a server challenge like ACME?
This is something I'd like an expert to confirm, but I don't think we need one. The request is sent over TLS, which prevents an attacker seeing the request itself and also replaying it. The `Host` header prevents "passing along" attacks as described above.

If I am ever persuaded that a server challenge is needed, I'd make it a parameter to the `WWW-Authenticate: HashBack` header with the 401 response. The value of this parameter would then need to be included in the JSON that builds the `Authorization` header. The server would check this value is one it created and reject it if it isn't.

### I'm going to make many requests to the same server. Can I send the same Authorization header with each request?
Short version: No.
Slightly longer version: Please don't.

If this is your situation, my official answer is that the client should call the server to issue you a temporal bearer token. That initial request will cause the HashBack exchange to happen only once. Once that has finished, you'll have a bearer token (which is very cheap to use) until it expires. Then you can start over and request another one. My expectation is that almost all HashBack-backed requests will, in practice, actually be to request a new temporal bearer token. Indeed, for earlier drafts, requesting a temporal bearer token was the *only* thing you could do.

But let's discuss the implications of reusing HashBack Authorization headers. I do understand the motivation for wanting to do this. If you've already taken the effort to publish a verification hash, why not reuse it as much as possible instead of doing it again?

The security of this exchange relies on the `Unus` value being unpredictable. This means you should use your operating system's secure random number generator to make a new one each and every time you build a new JSON object. If there's any level of predictability of this value, an attacker might be able to predict a request you're about to make and the attacker makes it first. You're only making yourself less secure if you ever reuse an `Unus` value.

### The process of requesting a temporal bearer token takes too long.
Consider making that request for a token ahead of time on a schedule, just in case you might need one later. If later on, you do need to make a request and you need to make it now, you'll have that bearer token ready.

### What are the previous public drafts?
- [Public Draft 1](https://github.com/billpg/HashBack/blob/22c67ba14d1a2b38c2a8daf1551f065b077bfbb0/README.md)
  - Initial published revision, then named "Cross Request Token Exchange".
  - Used two POST requests in opposite directions, with the second POST request acting as the response to the first.
- [Public Draft 2](https://github.com/billpg/HashBack/blob/2165a661e093754e038620d3b2be1caeacb9eba0/README.md)
  - Updated to allow a 202 "Accepted" response to the first POST request, avoiding to need to keep the connection open.
  - I had a change of heart to this approach shortly after publishing it.
- [Public Draft 3.0](https://github.com/billpg/HashBack/blob/bf7e2ff1876e9673b04bffb7d70766a10d326976/README.md)
  - Substantial refactoring. The client makes a POST request with a JSON body and puts a hash of that JSON body on their website. The server fetches that hash and compares it to their own expected hash. The POST response is always a Bearer token.
  - Added a "dot zero" to allow for minor updates, reserving 4.0 for another substantial refactor.
  - Changed name to "HashBack" Authentication, as a play on "Call Back".
- [Public Draft 3.1](https://github.com/billpg/HashBack/blob/d8886ce0cebb159f6484186f5b6ccd750d0dd97c/README.md)
  - The "fixed salt" is now the result of running RBKDF2 but without processing the result into capitals letters. This means I no longer need to link to some "attached" C# code and can simply record the input parameters. (The original motivation of having only capital letters in the salt was to support implementations that only accept ASCII strings, but all implementations I could find will accept arbitrary blocks of bytes as input.)
  - Added "204SetCookie" as a third response type. Might be useful for a browser making the POST request.
- Public Draft 4.0 (This document)
  - The JSON request is now sent by the client in the form of an HTTP `Authorization` header. The transaction being authenticated could be anything, including a request for a Bearer token. This has the advantage of allowing a once-off request to skip the extra transaction to fetch a Bearer token and act more like traditional HTTP authentication. Also, as this header payload is BASE64 encoded, we don't need to canonicalize the JSON as the hash can be done on the BASE64 encoded bytes.

## Next Steps
This document is a draft version. I'm looking (please) for clever people to review it and give feedback. In particular I'd like some confirmation I'm using PBKDF2 with its fixed salt correctly. I know not to "roll your own crypto" and this is very much using pre-existing components. Almost all the security is done by TLS and the hash is there to confirm that authenticity of the authentication request. If you have any comments or notes, please raise an issue on this project's github.

In due course I plan to deploy a publicly accessible test API which you could use as the other side of the exchange. It'd perform both the role of an authenticating server by downloading your hashes and validating them, as well as perform the role of a client requesting authentication from you and publishing a verification hash for you to download. (And yes, you could point both APIs at each other, just for laughs.)

Ultimately, I hope to publish this as an RFC and establish it as a public standard.

My thanks to Danny Wilson for his feedback and for developing his own service that performs this authentication. Multiple independent implementations are good for establishing a new standard. My thanks also to Ollie Hayman for bringing ACME to my attention.

Thank you to my wife for her love and support while I developed this idea. I couldn't have done this without you.

Regards, Bill. <div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
