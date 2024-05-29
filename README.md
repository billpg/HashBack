# HashBack Authentication
A web authentication exchange where a caller proves their identity by publishing a hash value on their website.

This version of the document is a **draft-under-development** for review and discussion and will be tagged as version **4.0**.
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

## The Exchange
In a nutshell, a client proves their identity by publishing a small text file on their TLS-secured website. The server downloads that file and thanks to TLS, is reassured that the client is indeed someone who is in control of that website.

To add a little more detail, the client builds a request header with a claim for authentication. That request header is itself hashed and the hash result is published on the client's website. To complete the loop, the server gets that text file in its own separate transaction. Once the server can confirm that the hash published on the client's website matches its own calculated hash for the request header, the server passes the request.

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
  - 256 bits of cryptographic-quality randomness, encoded in BASE-64 including trailing `=`.
  - This is to make reversal of the verification hash practically impossible.
  - The other JSON property values listed here are "predictable". The security of this exchange relies on this one value not being predictable.
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
    "Unus": "iZ5kWQaBRd3EaMtJpC4AS40JzfFgSepLpvPxMTAbt6w=",
    "Rounds": 1,
    "Verify": "https://client.example/hashback_files/my_json_hash.txt"
}
```
This JSON string is BASE64 encoded and added to the end of the `Authorization:` header.<!--1066_EXAMPLE_AUTH_HEADER-->
```
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v
 dyI6NTI5Mjk3MjAwLCJVbnVzIjoiaVo1a1dRYUJSZDNFYU10SnBDNEFTNDBKemZGZ1NlcExwdlB4
 TVRBYnQ2dz0iLCJSb3VuZHMiOjEsIlZlcmlmeSI6Imh0dHBzOi8vY2xpZW50LmV4YW1wbGUvaGFz
 aGJhY2tfZmlsZXMvbXlfanNvbl9oYXNoLnR4dCIsIvCfpZoiOiJodHRwczovL2JpbGxwZy5jb20v
 bmdneXUifQ==
```

### Verification Hash Calculation and Publication

Once the client has built the request, it will need to find the JSON object's hash in order to publish it on their website. The server will also need to repeat this hashing process in order to verify the request is genuine.

The hashing process takes the following steps.
1. Call PBKDF2 with the following parameters:
   - Password: The bytes that the went into the BASE64 block.
   - Salt: The following 32 bytes.<!--FIXED_SALT-->
     - ```
       [113,218,98,9,6,165,151,157,
       46,28,229,16,66,91,91,72,
       150,246,69,83,216,235,21,239,
       162,229,139,163,6,73,175,201]
       ```
   - Hash Algorithm: SHA256
   - Rounds: The value specified in the JSON under `Rounds`.
   - Output: 256 bits / 32 bytes
2. Encode the hash result using BASE-64, including the trailing `=` character.

Note that the hash is performed on the same bytes that were encoded inside the BASE64 block. Because of this, the JSON itself may be flexible with formatting whitespace or JSON character encoding, as long as the JSON object is valid according to the rules above. As the intended audience is another machine, no whitespace and UTF-8 encoding is preferable.

The fixed salt is used to ensure that a valid hash is only meaningful in light of this document, as that salt is not sent over the wire with the request. For your convenience, here is the 32 byte fixed salt block in a variety of encodings:
- Base64: `cdpiCQall50uHOUQQltbSJb2RVPY6xXvouWLowZJr8k=`<!--FIXED_SALT_B64-->
- Hex: `71DA620906A5979D2E1CE510425B5B4896F64553D8EB15EFA2E58BA30649AFC9`<!--FIXED_SALT_HEX-->
- URL: `q%dab%09%06%a5%97%9d.%1c%e5%10B%5b%5bH%96%f6ES%d8%eb%15%ef%a2%e5%8b%a3%06I%af%c9`<!--FIXED_SALT_URL-->

Once the Caller has calculated the verification hash for itself, it then publishes the hash under the URL listed in the JSON with the type `text/plain`. The text file itself must be one line with the BASE-64 encoded hash in ASCII as that only line. The file must either be exactly 44 bytes long with no end-of-line sequence, or end with either a single CR, LF, or CRLF end-of-line sequence.

The expected hash of the above example is: 
- `9Qe9cXJ7AAzfnByI7JnWC70l9W+KB7wFOZEjXHZ33kY=`<!--1066_EXAMPLE_HASH-->

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
HTTP Authentication is typically triggered by the client first attempting to perform a particular transaction without any authentication, but for the response to reject that attempt with a `401` response and a `WWW-Authenticate` header that lists the many available authentication methods the client could use.

For a server to respond when HashBack authentication is available, the `WWW-Authenticate` header must include an `<auth-scheme>` of `HashBack`. A `realm` parameter may be present but this is optional.

For example:
```
HTTP/1.1 401 Authentication Required
WWW-Authenticate: HashBack realm="My_Wonderful_Realm"
```

Clients may skip that initial transaction if it is already known that the server supports HashBack authentication.

## application/temporal-bearer-token+json
The above exchange does have the disadvantage of being expensive. While this may be acceptable for a once-off transaction, it would be prohibitively expensive to perform the full exchange for a large number of requests. 

This section describes an optional use of HashBack authentication that addresses this. An API that can be called once-off with a single HashBack transaction, that returns a temporal *Bearer token* that can be used until it expires. The use of an additional header, `Accept: application/temporal-bearer-token+json`, indicates the caller's request. 

For example: <!--BEARER_AUTH_HEADER-->
```
GET /api/bearer_token HTTP/1.1
Host: issuer.example
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJiZWFyZXItdG9rZW4taXNzdWVy
 LmV4YW1wbGUiLCJOb3ciOjY4MjcxODUyMCwiVW51cyI6IkpBTGlDeXpBanpVNkJDUHk3Q296dTdN
 UFZJZGlHUVlpN2trbnpCcXcydnc9IiwiUm91bmRzIjoxLCJWZXJpZnkiOiJodHRwczovL2NsaWVu
 dC5leGFtcGxlL2hiLzM3NjM1OC50eHQifQ==
