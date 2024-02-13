# Cross Request Token Exchange
A web authentication exchange where a caller proves their identity by publishing a hash value on their website.

This version of the document is a **public draft** for review and discussion and when it ready will be tagged as `CRTE-PUBLIC-DRAFT-3`. If you have any comments or notes, please open an issue on this project's public github.

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

Now apply that thought to web authentication. The client can be sure (thanks to TLS) who the server is, but the server can't be sure who the client is, much like the analogy with phone calls. To perform a similar exchange for every single web request would be expensive, so this document specifies a one-off "call me back" exchange to supply the caller with a Bearer token, which the caller may use until it expires.

## What's a Bearer token?
(You may skip this section if you are already familiar with this concept.)

A Bearer token is string of characters. It could be a signed JWT or a string of randomness. If you know what that string is, you can include it any web request where you want to show who you are. The token itself is generated (or "issued") by the service that will accept that token later on as proof that you are who you say you are, because no-one else would have the token. 

```
POST /api/some/secure/api/
Authorization: Bearer eyIiOiIifQ.eyIiOiJodHRwczovL2JpbGxwZy5jb20vbmdneXUifQ.nggyu
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself. Cookies are a common variation of this method.

## The Exchange
- "Hey Bob. I want to use your API but I need a Bearer token."
- "Hey Alice. I've got this request for a Bearer token from someone claiming to be you."
- "Bob, that was me. Here's proof."
- "Thank you Alice. Here's your Bearer token."

There are two participants in this exchange:
- The **Caller** is requesting a Bearer token and publishes proof it made that request.
- The **Issuer** checks the proof and issues that Bearer token back to the Caller.

To request a Bearer token, the Caller will make a POST request to the Issuer requesting a Bearer token. Thanks to TLS, the Caller can be reassured they are talking to the genuine Issuer, but the Issuer doesn't yet know if the request came from the genuine Caller. To complete the loop, the JSON request will include a URL on the Caller's website which will contain a hash of the request JSON. Thanks again to TLS, the Issuer is reassured that the request came from the genuine Caller. The Issuer will now be able to respond to the initial POST request with an issued Bearer token.

(Note that the initial POST request is kept open while the Issuer service retrieves the verification hash from the Caller's website. I am working on a separate proposal utilizing the HTTP status code 202 to allow an open request to be closed and reopened later.)

### The POST Request Body
The request body is a single JSON object. All properties are of string type except `Now` and `Rounds` which are integers. (`Now` will need to be larger than 32 bits to continue working after 2038.) All properties are required and `null` is not an acceptable value for any of them. The object must not include other properties except these listed here.

- `CrossRequestTokenExchange`
  - Confirms this is a request for a Bearer token and indicates which version of this exchange the Caller wishes to use.
  - This version of this document is specified by the value "CRTE-PUBLIC-DRAFT-3". 
  - See the section describing 400 error responses below for version negotiation.
- `TypeOfResponse`
  - States what type of response the Caller is requesting. 
  - The responding service may use a 400 response to negotiate supported response types.
  - Values:
    - `BearerToken` - The Caller is requesting an opaque token.
    - `JWT` - The Caller is requesting a JWT token.
    - (Other values may be specified separately from this document.)
- `IssuerUrl`
  - A copy of the full POST request URL.
  - Because load balancers and CDN systems might modify the URL as POSTed to the service, a copy is included here so there's no doubt exactly which string was used in the verification hash.
  - The Issuer service must reject all requests that come with a URL that belongs to someone else, as this may be an attacker attempting to re-use a request that was made for a different Issuer.
- `Now`
  - The current UTC time, expressed as an integer of the number of seconds since the start of 1970.
  - The recipient service should reject this request if timestamp is too far from its current time. This document does not specify a threshold in either direction but instead this is left to the service's configuration. (Finger in the air - ten seconds.)
- `Unus`
  - 256 bits of cryptographic-quality randomness, encoded in BASE-64 including trailing `=`.
  - This is to make reversal of the verification hash practically impossible.
  - The other JSON property values listed here are "predictable". The security of this exchange relies on this one value not being predictable.
  - I am English and I would prefer to not to name this property using a particular five letter word starting with N, as it has an unfortunate meaning in my culture.
- `Rounds`
  - An integer specifying the number of PBKDF2 rounds used to produce the verification hash. 
  - Must be a positive integer, at least 1.
  - The Issuer service may request a different number of rounds if the value supplied by the Caller is too low or too high. See section describing 400 error responses below for negotiation of this number.
- `VerifyUrl`
  - An `https://` URL belonging to the Caller where the verification hash may be retrieved with a GET request.
  - The URL must be one that Issuer knows as belonging to a specific user.

