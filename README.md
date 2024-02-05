# Cross Request Token Exchange
An authentication exchange between two web services.

This version of the document is a **public draft** for review and discussion and when it ready will be tagged as `CRTE-PUBLIC-DRAFT-3`. If you have any comments or notes, please open an issue on this project's public github.

This document is Copyright William Godfrey, 2024. You may use its contents under the terms of the Creative-Commons Attribution license.

## The elevator pitch.

- "Hey Bob. I want to use your API but I need a Bearer token."
- "Hey Alice. I've got this request for a Bearer token from someone claiming to be you."
- "Bob, that was me. Here's proof."
- "Thank you Alice. Here's your Bearer token."

Did you notice what **didn't** happen?

No-one needed a pre-shared key nor a shared secret. No-one needed a place to securely store them. The two web servers already have TLS set up and this is what enables the exchange to work.

When you make an HTTPS request, thanks to TLS you can be sure who you are connecting to, but the service receiving the request can't be sure who the request is coming from. By using **two separate** HTTPS requests, the two web servers may perform a brief handshake and exchange a *Bearer* token.

## What's a Bearer token?

A Bearer token is string of characters. It could be a signed JWT or a string of randomness. If you know what that string is, you can include it any web request where you want to show who you are. The token itself is generated (or "issued") by the service that will accept that token later on as proof that you are who you say you are, because no-one else would have the token. 

```
POST /api/some/secure/api/
Authorization: Bearer eyIiOiIifQ.eyJuZ2d5dSI6Imh0dHBzOi8vYmlsbHBnLmNvbS9uZ2d5dSJ9.nggyu
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself. Cookies are a common variation of this.

The exchange in this document describes a mechanism for a server to request a Bearer token from another server in a secure manner.

## The Exchange

There are two participants in this exchange:
- The **Caller** is requesting a Bearer token.
- The **Issuer** issues that Bearer token back to the Caller.

To request a Bearer token from the Issuer, the Caller will first need to prepare a JSON object for the request body. The Caller will then hash this JSON object according to the rules documented below. Once calculated, that hash value will need to be made available for retrieval by the Issuer by publishing it on a website under a URL that's known to the Issuer as belonging to the Caller. The URL of this hash is included in the JSON request body.

For example. An Issuer might know that a particular Caller is the only user capable of publishing files in the folder `https://caller.example/crte_files/` and will use this knowledge as reassurance the file must have come from the genuine Caller.

Once that JSON and the published hash is ready, the Caller will open a POST request to the Issuer using the URL agreed in advance for this purpose, together with the JSON request body written earlier. While handling this POST request, the Issuer will retrieve the expected hash published by the Caller earlier. By repeating the hash calculation on the JSON request body and comparing it against the downloaded expected hash, the Issuer can confirm if the request came from the known Caller or not.

During this time when the Issuer service retrieves the expected hash from the Caller's own web service, the POST request will be kept open, responding only when the exchange has completed.

(I am designing a separate mechanism utilizing the 202 HTTP response that will allow for the request to be closed and reopened later, which I will document separately.)

Once the Issuer is satisfied the POST request came from the genuine Caller, it will create a new Bearer token for that user and include it in the POST response, alongside applicable metadata.

If the Issuer cannot confirm the POST request came from the genuine Caller or an error occurred during processing, the Issuer must instead respond with an applicable error. Some error responses are documented below.

### The POST Request Body JSON Object
The request body is a single JSON object. All properties are of string type except `Now` and `Rounds` which are integers. All properties are required and `null` is not an acceptable value for any. The object must not include other properties except these.

- `CrossRequestTokenExchange`
  - This indicates which version of this exchange the Caller wishes to use.
  - This version of this document is specified by the value "CRTE-PUBLIC-DRAFT-3". 
  - See the section describing 400 error responses below for version negotiation.
- `IssuerUrl`
  - A copy of the full POST request URL.
  - Because load balancers and CDN systems might modify the URL as POSTed to the service, a copy is included here so there's no doubt exactly which string was used in the verification hash.
  - The Issuer service must reject a request with the wrong URL as this may be an attacker attempting to re-use a request that was made for a different Issuer.