Accept: application/temporal-bearer-token+json
```

The response body includes the requested Bearer token and when that token expires. The JSON will have the following properties.

- `BearerToken`
  - Required not-nullable string.
  - This is the requested Bearer token. An opaque string of characters without (necessarily) any internal structure.
  - Because Bearer tokens are sent in ASCII-only HTTP headers, it must consist only of printable ASCII characters.
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

For example:<!--BEARER_RESPONSE-->
```
Content-Type: application/temporal-bearer-token+json
{
    "BearerToken": "XCQr0XW1-J1wriEeU-Wqf15hS8-0MDhVode-bAg6RYdj-Swjyq1qR",
    "IssuedAt": 682718521,
    "ExpiresAt": 682722121
}
```

# An extended example.
**The Rutabaga Company** operates a website with an API designed for their customers to use. They publish a document for their customers that specifies how to use that API. One GET-able end-point is at `https://rutabaga.example/api/bearer_token` which returns a Bearer token in a JSON response if the request header includes a valid HashBack authentication header.

**Carol** is a customer of the Rutabaga Company. She's recently signed up and logged into their customer portal. On her authentication page under the *HashBack Authentication* section, she's configured her account affirming that `https://carol.example/hashback/` is a folder under her sole control and where her verification hashes will be saved.

## Making the request.
Time passes and Carol needs to make a request to the Rutabaga Company API and needs a Bearer token. Her code builds a JSON object in memory:<!--CASE_STUDY_REQUEST-->
```
{
    "Version": "BILLPG_DRAFT_4.0",
    "Host": "rutabaga.example",
    "Now": 1111863600,
    "Unus": "TmDFGekvQ+CRgANj9QPZQtBnF077gAc4AeRASFSDXo8=",
    "Rounds": 1,
    "Verify": "https://carol.example/hashback/64961859.txt"
}
```

The code calculates the verification hash from this JSON using the process outlined above. The result of hashing the above example request is:
- `1kL3PhDiiPLu+uUmVrz6GTJ5dpIRmvEOENem1dwx3yg=`<!--CASE_STUDY_HASH-->

