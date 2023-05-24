# BearerTokenExchange
Server-to-Server Bearer Token Exchange Protocol

## The elevator pitch.

Alice and Bob are normal web servers with an API.
- (Alice opens an HTTPs request to Bob.)
- "Hey Bob. I want to use your API but I need a Bearer token."
  - (Bob opens a separate HTTPS request to Alice.)
  - "Hey Alice, have this Bearer token."
  - "Thanks Bob."
- "Thanks Alice."

When anyone opens an HTTPS request, thanks to TLS they can be sure who they are connecting to, but neither can be sure who was making the request.

By using *two* HTTPS requests in opposite directions, two web servers may perform a brief handshake and exchange a *Bearer* token. All without needing pre-shared secrets, additional PKI systems or having to deal with TLS client certficates.

### What's a Bearer token?

A Bearer token is string of characters. It might be a signed JWT or it might be a short string of random characters. If you know what that string is, you can include it any web request where you want to show who you are. The token itself is generted (or "issued") by the service that will accept that token later on as proof that you are who you say you are, because no-one else would have the token. 

```
POST /api/some/secure/api/
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ4IjoiYmlsbHBnLmNvbS9qMSJ9.nggyu
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Cookies are a common variation of the Bearer tokens.

Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself.

This document describes a mechanism to request a Bearer token. The process takes a little time so you'd keep a copy of the token until it has expired. You could request one ahead of time so you've got one ready when you need it.

## The exchange in a nutshell.

The core exchange takes the form of two HTTPS requests, the **TokenRequest** and the **TokenIssue**.

### TokenRequest

Alice needs a Bearer token from Bob. She makes a request to the URL Bob published.

```
POST https://bob.example/api/RequestBearerToken
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "SendToDomain": "alice.example",
    "MacKeyHex": "253893686BB94369A271A04010B674B17EBD984D7A5F85788EB856E50350788E"
}
```

- `POST https://bob.example/api/RequestBearerToken`
  - Bob published and documented this URL. He could have instead used a `401` response to indicate where this request should take place. 
  - (See section **401 Response** later.)
- `"PickAName": "DRAFTY-DRAFT-2",`
  - This indicates the client is attempting to use the PickAName process and is using the version described in this document.
  - The client might prefer to use a later versio. If the service does not support that version, it may indicate the versions is knws in a `400` response. (See section **Version Negotiaton** later.)
- `"Realm": "MyRealm",`
  - The service may specify a particuar realm which is specified here.
  - The request may use `null` to mean the same as not having a realm.
- `"RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B"`  
  - A GUID that will be repeated back in the new HTTPS request sent back to the caller.
  - This is optional. `null` is the same as not supplying a value.
- `"SendToDomain": "alice.example",` 
  - A success response doesn't come back via the same HTTPS response, but via a new HTTPS request sent to the website at the domain specified here.
  - The value is a full domain name. If the domain is IDN type, the value is encoded using normal JSON/UTF-8 rules.
- `"MacKeyHex": "253893686BB94369A271A04010B674B17EBD984D7A5F85788EB856E50350788E"`
  - A key that will be used to "sign" the Bearer key with an HMAC, confirming the Bearer key came from the expected source.
  - The value is 256 bits of cryptographic quality randomness.

### TokenIssue

At this stage, Bob doesn't know for certain that the TokenRequest actually came from Alice. Nonetheless, Bob issues a Bearer token for Alice and sends it via a new separate HTTP request to Alice. The URL used will always be `https://` + (Value of `SendToDomain`) + `/.well-known/PickAName/TokenIssue`. Thanks to TLS, Bob can be sure that only Alice is receiving it.

```
POST https://alice.example/.well-known/PickAName/TokenIssue
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "BearerToken": "hnGbHGat49m1zRcpotQV9xPh7j8JgS1Qo0OCy4Wp2hIS43RJfhEyDvZnyoH5YZA",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "HashHex": "(TODO)",
}
```

- `POST https://alice.example/.well-known/PickAName/TokenIssue`
  - The URL, except for the domain name, is fixed.
- `"PickAName", "Realm", "RequestId"`
  - These are copied from the initial TokenRequest body.
  - The request ID might be used to unite this request with the initial request.
- `"BearerToken": "...",`
  - This is the requested Bearer token. It may be used for subsequent requests with the API.
- `"ExpiresAt": "2023-10-24T14:15:16Z",`
  - The UTC expiry time of this Bearer token in ISO format.
- `"HashHex": "(TODO)",`
  - The HMAC-256 hash of the Bearer token, using the value of `MacKeyHax` from the TokenRequest body as the key. 

### HTTPS Responses