For example:<!--1066_EXAMPLE_REQUEST-->
```
{
    "CrossRequestTokenExchange": "CRTE-PUBLIC-DRAFT-3",
    "TypeOfResponse": "BearerToken",
    "IssuerUrl": "https://issuer.example/api/generate_bearer_token",
    "Now": 529297200,
    "Unus": "iZ5kWQaBRd3EaMtJpC4AS40JzfFgSepLpvPxMTAbt6w=",
    "Rounds": 1,
    "VerifyUrl": "https://caller.example/crte_files/my_json_hash.txt"
}
```

### Verification Hash Calculation and Publication

Once the Caller has built the request JSON, it will need to find its hash in order to publish it on the Caller's website. The Issuer will also need to repeat this hashing process in order to verify the request is genuine.

The hash calculation takes the following steps.
1. Convert the JSON request body into its canonical representation of bytes per RFC 8785.
2. Call PBKDF2 with the following parameters:
   - Password: The JSON request body's canonical representation.
   - Salt: The following 64 ASCII bytes. (All are ASCII capital letter bytes.)
     - `LUGAXNWPDSFLHKCRBAJZQSGYWVDNBAECKFRMXTSUVHZKCEOQYGUDAVKXMICEQTGL`<!--FIXED_SALT-->
   - Hash Algorithm: SHA256
   - Rounds: The value specified in the JSON request under `Rounds`.
   - Output: 256 bits.
3. Encode the hash result using BASE-64, including the trailing `=` character.

(A simplified RFC 8785 generator could be used, thanks to all of the values being simple integers or strings and all the JSON property names beginning (by design) with a different capital letter.)

The fixed salt is used to ensure that a valid hash could only be calculated by reading this document. The salt string is not sent with the request so any hashes resulting are only meaningful in light of this document.

The salt string itself was generated by the attached C# program. This calls PBKDF2 but using its own source code as the password, together with a very high iteration count. After a little processing, the program outputs a string of 64 capital letters that was deterministically derived.