To complete the GET request, an `Authorization` header is constructed by encoding the JSON with BASE64. The complete request is as follows.<!--CASE_STUDY_AUTH_HEADER-->
```
GET /api/bearer-token HTTP/1.1
Host: rutabaga.example
User-Agent: Carol's Magnificent Application Server.
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMCIsIkhvc3QiOiJydXRhYmFnYS5leGFtcGxlIiwi
 Tm93IjoxMTExODYzNjAwLCJVbnVzIjoiVG1ERkdla3ZRK0NSZ0FOajlRUFpRdEJuRjA3N2dBYzRB
 ZVJBU0ZTRFhvOD0iLCJSb3VuZHMiOjEsIlZlcmlmeSI6Imh0dHBzOi8vY2Fyb2wuZXhhbXBsZS9o
 YXNoYmFjay82NDk2MTg1OS50eHQifQ==
Accept: application/temporal-bearer-token+json

```

The hash is saved as a text file to her web server using the random filename selected earlier. With this in place, the request for a Bearer token including the header can be sent to the API. The HTTP client library used to make the request will perform the necessary TLS handshake as part of making the connection.

## Checking the request
The Rutabaga Company website receives this request and validates the request body, performing the following checks:
- The request arrived via HTTPS.  :heavy_check_mark:
- The `Authorization` header is `HashBack` type with a BASE64-encoded JSON payload.  :heavy_check_mark:
- The `Host` value is a domain it owns - `rutabaga.example`.  :heavy_check_mark:
- The `Now` time-stamp is reasonably close to the server's internal clock.  :heavy_check_mark:
- The `Unus` value represents 256 bits encoded in base-64.  :heavy_check_mark:
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
    "BearerToken": "M7HFwZjs-oaRmeCWn-x9P7p3OT-ab56YHh0-5q6qAiR0-u1jjMYc3",
    "IssuedAt": 1111863601,
    "ExpiresAt": 1111867201
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

If this is a serious concern, then you could keep your own collection of trusted TLS certificates and refuse of recognize any TLS ceritifcates not on your list. You'd effectively be running your own CA if you can't trust the ones built into your HTTP library.

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

### Why BASE64 the JSON in the `Authorization` header?
To ensure there's an unambiguous sequence of bytes to feed into the hash. By transferring the JSON block in an encoded set of bytes, the recipient can simply pass the decoded byte array into the PBKDF2 function as the password parameter.

I had considered requiring that the JSON be included in the header as unencoded ASCII, without any whitespace characters and IDN domains using JSON's `\u` notation. Can I, in practice, use an `Authorization` header with all of characters JSON uses? I am open to be persuaded that this would be preferable to using BASE64. 

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
  - Changed name to "HashBack" Authentication, reflecting that a token is only one possible outcome and the verification hash is the big idea.
- [Public Draft 3.1](https://github.com/billpg/HashBack/blob/d8886ce0cebb159f6484186f5b6ccd750d0dd97c/README.md)
  - The "fixed salt" is now the result of running RBKDF2 but without processing the result into capitals letters. This means I no longer need to link to some "attached" C# code and can simply record the input parameters. (The original motivation of having only capital letters in the salt was to support implementations that only accept ASCII strings, but all implementations I could find will accept arbitrary blocks of bytes as input.)
  - Added "204SetCookie" as a third response type. Might be useful for a browser making the POST request.
- Public Draft 4.0 (This document)
  - The JSON request is sent by the client in the form of an HTTP `Authorization` header. The transaction being authenticated could be anything, including a request for a Bearer token. This has the advantage of allowing a once-off request to skip the extra transaction to fetch a Bearer token and act more like traditional HTTP authentication. Also, as this header payload is BASE64 encoded, we don't need to canonicalize the JSON as the hash can be done on the BASE64 encoded bytes.
  - As I write this I'm uncertain if this is the best approach. I may yet return to the 3.1 draft and consider version 4 (along with version 2) as an abandoned idea.

## Next Steps
This document is a draft version. I'm looking (please) for clever people to review it and give feedback. In particular I'd like some confirmation I'm using PBKDF2 with its fixed salt correctly. I know not to "roll your own crypto" and this is very much using pre-existing components. Almost all the security is done by TLS and the hash is there to confirm that authenticity of the authentication request. If you have any comments or notes, please raise an issue on this project's github.

In due course I plan to deploy a publicly accessible test API which you could use as the other side of the exchange. It'd perform both the role of an authenticating server by downloading your hashes and validating them, as well as perform the role of a client requesting authentication from you and publishing a verification hash for you to download. (And yes, you could point both APIs at each other, just for laughs.)

Ultimately, I hope to publish this as an RFC and establish it as a public standard.

Regards, Bill. <div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
