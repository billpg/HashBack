# BearerTokenExchange
Server-to-Server Bearer Token Exchange Protocol

## The elevator pitch.

When two machines need to communicate via an API, the client end needs to include some sort of authentication, which requires a long-term pre-shared-key or similar mechanism thatneeds to be stored and protected from aunauthorized access for the long-term. Wouldn't it be nice if these two machines could communicate without needding a special secret store?

Alice and Bob are normal web servers with an API.
- (Alice opens an HTTPs request to Bob.)
- "Hey Bob. I want to use your API but I need a Bearer token."
  - (Bob opens a separate HTTPS request to Alice.)
  - "Hey Alice, have this Bearer token."
  - "Thanks Bob."
- "Thanks Alice."

When anyone opens an HTTPS request, thanks to TLS they can be sure who they are connecting to, but neither can be sure who was making the request. By using *two* HTTPS requests in opposite directions, two web servers may perform a brief handshake and exchange a *Bearer* token. All without needing any pre-shared secrets. You should already have TLS configured and it protects that exhnage for free.

### What's a Bearer token?

A Bearer token is string of characters. It might be a signed JWT or it might be a short string of random characters. If you know what that string is, you can include it any web request where you want to show who you are. The token itself is generated (or "issued") by the service that will accept that token later on as proof that you are who you say you are, because no-one else would have the token. 

```
POST /api/some/secure/api/
Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJTZWNNc2ciOiJiaWxscGcuY29tL2oxIn0.nggyu
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Cookies are a common variation of the Bearer tokens. Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself.
 
This document describes a mechanism to request a Bearer token.

## The exchange in a nutshell.

There are two particpants in this exchange:
- The **Initiator** who is requesting a Bearer token from...
- The **Issuer** who issues that Bearer token back to the Initiator.

The exchange takes the form of these HTTPS tranactions.

- **Initiate** where the *Initiator* starts of the exchange by calling the *Issuer*.
- **Configure** where the *Issuer* gets a static JSON file from the *Initiator* with basic configuration.
- **Verify** where the *Issuer* may (optionally) verify that the claimed *Initiator* actually made the original Initiate request.
- **Issue** where the *Issuer* supplies the issued Bearer token to the *Initiator*.

### Initiate

The process is started by the Initiator, who needs a Bearer token from the issuer.

```
POST https://issuer.example/api/initiate
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "Step": "Initiate",
    "ClaimedDomain": "initiator.example",
    "HmacKey": "253893686BB94369A271A04010B674B17EBD984D7A5F85788EB856E50350788E"
}
```

- `POST POST https://issuer.example/api/initiate`
  - The choice of URL is up to the issuer to docment and publish.
  - This could be published in the form of a `401` response. (See section **401 Response** later.)
- `"PickAName": "DRAFTY-DRAFT-2",`
  - This indicates the client is attempting to use the PickAName process and is using the version described in this document.
  - The client might prefer to use a later version. If the service does not support that version, it may indicate the versions it does know in a `400` response. (See section **Version Negotiaton** later.)
- `"Realm": "...",`
  - The issuer may issue different tokens depending on the requested realm. If applicable, the initiator should specoify that realm here.
- `"RequestId": "..."`  
  - A GUID that the issuer will supply when connecting back tpo the initiator, who might use this ID to identity which of many requests it is in reference to.
- `"Step": "Initiate"`
  - Shows that this is the Initiate step. If this is not present the request must be rejected with an error response.  
- `"ClaimedDomain": "initiator.example",` 
  - A success response doesn't come back via the same HTTPS response, but via a new HTTPS request sent to the website at the domain specified here.
  - The value is a full domain name. If the domain is IDN type, the value is encoded using normal JSON/UTF-8 rules.
- `"HmacKey": "..."`
  - A key that will be used to "sign" the Bearer token when it is ready, confirming the Bearer key came from the expected source.
  - The value must represent at least 256 bits of cryptographic quality randomness.
  - The value will be reduced to 256 bits using PBKDF2, so it doesn't matter which encoding mechanism (hex, base64, etc) is used.

The issuer service will return a `204` response after the Bearer token has been issued and passed back to the initiator, keeping this HTTPS request open until that separate exchange has completed.

An error response indicates an error either in the request itslf (4xx) or during the processing of the request (5xx). The response body should contain enough detail for a developer to diagnose and fix the error. 

### Configure

Before making POST requests to an unverified service, the Issuer must first make a GET request to the service at the domain specified inside the Initiate request.
The response may be cached according to the response headers per the HTTP standards.