[Please see the source code used to generate the fixed salt which includes commentary for how it works.](https://github.com/billpg/CrossRequestTokenExchange/blob/c263795f3251bda7372bd9cadbe5c6dc08ed41b5/UpdateReadme/GenFixedSalt/GenFixedSalt.cs)

Once the Caller has calculated the verification hash for itself, it then publishes the hash under the URL listed in the JSON with the type `text/plain`. The text file itself must be one line with the BASE-64 encoded hash in ASCII as that only line. The file must either be exactly 44 bytes long with no end-of-line sequence, or end with either a single CR, LF, or CRLF end-of-line sequence.

The expected hash of the above example is: 
- `SenTKr29MXmn2Ja+zBy6dYtzLhoYg9rcb2VA0lT5IlQ=`<!--1066_EXAMPLE_HASH-->

### 200 "Success" Response
A 200 response indicates the Issuer service is satisfied the POST request came from the genuine Caller. The specific response will depend on the value of the `TypeOfResponse` request property.

#### '"TypeOfResponse": "BearerToken"'
The response body includes the requested Bearer token and when that token expires. The JSON will have the following properties.

- `BearerToken`
  - Required not-nullable string.
  - This is the requested Bearer token.
  - Because Bearer tokens are sent in ASCII-only HTTP headers, it must consist only of printable ASCII characters.
  - Note that while the examples in this document all use JWT, the token might use any format, including a string of random characters. The recipient should treat the token as an opaque string of characters unless agreed to separately from this document.
- `IssuedAt`
  - Optional or nullable integer.
  - The UTC time this token was issued, expressed as an integer of the number of seconds since the start of 1970.
  - If `null` or missing, the Issuer has chosen not to supply this information.
  - This value is supplied for documentation and auditing purposes only. The recipient is not expected to make decisions based on the value of this token.
- `ExpiresAt`
  - Optional or nullable integer.
  - The UTC expiry time of this Bearer token, expressed as an integer of the number of seconds since the start of 1970.
  - If `null` or missing, the Issuer is not declaring a particular expiry. The token will last until a request is made that actively rejects it.
  - A non-null value is advisory only. The issuer is neither guaranteeing the token will continue to work until it expires, nor that it will stop working once this expiry time has passed. 

For example:<!--1066_EXAMPLE_RESPONSE_SIMPLE_TOKEN-->
```
Content-Type: application/json
{
    "BearerToken": "6MxEeyaWbL4MBvRkyaELV7cVBS3gcb54aayAHPqs",
    "IssuedAt": 529297201,
    "ExpiresAt": 529300801
}
```

#### '"TypeOfResponse": "JWT"'
The response body will be a single JSON string value (including quotes) with the JWT token. The expiry and issued timestamps and other claims may be encoded inside the JWT according to that standard's rules.

For example<!--1066_EXAMPLE_RESPONSE_JWT_ONLY-->
```
Content-Type: application/json
"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsIiI6ImJpbGxwZy5jb20vbmdneXUifQ.eyJzdWIiOiJjYWxsZXIuZXhhbXBsZSIsImlzcyI6Imlzc3Vlci5leGFtcGxlIiwiaWF0Ijo1MjkyOTcyMDEsImV4cCI6NTI5MzAwODAxfQ.ZuzMUxY1_Zv8duc5kvj5LVTd2B4A9oj4m8qPCMGi7os"
```

### 400 "Bad Request" Response
As the recipient of this POST request, the Issuer will check the request is valid and conforms to its own requirements. If the request is unacceptable, the Issuer must respond with a 400 "Bad Request" response. The response body should include sufficient detail to assist an experienced developer to fix the problem. 

If the response body is JSON, some of the 400 response properties may have particular documented meanings that Caller software might detect and automatically cause a reattempt of the initial POST request. This mechanism allows negotiation of version and the number of PBKDF2 rounds using this method. (Note that any re-attempted request must use a fresh `Unus` value.)

Any problem with downloading or verifying the verification hash (referenced by the `VerifyUrl` property), should be reported back to the Caller in a 400 response. This includes on the successful download of the verification hash but when it doesn't match the expected hash.

Note that the Issuer service is not required by this document to include any of these properties, or even to return a JSON response body. If a JSON response does use any of these properties, they are required by this document to have these documented meanings.

#### 400 Response property `Message`.
If the response is JSON and the response body includes a property named "Message", the value will be a string of human-readable text describing the problem in a form suitable for logging. The intent of the presence of this property is that the remainder of the response need not be logged or may be deemed as only intended for machine use. (The Caller software is of course free to log the entire response if the developer wishes.)

For example:
```
400 Bad Request
{
    "Message": "The service at alice.example returned a 404 error."
}
```

#### 400 Response property `IncidentID`

If the response includes this property, the value will be a UUID that the service used to record the error. This may be used for an administrator to retrieve logging from the request in order to investigate issues without including information in the response that might constitute revealing privileged information to an unauthenticated agent.

If used, the value must be a valid UUID using ASCII hex digits (capitals or lower case) and hyphens in their standard location.

For example:
```
400 Bad Request
{
    "Message": "There was a problem downloading the verification hash.",
    "IncidentID": "db8e215c-a7ee-5104-a3cd-5fc715dec19f"
}
```

#### 400 Response property `AcceptVersions`
If the response includes this particular property, it is reporting that is doesn't know about that version of this exchange listed in the request. The property value will be a JSON array of version strings it does understand. If the requesting code understands many versions of this protocol, the Caller should make an initial request with its preferred version. If the recipient service doesn't know that version, the response should respond listing all the version is does know about. The Caller, if it knows versions other the one it initially selected, should finally select its most preferred version of the protocol it does know about from that list and start the initial request again.

For example:
```
POST https://bob.example/api/BearerRequest
{ "CrossRequestTokenExchange": "NEW-FUTURE-VERSION-THAT-YOU-DONT-KNOW-ABOUT", ... }

400 Bad Request
{ 
    "Message": "Unknown version. I only know CRTE-PUBLIC-DRAFT-3.",
    "AcceptVersions": [ "CRTE-PUBLIC-DRAFT-3" ]
}
```
#### 400 Response Property `AcceptTypeOfResponse`
The request property allows for a single string to specify the desired response. Two values are specified here (`BearerToken` and `JWT`) but later standards may allow for additional values without requiring a new edition of this document.

If the recipient does not know about the request response type, the response may list the response-type values it does know about using this response property. The value will be a JSON array of those strings.

For example:
```
POST https://bob.example/api/BearerRequest
{ ..., "TypeOfResponse": "NEW_AUTHENTICATION_TECHNOLOGY", ... }

400 Bad Request
{ 
    "Message": "We only know how to issue JWT tokens.",
    "AcceptTypeOfResponse": ["BearerToken", "JWT"]
}
```

#### 400 Response Property `AcceptRounds`
This response property indicates the service doesn't accept the number of PBKDF2 rounds the caller has selected. A too-high value would represent a significant load on hardware while a too-low value might need to be rejected as enabling an attacker.

The property value is a number of rounds the service would accept. If the Caller accepts the responded number of rounds, it may repeat the request with the new number of rounds and a corresponding updated verification hash with the requested number of rounds.

If a service has an acceptable range and the caller selected a value outside this range, the acceptable value returned could be the higher or lower thresholds, depending on which side of the acceptable range was requested.

For example:
```
POST https://bob.example/api/BearerRequest
{ ..., "Rounds": 1, ... }

400 Bad Request
{ 
    "Message": "We require the verification hash to have 99 to 999 rounds of PBKDF2.",
    "AcceptRounds": 99
}
```

### Other errors.
The service may respond with any applicable standard HTTP error code in the event of an unexpected error. 

# An extended example.
**SAAS** is a website with an API designed for their customers to use. When a customer wishes to use this API, their code must first go through this exchange to obtain a Bearer token. The service publishes a document for how their customers cam do this, including that the URL to POST requests to is `https://saas.example/api/login/crte`.

**Carol** is a customer of Saas. She's recently signed up and logged into the Saas customer portal. On her authentication page under the CRTE section, she's configured her account affirming that `https://carol.example/crte/` is a folder under her sole control and where her verification hashes will be saved.

## Making the request.
Time passes and Carol needs to make a request to the Saas API and needs a Bearer token. Her code builds a JSON request:<!--CASE_STUDY_REQUEST-->
```
{
    "CrossRequestTokenExchange": "CRTE-PUBLIC-DRAFT-3",
    "TypeOfResponse": "BearerToken",
    "IssuerUrl": "https://sass.example/api/login/crte",
    "Now": 1111863600,
    "Unus": "TmDFGekvQ+CRgANj9QPZQtBnF077gAc4AeRASFSDXo8=",
    "Rounds": 1,
    "VerifyUrl": "https://carol.example/crte/64961859.txt"
}
```

The code calculates the verification hash from this JSON using the process outlined above. The result of hashing the above example request is:
- `nbIMaOOUz+K9wlczDvGtKTeSWqbLWL9dfLuhDYCr3P0=`<!--CASE_STUDY_HASH-->

The hash is saved as a text file to her web server using the random filename selected earlier. With this in place, the POST request can be sent to the SAAS API. The HTTP client library used to make the POST request will perform the necessary TLS handshake as part of making the connection.

## Checking the request
The SAAS website receives this request and validates the request body, performing the following checks:
- The request arrived via HTTPS.  :heavy_check_mark:
- The version string `CRTE-PUBLIC-DRAFT-3` is known.  :heavy_check_mark:
- The `IssuerUrl` value is a URL belonging to itself - `saas.example`.  :heavy_check_mark:
- The `Now` time-stamp is reasonably close to the server's internal clock.  :heavy_check_mark:
- The `Unus` value represents 256 bits encoded in base-64.  :heavy_check_mark:
- The `Rounds` value is within its acceptable 1-99 rounds.  :heavy_check_mark:
- The `VerifyUrl` value is an HTTPS URL belonging to a known user - Carol.  :heavy_check_mark:

(If the version or `Rounds` property was unacceptable, a 400 response might be used to negotiate acceptable values. In this case, that won't be necessary.)

The service has passed the request for basic validity, but it still doesn't know if the request has genuinely come from Carol's service or not. To perform this step, it proceeds to check the verification hash.

## Retrieval of the verification hash
Having the URL to get the Caller's verification hash, the Issuer service performs a GET request for that URL. As part of the request, it makes the following checks:
- The URL lists a valid domain name.  :heavy_check_mark:
- The TLS handshake completes with a valid certificate.  :heavy_check_mark:
- The GET response code is 200.  :heavy_check_mark:
- The response's `Content-Type` is `text/plain`.  :heavy_check_mark:
- The text, once any CRLF bytes have been trimmed from the end, is 256 bits encoded in BASE-64.  :heavy_check_mark:

(If any of these tests had failed, the specific error would be indicated in a 400 error response to the initial POST request. As the download was successful, that isn't needed.)

Having successfully retrieved a verification hash, it must now find the expected hash from the original POST request body.

## Checking the verification hash
The Issuer service performs the same PBKDF2 operation on the JSON request that the Caller performed earlier. With both the retrieved verification hash and the internally calculated expected hash, the Issuer service may compare the two strings. If they don't match, the Issuer service would make a 400 response to the original POST request complaining that the verification hash doesn't match the request body. In this case, they do indeed match and the Issuer is reassured that the Caller is actually Carol.

Satisfied the request is genuine, the Saas service generates a Bearer token and returns it to the caller as the response to the POST request, together with when it was issued and its expiry time.<!--CASE_STUDY_RESPONSE-->

```
{
    "BearerToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsIiI6ImJpbGxwZy5jb20vbmdneXUifQ.eyJzdWIiOiJjYXJvbC5leGFtcGxlIiwiaXNzIjoic2Fzcy5leGFtcGxlIiwiaWF0IjoxMTExODYzNjAxLCJleHAiOjExMTE4NjcyMDF9.C7PshUwZG6C-6jeDe32vIvx2NCoiyB4CU_oKrMRvzDM",
    "IssuedAt": 1111863601,
    "ExpiresAt": 1111867201
}
```

She may now use the issued Bearer token to call the Saas API until that token expires. Additionally, the verification hash file can be deleted from the website if she so wishes.

## Answers to Anticipated Questions

### What's wrong with keeping a pre-shared secret long term?
They require management and secure storage. Your server-side code will need a way to access them without access to your master passwords or MFA codes. There are solutions for secure password storage that your unattended service code can use but they still need to be managed while this exchange utilises TLS (which both sides will have already made an investment in) to secure the exchange.

### I don't have a web server.
Then this exchange is not for you. It works by having two web servers make requests to each other.

### I have a web server on the other side of the Internet but not the same machine.
Your web site needs to be covered by TLS, and for your code to be able to publish a small static file to a folder on it. If you can be reasonably certain that no-one else can publish files on that folder, it'll be suitable for this exchange.

### What sort of range should be allowed for identifying an `IssuerUrl` to a single user.
I recommend keeping it tight to either a file inside a single folder or to a single URL with a single query string parameter.

For example, if a user affirms they are in control of `https://example.com/crte/`, then allow `https://example.com/crte/1234.txt`, but reject any sub-folders or URLs with query strings. Similarly, if a user affirms they are in control of 'https://example.com/crte?ID=' then allow variations of URLs with that query string parameter changing, rejecting any requests with sub-folders or additional query string parameters.

Ultimately, it is up to each pair of Caller and Issuer to agree what URLs identify that Caller. This document does not proscribe that scope.

### TLS supports client-side certificates.
To use client-side certificates, the client side would need access to the private key. This would need secure storage for the key which the caller code has access to. Avoidance of this is the main motivation of this exchange.

### How long should a bearer token last until expiry?
Up to you but (finger in the air) I'd go for an hour. If the exchange takes too long, remember you can do it in advance and have the Bearer token ready if needed.

### What if either HTTP transaction uses a self-signed TLS certificate or one signed by an untrusted root?
If a connection to an untrusted TLS certificate is found, abandon the request and maybe log an error. Fortunately, this is default of most (all?) HTTP client libraries.

If you want to allow for self-signed TLS certificates, since this exchange relies on a pre-existing relationship, you could perhaps allow for "pinned" TLS certificates to be configured.

### What if an attacker attempts to eavesdrop on the initial POST request?"
The attacker can't eavesdrop or spoof because TLS is securing the channel.

### What if an attacker can predict the verification hash URL?"
Let them.

Suppose an attacker knows a current request's verification hash URL. They would be able to make that GET request and from that know the verification hash. Additionally, they could construct their own request for a Bearer token to the genuine Issuer, using the known `VerifyUrl` value with knowledge the genuine Caller's website will respond again to a second GET request with the known verification hash.

To successfully perform this attack, the attacker will need to construct their request body such that its hash will match the verification hash, or else the Issuer service will reject the request. This will require finding the value of the `Unus` property which is unpredictable because it was generated from cryptographic-quality-randomness, sent over a TLS protected channel to the genuine Issuer, and is never reused. 

For an attacker to exploit knowing a current verification hash, they would need to be able to reverse that hash back into the original JSON request, including the unpredictable `Unus` property.  Reversing SHA256 (as part of PBKDF2) is considered practically impossible.

Nonetheless, it is trivial to make the verification hash URL unpredictable by using cryptographic-quality randomness and it may be considered prudent to do so. Any security analysis conducted on this exchange should assume the URL *is* predictable and thus the verification hash may be exposed to attackers.

### What if an attacker sends a fake POST request to an Issuer?
The Issuer will attempt to retrieve a verification hash file from the Caller's website. As there won't have a verification hash that matches the fake POST request, the attempt will fail.

### Does it matter if any part of the POST request is predictable?
Only the value of the `Unus` property needs to be unpredictable. All of the other values may be completely predictable to an attacker because only one unpredictable element is enough to make the verification hash secure.

### What if an attacker downloads a verification hash intended for a different issuer?
To exploit knowing a verification hash, an attacker would need to build a valid JSON request body that resolves to that hash. As the value of the `Unus` property is included in the hash but not revealed to an attacker, the task is practically impossible.

### What if a Caller sends a legitimate POST request to an Issuer, but that Issuer copies that request along to a different Issuer?
The second Issuer will reject the request because they will observe the `IssuerUrl` property of the request is for the first Issuer, not itself.

For this reason it is important that Issuer services reject any requests with a URL other than one belonging to them, including "localhost" and similar. Services should also avoid trusting the value of the "Host" header when comparing the value of `IssuerUrl` against the expected URL, as attacking callers might be able to spoof this header.

### What if an attacker floods the POST request URL with many fake requests?
Any number of fake requests will all be rejected by the Issuer because there will be no verification hash that matches the expected hash and the Issuer will not respond with a Bearer token without one.

Despite this, the fact that a POST request will trigger a second GET request might be used as a denial-of-service attack. For this reason, it may be prudent for an Issuer to track IP address blocks with a history of making bad POST requests and rejecting subsequent requests that originate from these blocks.

This exchange normally requires a pre-existing relationship between the participants, but it isn't unreasonable to suppose that open Issuer services exist that will take POST requests with any valid URL as the `VerifyUrl` property value. These should, to avoid being a participant in a denial-of-service attack, keep track of which `VerifyUrl` domains and IPs have a history of having any result other than returning a correct verification hash. A web site that isn't participating in this exchange might nonetheless have a public folder of text files that are exactly the right length for a verification hash, but only ones that match the expected hash will be willing participants.

It may also be prudent to keep the POST URL secret, so attackers can't send in a flood of fake requests if they don't know where to send them. As this would only be a method to mitigate a denial-of-service attack, the secret URL doesn't have to be treated as secret needing secure storage. The URL could be set in a unencrypted config file and if it does leak, be replaced without urgency at the participant's convenience. The security of the exchange relies on TLS and the verification hash, not the secrecy of the URLs.

### What if there's a website that will host files from anyone?
Maybe don't claim that website as one that you have exclusive control over.

At its a core, a Bearer token issued by this exchange is the result of someone who was able to demonstrate control of a particular URL. If the group of people who have that control is "anyone" than that's who the Bearer token is identifying you as.

### What if a malicious Caller supplies a verification URL that keeps the request open?
[I am grateful to "buzer" of Hacker News for asking this question.](https://news.ycombinator.com/item?id=38110536)

Suppose an attacker sets themselves up and configures their website to host verification hash files. However, instead of responding with verification hashes, this website keeps the GET request open and never closes it. As a result, the Issuer server is left holding two TCP connections open - the original POST request and the GET request that won't end. If this happens many times it could cause a denial-of-service by the many opened connections being kept alive.

We're used to web services making calls to databases or file systems and waiting for those external systems to respond before responding to its own received request. The difference in this scenario is that the external system we're waiting for is controlled by someone else who may be hostile.

This can be mitigated by the Issuer configuring a low timeout for the request that fetches the verification hash. The allowed time only needs to be long enough to perform the hash and the usual roundtrip overhead of a request. If the verification hash requests takes too long the overall transaction can be abandoned.

Nonetheless, I have a separate proposal that will allow for the POST request to use a 202 "Accepted" response where the underlying connection can be closed and reopened later. Instead of keeping the POST request open, the Issuer can close the request and the Caller may reopen it at a later time.

### Why does the PBKDF2 operation have a fixed salt?
The fixed hash only appears in this document and does not go over the wire as a request is made, so any hash produced which passes validation must have been calculated by someone reading this document. Any hashes produced will have no value outside of this documented exchange.

### Why use PBKDF2 at all?
PBKDF2 (which wraps SHA256) is used to allow for additional rounds of hashing to make an attack looking for a JSON string that hashes to a known verification hash much harder.

I don't think this is necessary (indeed, most of the examples in this document use `"Rounds":1`) because the `Unus` property is already 256 bits of unpredictable cryptographic quality randomness. For an attack exercising knowledge of a verification hash, looping through all possible `Unus` values, is already a colossally impractical exercise, even without additional rounds of PBKDF2. A previous draft of this proposal used a single round of SHA256, but I ultimately switched to PBKDF2 to allow for added rounds without needing a substantially updated new version of this protocol and for all implementations needing significant updates. For now, I'm going to continue using 1 as the default number of rounds. 

As this proposal is still in the public-draft phase, I am open to be persuaded that PBKDF2 is not needed and a single round of SHA256 is quite sufficient thank you very much. I'm also open to be persuaded that the default number of rounds needs to be significantly higher.

### What are the previous public drafts?

- [Public Draft 1](https://github.com/billpg/CrossRequestTokenExchange/blob/22c67ba14d1a2b38c2a8daf1551f065b077bfbb0/README.md)
  - Used two POST requests in opposite directions, with the second POST request acting as the response to the first.
- [Public Draft 2](https://github.com/billpg/CrossRequestTokenExchange/blob/2165a661e093754e038620d3b2be1caeacb9eba0/README.md)
  - Updated to allow a 202 "Accepted" response to the first POST request, avoiding to need to keep the connection open.
  - I had a change of heart to this approach shortly after publishing it.
- Public Draft 3 (This document)
  - Substantial refactoring after realising the verification hash could be a unauthenticated GET request on a static file host.
  - Removed 202 responses after noting the inner GET request could have a very short timeout and deciding it could work as an independent proposal for any POST request.

## Next Steps

This document is a draft version. I'm looking (please) for clever people to review it and give feedback. In particular I'd like some confirmation I'm using PBKDF2 with its fixed hash correctly. I know not to "roll your own crypto" and this is very much using pre-existing components. Almost all the security is done by TLS and the hash is there to confirm that authenticity of the POST request. If you have any comments or notes, please raise an issue on this project's github.

In due course I plan to deploy a publicly accessible test API which you could use as the other side of the exchange. It'd perform the role of an Issuer by sending your API tokens on demand, as well as perform the role of a Caller by asking your API for a token.

Ultimately, I hope to publish this as an RFC and establish it as a public standard.

Regards, Bill. <div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
