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

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Cookies (those things websites keep asking if you consent to) are a common variation of the Bearer tokens.

Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself.

This document describes a mechanism to request a Bearer token. The process takes a little time so you'd keep a copy of the token until it has expired. You could request one ahead of time so you've got one ready when you need it.

## The exchange in a nutshell.

The core exchange takes the form of two HTTPS requests, the **TokenRequest** and the **TokenIssue**.

### TokenRequest

Alice needs a Bearer token from Bob. She makes a request to the URL Bob pushlished.

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

The intial request JSON includes a property named `"PickAName"` with a value specifying the version of this protocol the client is using. As this is the first (and so far, only) version, all requests should include this string. For servers that implement only this version of the protocol, if the property is missing or has a different value, it should return a `400` response.

(That later version of this document may include guidance for how to indicate inside a 400 response that this older version *is* still supported.)

## The HMAC hash

The recipient of the TokenIssue request will be able to confirm the source of the Bearer token by repeating the HMAC hash and checking it matches the one supplied alongside the Bearer token. The following parameters are used to perform the HMAC.
- Algorthithm: HMAC-SHA256
- Key: (Value of MacKeyHex, after decoding hex into bytes.)
- Value: (Value of Bearer token as UTF-8 bytes.)

If the hash doesn't match the one supplied in the TokenIssue request, the Bearer token should be discarded. 

## "Was that you"?

In the discussion of the TokenIssue request above, the issuer generates a new Bearer token with the assumption that the TokenRequest is genuine. If the effort to generate a Bearer token is low, there is no problem to generate a token only to see it never used.

If the effort to generate a token is high, the issuer may wish to make a request to confirm the original TokenRequest was genuine before making the effort to generate the Bearer token.

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

- As before the `"PickAName"`, `"Realm"` and `"RequestId"` are copied from the original TokenRequest body.
- `"HashHex"` is the result, in hex, of running the same HMAC algorithm as above but with an empty byte-array as the Value instead of the Bearer token.

The recipient can confirm if the request is genuine by confirming the supplied details along with checking if whoever is making this verification request has the same MAC key by chekcing the HMAC result. 

The response to the POST request will be a `200` status code and the JSON request body will have a property named `"IsRequestValid"` with a value of `true` or `false`.

(Note that `200` is used for both confirmation and rejection responses. `400` or similar responses should only be used if the request is bad, such as missing properties.)