The URL of this GET request is always `https://` + (Value of `ClaimedDomain` property) + `/.well-known/PickAName.json`. The choice to be at a URL under the control of the
site administrator is deliberate. (`/.well-known/` is set aside by RFC8615 for services at the site level. A third-party user should not be able to select this as a user name.)

```
GET https://initiator.example/.well-known/PickAName.json
Accept: application/json

200 OK
Content-Type: application/json
{
    "DRAFTY-DRAFT-2":
    {
        "Dedication": "3imEWtSTqtd8QsFgJUVKG+QfVS0JmGIkAnnyJdtLkqufqdxI",
        "PostHandlerUrl": "/api/ReceiveBearerToken"
    },
    "FUTURE-VERSION-YET-TO-BE-WRITTEN":
    {
        "BestVegetable": "Rutabaga"
    }
}
```

As many different versions of this exchange might use this file and the intention is that it can be deployed as a static file, the
JSON object might contain details for many different versions of this exchange. Each property is named after a version string but as this the first version, only this one object is
required to be listed and the others are to be ignored. (The above example shows a future version yet to be written.)

The object inside the `DRAFTY-DRAFT-2` object must contan two properties:
- `Dedication` - This must have the fixed sring value `"3imEWtSTqtd8QsFgJUVKG+QfVS0JmGIkAnnyJdtLkqufqdxI"`.
  - As this specific string would not appear by accident, if a file with this value at this location is found at this URL, the issuer can be reasonably certain it is communicting with a service that implements this exchange.
- `PostHandlerUrl` - The URL where hhe remaining POST requests of this exchange should be sent to.
  - If starting with `/`, the rules of relative URLs apply.

If the reequest yields any response other than `200` or an expceted cache response, or the `Dedication` string si either mssing or has any value other the one documented above, the Issuer must abandon the process and close off the open Initiate request with an error.

### Verify

The Configure request confirms that the claimed domain has a web service that implements the exchnage documented here. The optional Verify step allows the Issuer to verify that the Initiator actually made the original Initiate request before expending the effort to generate a new Bearer token. At this point of the exchange, the issuer has the word of an unverified initiator that the Initiate request was genuine. (If this step is skipped, the Issuer might create a Berer token only for the supposed Initiator to discard it because it was never asked for in the first place. If the cost of generating a Bearer token is low, you may prefer to skip this step.)

The Verify step is a normal HTTPS interaction with a request and response in the same transaction. Both the request and response will include evidence of possession of the HMAC key supplied in Initiator request.

```
POST https://initiator.example/api/ReceiveBearerToken
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "Step": "Verify",
    "IssuerEvidence": "(TODO)"
}

200 OK
Content-Type: application/json
{
    "IsVerfied": true,
    "InitiatorEvidence": "(TODO)"
}
```

The `"IsVerified"` JSON property will be `true` or `false` indicating if the request was valid and if the HMAC hash supplied in the request was valid or not. If the the value of this property is false, the supposed initiator is informing you that it it did not make the original Initiate request or you do not have the correct HMAC key as yoiu were not able to demonstrate evidence of having it.

If the `IsVerificed` flag is true, the response must also have a property named `InitiatorEvidence` to show that it also has the correct HMAC key. If this evidence is not the expected value, the original Initiate request must be considered faulty and that request is to be closed with an error response.

Note that `200` is used for both confirmation and rejection responses. `400` or similar responses should only be used if the request is bad, such as missing properties.

The eviddence properties are the result of an HMAC operation using the key provided in the original Initiate request. Both issuer and Initiator will perform the same HMAC operations using that HMAC key and confirm if the supplied value is correct. For the `InitiatorEvidenceIssuerEvidence` property, the HMAC input will be a single byte with the value 1. For the `IssuerEvidence` property, the HMAC input will instead be a single byte with the vaue 2. (See section **HMAC Key Derivation** for details of how the key is derived.)

### Issue

The Issue step is performed by the Issuer to pass the Bearer token to the Initiator. Because this happens in a separate HTTPS tranaction to the original Initiaate request, the issuer an be certain only the Initiator will have a copy thanks to the TLS handshake. The Issuer knows the Bearer token must have come from the Issuer because it will be signd by the HMAC key that was only supplied to the Issuer.

If the Issuer chose to skip the Verify step documented above, it still does now know if the original Initiate request was genuine. The result of this HTTPS transaction may be the the supposed initiator discards the supplied Bearer token because it didn't actually ask for it. 