- `Now`
  - The current UTC time, expressed as an integer of the number of seconds since the start of 1970.
  - The recipient service should reject this request if timestamp is too far from its current time. This document does not specify a threshold but instead this is left to the service's configuration. (Finger in the air - ten seconds.)
- `Unus`
  - At least 256 bits of cryptographic-quality randomness, encoded in BASE-64 including trailing `=`.
  - This is to make reversal of the verification hash practically impossible.
  - The other JSON property values listed here are "predictable". The security of this exchange relies on this one value not being predictable.
  - I am English and I would prefer to not to name this property using a particular five letter word starting with N, as it has an unfortunate meaning in my culture.
- 'Rounds'
  - An integer specifying the number of PBKDF2 rounds used to produce the verification hash. 
  - Must be a positive integer, at least 1.
  - The Issuer service may request a different number of rounds if this value is too low or too high. See section describing 400 error responses below for negotiation of this number.
- `VerifyUrl`
  - An `https://` URL belonging to the Caller where the verification hash may be retrieved with a GET request.
  - The URL must be one that Issuer knows as belonging to a specific Caller user.

For example:<!--1066_EXAMPLE_REQUEST-->
```
{
    "CrossRequestTokenExchange": "CRTE-PUBLIC-DRAFT-3",
    "IssuerUrl": "https://issuer.example/api/generate_bearer_token",
    "Now": 529297200,
    "Unus": "iZ5kWQaBRd3EaMtJpC4AS40JzfFgSepLpvPxMTAbt6w=",
    "Rounds": 1,
    "VerifyUrl": "https://caller.example/crte_files/C4C61859.txt"
}
```

### Hash Calculation and Publication

Once the Caller has built the request JSON, it will need to find its hash in a particular way, which the issuer will need to repeat in order to verify the request is genuine.

The hash calculation takes the following steps.
1. Convert the JSON request into its canonical representation of bytes per RFC 8785.
2. Call PBKDF2 with the following parameters:
   - Password: The JSON request's canonical representation.
   - Salt: The following 64 ASCII bytes. (All ASCII capital letters.)
     - `EAHMPQJRZDKGNVOFSIBJCZGUQAFWKDBYEGHJRUZMKFYTQPOHADJBFEXTUWLYSZNC`<!--FIXED_SALT-->
   - Hash Algorithm: SHA256
   - Rounds: The value specified in the JSON request under `Rounds`.
   - Output: 256 bits.
3. Encode the hash result using BASE-64, including the trailing `=` character.

(As all of the values are either integers or strings and all the JSON property names begin (by design) with a different capital letter, a simplified RFC 8785 generator could be used without needing to implement the full specification.)