Once both sides are content their side of the transaction is complete and sucessful, they close down the request with a `204` response to indi, te success.

If the server can't use or has any kind of vaidation error with a request, it should respond with a `400` error. The body should include enough detail for a developer to fix the underlying issue.

Server errors should result in an applicable `5xx` error, indicating that the caller should try again later.

## 401 Response - Kicking it off

The above interactions are the core of this protocol, but the traditional first step with an HTTP request is to make it *without* authentication and be told what's missing. The HTTP response code for a request that requires authenticaton is `401` with a `WWW-Authentication` header.

```
GET https://bob.example/api/status.json

401 Needs authentication...
WWW-Authenticate: PickAName realm=MyRealm url=/api/RequestBearerToken
```

- The optional `realm` parameter value, if used, should be copied into the TokenRequest message. It allows for variation of the issued token if desired.
- The required `url` parameter specifies the URL to send the TokenRequest POST.

Per the HTTP standard, this `WWW-Authenticate` header may appear alongside other `WWW-Authenticate` headers, or together in a single header separated by commas.

An API, instead of using this mechanism, might document where this end-point is and the calling code would skip directly to making that request without waiting to be told to. Nonetheless, this response informs the caller they need a Bearer token, the URL to make a POST request to get it and the "Realm" to use when making that request.

## Redirect responses to TokenIssue requests

Because the authentication is done at the level of a domain, the URL for the POST request is always done with the path `/.well-known/PickAName`. The URL `/.well-known/` is reserved for operations at the "site" level and should be reserved and sepaated from day-to-day use such as user names. The response to a POST request may be a 307 or 308 redirect. If this is the case the the caller should repeat the POST request at the new location.

## Version Negotiation

The intial request JSON includes a property named `"PickAName"` with a value specifying the version of this protocol the client is using. As this is the first (and so far, only) version, all requests should include this string. 

If a request arrives with a different unknown string value to this property, the servive should respond with a `400` (bad request) response, but with a JSON body including a property named `"AcceptVersion"`, listing all the supported versions in a JSON array of strings.

```
POST https://bob.example/api/BearerRequest
{ "PickAName": "NEW-FUTURE-VERSION-84", ... }

400 Bad Request
{ 
    "Message": "Unknown version.",
    "AcceptVersion": [ "DRAFTY-DRAFT-2" ] 
}
```

The requestor may now repeat that request but interacting according to the rules of this document.

## The HMAC hash

The recipient of the TokenIssue request will be able to confirm the source of the Bearer token by repeating the HMAC hash and checking it matches the one supplied alongside the Bearer token. The following parameters are used to perform the HMAC.
- Algorthithm: HMAC-SHA256
- Key: (Value of MacKeyHex, after decoding hex into bytes.)
- Value: (Value of Bearer token as UTF-8 bytes.)

If the hash doesn't match the one supplied in the TokenIssue request, the Bearer token should be discarded. 

## RequestVerify

This request allows the service recieving a TokenRequest to first verify:
- That the web service behind the domain named in the request implements this protocol.
- That the service has possesion of the HMAC key supplied in the original request.
- That the other properties of the request (ReqquestID, Realm, Version) are correct.
- To avoid the burden of issuing a token for a fake TokenRequest.

There are two HMAC-SHA256 operations that both sides of the conversation need to perform as part of verifying that the other side of the conversation is in possession of the HMAC key without reavling the key itself. In both the request and response JSON bodies, the hash result will be encoded in hex and supplied under the JSON key "HashHex" in both request and response.

The request's fixed value is "PickAName-DRAFTY-DRAFT-2-RequestVerifyRequest".
The response's fixed value is "PickAName-DRAFTY-DRAFT-2-RequestVerifyResponse".

This request is made to the `/.well-known/PickAName/RequestVerify` URL with the following structure.
```
POST https://alice.example/.well-known/PickAName/RequestVerify
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "HashHex": "(TODO)"
}
```

The request repeats the `"PickAName"`, `"Realm"` and `"RequestId"` properties from the original TokenRequest body allowing the verifying server to look up which HMAC key it supplied in that request.

The response to the POST request will be:
```
200 OK
Content-Type: application/json
{
    "IsRequestVerfied": true,
    "HashHex": "(TODO)"
}
```

As this is a normal HTTP response, there is no need to repeat the request ID and other properties. The `"IsRequestVerified"` JSON property will be `true` or `false` indicating if the request was valid and that the HMAC hash was valid or not. If the value if `false` then the response need not include the `"HashHex"` property. 

Note that `200` is used for both confirmation and rejection responses. `400` or similar responses should only be used if the request is bad, such as missing properties.

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