```
POST https://initiator.example/api/ReceiveBearerToken
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "Step": "Issue",
    "BearerToken": "hnGbHGat49m1zRcpotQV9xPh7j8JgS1Qo0OCy4Wp2hIS43RJfhEyDvZnyoH5YZA",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "TokenHash": "(TODO)",
}
```

- `POST https://initiator.example/api/ReceiveBearerToken`
  - The URL specified by the JSON file inside the `/.well-known/` folder.
- `"PickAName", "Realm", "RequestId"`
  - These are copied from the initial TokenRequest body.
  - The request ID might be used to unite this request with the initial request.
- `"Step": "Issue"`
  - This marks this current transaction as the *Issue* step, allowing it to be differentiated from the *Veirfy* step.
- `"BearerToken": "...",`
  - This is the requested Bearer token. It may be used for subsequent requests with the API.
- `"ExpiresAt": "2023-10-24T14:15:16Z",`
  - The UTC expiry time of this Bearer token in ISO format.
- `"TokenHash": "(TODO)",`
  - The hex-encoded HMAC-256 hash of the UTF-8 bytes of the Bearer token, using the value of `HmacKey` from the Initiator request body as the key. 

The response to this HTTPS transaction is `204` to indicate the token was received with thanks. An error response indicates there was a problem and the response body should include enough detail to allow a developer to diagnose and fix the issue. A redirect response should result in the POST request being repeated a the new URL.

Once the initiator has made that `204` response to this HTTPS transaction, the original Initiator transaction that has been held open should be closed by the issuer with its own `204` response.

## 401 Response - Kicking it off

The above interactions are the core of this protocol, but the traditional first step with an HTTP request is to make it *without* authentication and be told what's missing. The HTTP response code for a request that requires authenticaton is `401` with a `WWW-Authentication` header.

```
GET https://bob.example/api/status.json

401 Needs authentication...
WWW-Authenticate: PickAName realm=MyRealm url=/api/RequestBearerToken
```

- The optional `realm` parameter value, if used, should be copied into the Initiate message. It allows for variation of the issued token if desired.
- The required `url` parameter specifies the URL to send the Initiate POST.

Per the HTTP standard, this `WWW-Authenticate` header may appear alongside other `WWW-Authenticate` headers, or together in a single header separated by commas.

An API, instead of using this mechanism, might document where this end-point is and the calling code would skip directly to making that request without waiting to be told to. Nonetheless, this response informs the caller they need a Bearer token, the URL to make a POST request to get it and the "Realm" to use when making that request.

## Version Negotiation

The intial request JSON includes a property named `"PickAName"` with a value specifying the version of this protocol the client is using. As this is the first (and so far, only) version, all requests should include this string. 

If a request arrives with a different unknown string value to this property, the servive should respond with a `400` (bad request) response, but with a JSON body including a property named `"AcceptVersion"`, listing all the supported versions in a JSON array of strings.

```
POST https://bob.example/api/BearerRequest
{ "PickAName": "NEW-FUTURE-VERSION-THAT-YOU-DONT-KNOW-ABOUT", ... }

400 Bad Request
{ 
    "Message": "Unknown version.",
    "AcceptVersion": [ "DRAFTY-DRAFT-2" ] 
}
```

The requestor may now repeat that request but interacting according to the rules of this document.

## HMAC Key Derivation

The Intiate request body will include a property named `HmacKey` that must contain at least 256 bits of cryptographic quality randomness, expressed in any convenient encoding. (Such as Hex or base64.)

As there is no restricion of the range of characters (other than that they are Unicode) they are converted to 256 bits using PBKDF2, which will take an arbitary amount of text and yield a requested number of bits output. The following inputs are used:
- *Salt* - The ASCII bytes of the string `PickAName/DRAFTY-DRAFT-2/2C266D36-53FB-459D-8B4D-AD67737DA026`.
- *Input* - The UTF-8 bytes of the `HmacKey` value.
- *Hash* - SHA256.
- *Rounds* - 10.
- *Output* - 256 bits.

The inclusion of the fixed salt is to ensure the derived key could only be found by reading this document. The use of PBKDF2 itself is to make it difficult to construct a selected key. It is belived that single round of hashing would be sufficient, given the input should already represent 256 bits of cryptographic quality randomness, but 10 rounds ups the burden a little.

## A brief security analysis

### "What if an attacker attempts to eavesdrop or spoof either request?"
TLS will stop this. The security of this protocol depends on TLS working. If TLS is broken then so is this exchange.