The fixed salt is used to ensure that a valid hash could only be calculated by reading this document. The salt string is not sent with the request so any hashes resulting could only be used for this exchange. [Generator code with commentary for fixed salt string.](https://github.com/billpg/CrossRequestTokenExchange/blob/898149500b36107f8943ac7024fc73772adfa9c0/GenFixedSalt/GenFixedSalt.cs)

The Caller must then publish this verification hash under the URL listed in the JSON with the type `text/plain`. The file itself must be one line with the BASE-64 encoded hash in ASCII as that only line. The file may end with CR, LF or CRLF bytes, or with no end-of-line byte sequence at all.

The expected hash of the above example is: 
- `BKG55yKpOGkdyMVVo5BgNpH64DiuIQ5KmhlLAeXYC7Y=`<!--1066_EXAMPLE_HASH-->

### 200 "Success" Response
A 200 response will include the requested Bearer token and other useful details in a JSON response body. The JSON will have the following  properties, both required.

- `BearerToken`
  - This is the requested Bearer token. It must consist only of printable ASCII characters.
- `ExpiresAt`
  - The UTC expiry time of this Bearer token, expressed as the number of seconds since the start of 1970.

For example:<!--1066_EXAMPLE_RESPONSE-->
```
Content-Type: application/json
{
    "BearerToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsIiI6ImJpbGxwZy5jb20vbmdneXUifQ.eyJzdWIiOiJjYWxsZXIuZXhhbXBsZSIsImlzcyI6Imlzc3Vlci5leGFtcGxlIiwiaWF0Ijo1MjkzMDA4MDAsImV4cCI6NTI5MzgzNjAwfQ.l9_tKGUcZxJJgSLOA_uvHKDCSocDMQX2yUC5mDzu7R0",
    "ExpiresAt": 529383600
}
```

### 400 "Bad Request" Response

As the recipient of this POST request, the Issuer will check the request is valid and conforms to its own requirements. If the request is unacceptable, the Issuer must respond with a 400 "Bad Request" response. The response body should include suffcient detail to assist an experienced developer to fix the problem. 

If the response body is JSON, some of the 400 response properties have particular documented meanings that Caller software might detect and automatically cause a reattempt of the initial POST request. This mechanism allows negotiation of version and the number of PBKDF2 rounds. (Note that any re-attempted request must use a fresh `Unus` value.)

Any problem with downloading or verifying the verification hash (referenced by the `VerifyUrl` property), should be reported back to the Caller in a 400 response. This includes on the successful download of the verification hash but it doesn't match the expected hash.

#### 400 Response property `Message`.
If the response is JSON and the response body includes a property named "Message", the value will be a string of human-readable text describing the problem in a form suitable for logging. The intent of the presence of this property is that the remainder of the response need not be logged or may be deemed as only intended for machine use. (The Caller software is of course free to log the entire response if the developer wishes.)

For example:
```
400 Bad Request
{
    "Message": "Your service at alice.example returned a 404 error."
}
```

#### 400 Response property `AcceptVersions`
If the response includes this particular property, it is reporting that is doesn't know about that version of this exchange listed in the request. The property value will be a JSON array of version strings it does understand. If the requesting code understands many versions of this protocol, the Caller should make an initial request with its preferred version. If the recipient service doesn't know that version, the response should respond listing all the version is does know about. The Caller should finally select its most preferred version of the protocol it does know about from that list and start the initial request again.

For example:
```
POST https://bob.example/api/BearerRequest
{ "CrossRequestTokenExchange": "NEW-FUTURE-VERSION-THAT-YOU-DONT-KNOW-ABOUT", ... }

400 Bad Request
{ 
    "Message": "Unknown version. Use CRTE-PUBLIC-DRAFT-3 instead.",
    "AcceptVersions": [ "CRTE-PUBLIC-DRAFT-3" ]
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
    "IssuerUrl": "https://saas.example/api/login/crte",
    "Now": 1111863600,
    "Unus": "TmDFGekvQ+CRgANj9QPZQtBnF077gAc4AeRASFSDXo8=",
    "Rounds": 1,
    "VerifyUrl": "https://carol.example/crte/64961859.txt"
}
```

The code calculates the verification hash from this JSON using the process outlined above. The result of hashing the above example request is:
- `26edlsJM9WD9c/j49EaGXbFmSqMClGU0g6AnitR32Ys=`<!--CASE_STUDY_HASH-->

The hash is saved as a text file to her web file server using the random filename selected earlier. With this in place, the POST request can be sent to the SAAS API. The HTTP client library used to make the POST request will perform the necessary TLS handshake as part of making the connection.

## Checking the request
The SAAS website receives this request and validates the request body, performing the following checks:
- The request arrived via HTTPS.  :heavy_check_mark:
- The version string `CRTE-PUBLIC-DRAFT-3` is known.  :heavy_check_mark:
- The `IssuerUrl` value is a URL belonging to itself - `saas.example`.  :heavy_check_mark:
- The `Now` time-stamp is reasonably close to the server's internal clock.  :heavy_check_mark:
- The `Unus` value represents 256 bits encoded in base-64.  :heavy_check_mark:
- The `Rounds` value is within its acceptable 1-99 rounds.  :heavy_check_mark:
- The `VerifyUrl` value is an HTTPS URL belonging to a known user - Carol.  :heavy_check_mark:

(If the version or `Rounds` property was unacceptable, a 400 response might be used to negotaiate acceptable values. In this case, that won't be neccessary.)

The service has passed the request for basic validity, but it still doesn't know if the request has genuinely come from Carol's service or not. To perform this step, it proceeds to check the verification hash.

## Retrieval of the verification hash
Having the URL to get the Caller's verification hash, the Issuer service performs a GET request for that URL. As part of the request, it makes the following checks:
- The URL lists a valid domain name.  :heavy_check_mark:
- The TLS handshake completes with a valid certificate.  :heavy_check_mark:
- The GET response code is 200.  :heavy_check_mark:
- The response's `Content-Type` is `text/plain`.  :heavy_check_mark:
- The text, once CRLF endings have been trimmed, is 256 bits encoded in BASE-64.  :heavy_check_mark:

(If any of these tests had failed, the specific error would be indicated in a 400 error response to the initial POST request. As the download was successful, that isn't needed.)

Having sucessfully retrieved a verification hash, it must now find the expected hash from the original POST request body.

## Checking the verification hash
The Issuer service performs the same PBKDF2 operation on the JSON request that the Caller performed earlier. With both the retrieved verification hash and the internally calculated expected hash, the Issuer service may compare the two strings. If they don't match, the Issuer service would make a 400 response to the original POST request complaiming that the verification hash doesn't match the request body. In this case, they do indeed match and the Issuer is reassured that the Caller is actually Carol.

Satisfied the request is genuine, the Saas service generates a Bearer token and returns it to the caller as the response to the POST request, together with its expiry time.<!--CASE_STUDY_RESPONSE-->
```
{
    "BearerToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsIiI6ImJpbGxwZy5jb20vbmdneXUifQ.eyJzdWIiOiJjYXJvbC5leGFtcGxlIiwiaXNzIjoic2Fzcy5leGFtcGxlIiwiaWF0IjoxMTExODYzNjAwLCJleHAiOjExMTE5NDY0MDB9.D-4kaxZNAlX_xwexDKIhFYy0FiuLEc864zJUNSjHX2A",
    "ExpiresAt": 1111946400
}
```

Carol may now delete the verification hash from her web server, or allow a housekeeping process to tidy it away. She may now use the issued Bearer token to call the Saas API until that token expires.

## Answers to Anticipated Questions

### What's wrong with keeping a pre-shared secret long term?
They require management and secure storage and your server-side code will need a way to access them without access to your master passwords or MFA codes. Since you'll have already made the investment in configuring TLS on both sides, why not utilize that and get rid of the pre-shared secrets?

### I don't have a web server.
Then this exchange is not for you. It works by having two web servers make requests to each other.

### I have one on the other side of the Internet.
If your server satisfies all of these properties, it would be useful for this exchange:
- The server runs TLS with a known URL.
- You can upload small files into a folder.
- You control the filename, or you can know the filename before uploading.
- No-one else can upload files to this folder.

### TLS supports client-side certificates.
To use client-side certificates, the client side would need access to the private key. This would need secure storage for the key, avoidance of which is the main motivation of this idea. 

### How long should a bearer token last until expiry?
Up to you but (finger in the air) I'd go for an hour. If the exchange takes too long, remember you can do it in advance and have the Bearer token ready if needed.

### What if either web service uses a self-signed TLS certificate or one signed by an untrusted root?
If a connection to an untrusted TLS certificate is found, abandon the request and maybe log an error. 

Since this exchange relies on a pre-existing relationship, you could perhaps allow for "pinned" TLS certificates to be configured.

### What if an attacker attempts to eavesdrop or spoof either request?"
The attacker can't eavesdrop or spoof because TLS is securing the channel.

### What if an attacker sends a fake POST request to an Issuer?
The Issuer will attempt to retrieve a verification hash file from the Caller's website. As the Caller won't have a verification hash that matches the fake POST request, the attempt will fail.

### Does it matter if any part of the POST request is predictable?
Only the value of the `Unus` property needs to be unpredictable. All of the other values may be completely predictable to an attacker because only one unpredictable element is enough to make the verification hash secure.

### What if an attacker downloads a verification hash intended for a different issuer?
To exploit knowing a verification hash, an attacker would need to build a valid JSON request body that resolves to that hash. As the value of the `Unus` property is included in the hash but not revealed to an attacker, the task is computationally unfeasible.

### What if a Caller sends a POST request to an Issuer, but that Issuer passes the request along to a different Issuer?
An evil Issuer would first need to perform their replat attack before the `Now` timestamp gets too old, and also before the genuine Caller deletes their verification hash, having completed the transaction it was meant for.

If those are not a problem, the second Issuer receiving a replayed request will find the verification hash matches, but will reject the request because it was for a different Issuer. It is for this reason it is important that the Issuer validates the request including rejecting requests with the wrong `IssuerUrl` property value.

### What if an attacker floods the POST request URL with many fake requests?
For each attempted fake POST request, the Issuer will attempt to retrieve the verification hash. Since the genuine Caller does not publish hashes for these fake requests, the Issuer will reject these attempts.

The fact that a POST request will trigger a second GET request might be used as a denial-of-service attack. For this reason, it may be prudent for an Issuer to track IP address blocks with a history of making bad POST requests and rejecting subsequent requests that originate from these blocks.

It may also be prudent to keep the POST URL secret, so attackers can't send in a flood of fake requests if they don't know where to send them. As this would only be a method to mitigate a denial-of-service attack, the secret URL doesn't have to be treated as secret needing secure storage. The URL could be set in a unencrypted config file and if it does leak, be replaced without urgency at the participant's convenience. The security of the exchange relies on TLS and the verification hash, not the secrecy of the URLs.

### What if a malicious Caller causes the connect to retrieve the verification hash to stay open?
[I am grateful to "buzer" of Hacker News for asking this question.](https://news.ycombinator.com/item?id=38110536)

An attacker sets themselves up and configures their website to host verification hash files. However, instead of responding with verification hashes, this website keeps the GET request open and never closes it. As a result, the Issuer server is left holding two TCP connections open - the original POST request and the GET request that won't end. If this happens many times it could cause a denial-of-service by the many opened connections being kept alive.

We're used to web services making calls to databases or file systems and waiting for those external systems to respond before responding to its own received request. The difference in this scenario is that the external system we're waiting for is controlled by someone else who may be hostile.

This could be mitigated by the Issuer configuring a low timeout for the request that fetches the verification hash. The allowed time only needs to be long enough to perform a single round of SHA256 and the usual roundtrip delay of a request. If the verification hash requests takes too long the overall transaction can be abandoned.

Nonetheless, I have a separate proposal that will allow for the POST request to use a 202 "Accepted" response where the underlying connection can be closed and reopened later. Instead of keeping the POST request open, the Issuer can close the request and the Caller may reopen it at a later time.

### Why does the SHA256 operation have a fixed salt?
The fixed hash only appears in this document and does not go over the wire as a request is made, so any hash produced which passes validation must have been calculated by someone reading this document. Any hashes produced will have no value outside of this documented exchange.

### What are the previous public drafts?

- [Public Draft 1](https://github.com/billpg/CrossRequestTokenExchange/blob/22c67ba14d1a2b38c2a8daf1551f065b077bfbb0/README.md)
  - Used two POST requests in opposite directions, with the second POST request acting as the response to the first.
- [Public Draft 2](https://github.com/billpg/CrossRequestTokenExchange/blob/2165a661e093754e038620d3b2be1caeacb9eba0/README.md)
  - Updated to allow a 202 "Accepted" response to the first POST request, avoiding to need to keep the connection open.
  - I had a change of heart to this approach shortly after publishing it.
- Public Draft 3 (This document)
  - Substantial refactoring after realising the verification hash could be a unauthenticated GET request.
  - Removed 202 responses after noting the inner GET request could have a very short timeout and deciding it could work as an independent proposal for any POST request.

## Next Steps

This document is a draft version. I'm looking (please) for clever people to review it and give feedback. In particular I'd like some confirmation I'm using SHA256 correctly. I know not to "roll your own crypto" and this is very much using pre-existing components. Almost all the security is done by TLS and SHA256 is there to bring the two requests together. If you have any comments or notes, please raise an issue on this project's github.

In due course I plan to deploy a publicly accessible test API which you could use as the other side of the exchange. It'd perform the role of an Issuer by sending your API tokens on demand, as well as perform the role of a Caller by asking your API for a token.

Ultimately, I hope to publish this as an RFC and establish it as a public standard.

Regards, Bill. <div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