### "What if an attacker sends a fake TokenRequest to Bob, pretending to be Alice?"
Bob will issue a new token and send it to Alice in the form of a TokenIssue request. Alice will reject the request because she wasn't expecting one.

### "What if the attacker sends a fake TokenRequest to Bob, but at the same time Alice is making a request and knowing what RequestID she will use?"
The genuine TokenIssue request from Bob to Alice will have a genuine token, but this will fail the HMAC check because the attacker doesn't know what HMAC key she supplied to Bob in the genuine TokenRequest body.

Even if Alice doesn't check the HMAC hash, this is not a problem. The attacker can't intercept it thanks to TLS. The token was genuinely issued so there's no problem if they go ahead and use it. That it was induced by an attacker is no reason to discard it.

### "What if an attacker sends a fake TokenIssue to Alice, pretending to be Bob."
If Alice isn't expecting a TokenIssue request, she will reject an unasked one.

If Alice is expecting a TokenIssue, she will be able to test it came from Bob by checking the HMAC hash. Only Alice and Bob know what the HMAC key is because Alice generated it from cryptographic quality randomness and sent to Bob. Thanks to TLS, no-one else knows the key.

If Alice, for whatever reason, decides to skip testing the HMAC hash, she will have a fake Bearer token. This will fail the first time she tries to use because Bob will reject the request as an unknown Bearer token. (The attacker doesn't know how to generate a genuine Bearer token that Bob will accept.)

### "What if Alice uses predictable randomness when generating the HMAC key?"
The an attacker will be able to send in a fake TokenIssue request to Alice with an HMAC hash that passes validation. Because the attacker still doesn't know how to issue a Bearer token, Bob will reject that Bearer token the first time Alice tries to use it.

Alice shoud use unpredictable cryptographic quality randomness when generating the HMAC key.

### "What if an attacker requests a genuine Bearer token for themselves and then pass that attacker onto Alice in a fake TokenIssue request?"
The attacker still doesn't know how to fake an HMAC hash without knowing the HAC key Alice generated, so this will fail Alice's HMAC test.

If Alice skips the HMAC test, she will have a genuine Bearer token that she thinks is hers, but one that actually identifies the attacker's domain, not Alice's. As Bob will recognise the token as the attacker's, he will not accept any action that requires a Alice's token.

Alice should perform the HMAC test on any TokenIssue requests she receives.

### "What if an attacker makes a TokenRequest to Bob but pretending to be from Carol, a website that does not implement this protocol?"
Then Bob will issue a token for carol.example, but the TokenIssue request passing it along to that unwitting website will fail with a 404 error.

The choice of fixing the request to the `/.well-known/` folder was deliberate. If an attacker could induce Bob to perform a POST request to any URL the attacker chose, the service might misinterpret the request as something other than a TokenIssue request.

If we may consider a worst case scenario, the default behaviour of Carol is to publish the contents of all incoming requests, the attacker would have a copy of the valid Bearer token by reviewing those published logs, but it would be a Bearer token with a claim for a domain that doesn't have a history of using this protocol. The attacker could equally purchase their own domain and get a valid Bearer token that way.

For this reason, websites issuing tokens via a TokenIssue request who additionally do not require a prior relationship, should first check the original TokenRequest message is genuine by performing a RequestVerify request.

### "What if an attacker signes up for someone's website under the user name '.well-known' and can host server-side scripts there?"
If someone can cause code to run in response to request at `/.well-known/' URLs, that individual owns the service.

Services allowing users to select their own names at the top level should set this top-level folder aside.

### "What if the intiator's domain has expired and the attacker has purchased it.
This protocol verifies claims based on operating a website at a particular domain. If you own the domain, then you have the means to be authenticated at that domain. If that kind of authentication claim is not suitable, this protocol is not appropriate.

## Questions to investigate.

- Given that there's a RequestVerify step, do we need to restrict URLs to the `/.well-known/` folder and instead allow any URL to be specified as the one to send RequestVerify and Tokenissue requests to? Instead of a domain, the claim would be based on the whole URL instead.
- Would it be better to pass the HMAC key through PBKDF2 with a fixed salt first?
- Am I doing HMAC correctly?
- Does there need to be pre-flight step before a POST request?
- An earlier version AES encrypted the Bearer token in the TokenIssue request using an AES key and IV from the initiator, alongside the HMAC key. I removed it as my security analysis didn't show it was needed. Was I right to take it out?
- What should I call it? What string should I find-and-replace 'PickAName' with?
