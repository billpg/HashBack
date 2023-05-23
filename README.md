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

Alice needs a Bearer token from Bob. She makes a request to the URL Bob pushlished.

```
POST https://bob.example/api/RequestBearerToken
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId: "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "SendToDomain": "alice.example",
    "MacKeyHex": "253893686BB94369A271A04010B674B17EBD984D7A5F85788EB856E50350788E"
}
```

- `POST https://bob.example/api/RequestBearerToken`
  - Bob published and documented this URL. He could have instead used a `401` response. (See section **401 Response** later.)
- `"PickAName": "DRAFTY-DRAFT-2",`
  - This indicates the client is attempting to use the PickAName process and is using the version described in this document.
  - The client might prefer to use a later versio. If the service does not support that version, it may indicate the versions is knws in a `400` response. (See section **Version Negotiaton** later.)
- `"Realm": "MyRealm",`
  - The service may specify a particuar realm which is specified here.
  - The request may use `null` to mean the same as not having a realm.
- `"RequestId: "C4C61859-0DF3-4A8D-B1E0-DDF25912279B"`  
  - A GUID that will be repeated back in the new HTTPS request sent back to the caller.
  - This is optional. `null` is the same as not supplying a value.
- `"SendToDomain": "alice.example",` 
  - A success response doesn't come back via the same HTTPS response, but via a new HTTPS request sent to the website at the domain specified here.
  - The value is a full domain name. If the domain is IDN type, the value is encoded using normal JSON/UTF-8 rules.
- `"MacKeyHex": "253893686BB94369A271A04010B674B17EBD984D7A5F85788EB856E50350788E"`
  - A key that will be used to "sign" the Bearer key with an HMAC, confirming the Bearer key came from the expected source.
  - The value is 256 bits of cryptographic quality randomness. 

Bob opens a new separate HTTP request to Alice. The URL used will always be `https://` + (Value of `SendToDomain`) + `/.well-known/PickAName`.

```
POST https://alice.example/.well-known/PickAName
Content-Type: application/json
{
    "PickAName": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId: "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "BearerToken": "F390DA7BE46F45F5AA27DB0020558FBAFB9194A165CD4158BBC9F6C1DBEE3DD1",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "HashHex": "(TODO)",
}
```

- ``

## How does it work?

Alice Jones is a rutabaga farmer and her business is growing and selling rutabagas. She has a website, **rutabaga.example**, where she take orders from the public and allows deliveries to be tracked.

Bob Smith runs **veggies.example**, a website that sells boxes of a mixture of vegetables to the public. He doesn't own any farms but instead contracts with farming businesses to supply those vegetables which he sorts into boxes and ships to his customers.

Because both Alice and Bob own web servers on the public internet, each with TLS certificates already set up, they are ideal for this exchange to work. 

### 401

The traditional first step with an HTTP request that requires authentication is to make the request *without* authentication and be told what's missing. The HTTP response code for a request that requirs authenticaton is **401** with a **WWW-Authentication** header.
```
GET https://veggies.example/api/status.json

401 Needs authentication...
WWW-Authenticate: PickAName Realm=MyRealm BearerRequest=https://veggies.example/api/bearer_request
```

In the real world, an API would probably document where this end-point is and the calling code would skip directly to making that request without waiting to be told to. Nonetheless, this response informs the caller they need a Bearer token and the URL to make a POST request to get it.

Also note that this URL doesn't have to be at the same domain as the original request. A service could outsource it's Bearer token generation if it wishes.

### BearerRequest

Either when instructed to by a 401 response or by API documentation, when the client needs to request a Bearer token, they start by making opening a new request.
```
POST https://veggies.example/api/bearer_request
Content-Type: application/json
{ 
    "Version": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "Requestor"; "rutabagas.example",
    "RequestId: "B85636A1-41C1-409B-B112-B5F56E9575D7",
    "MacKeyHex": "(todo)"
}
```

- `"Version"` is a string that identifies that we're doing a bearer token exchange and that this version of this document is being followed.
  - If the service does not support this version, it may respond with a 400 response and a list of versions it does support. See below.
- `"Realm"` is a copy of the Realm string supplied in the `WWW-Authenticate` header.
  - If not used, this property should be missing for have a `null` value.
- `"Requestor"` is the full domain name of the service that making the request.
- `"RequestId"` is a GUID that will be repeated in the subsequent request to supply the requested Bearer token.
- `"MacKeyHex"` is 256 bits of cryptographic quality randomness, hex encoded, to be used as the HMAC key later on.

### BearerIssue
At this point, the server *veggies.example* doesn't know if the request for a Bearer token came from the genuine *rutabagas.example* or not. Only that a someoe that *claims* to be this server is making the request.  

The service generates a Bearer token for the claimed domain. As only a service in possession on a legitimate TLS key for that domain will be capable of using it, it is no a problem if the requestor is not actually of the claimed domain. (There may be virtue in specifying an optional "was this you?" request to allow this possibility to be checked before generating the token.)

As well as the Bearer token itself, the new request also needs to include an HMAC of the token using the `"MacKey"` value from the earlier request. This will allow the recipient of the token to be sure it came from the expected source because no-one else would know that key. The following parameters are used to perform the HMAC.
- Algorthithm: HMAC-SHA256
- Key: (Value of MacKeyHex, after decoding hex into bytes.)
- Value: (Value of Bearer token as UTF-8 bytes.)

While keeping the BearerRequest connection open and unresponded-to, the recipient of the BearerRequest will open a new separate HTTPS request to the domain specified in the `Requestor` property.
```
POST https://rutabagas.example/.well-known/PickAName
Content-Type: application/json
{
    "Version": "DRAFTY-DRAFT-2",
    "Realm": "MyRealm",
    "RequestId: "B85636A1-41C1-409B-B112-B5F56E9575D7",
    "BearerToken": "(TODO)",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "HashHex": "(TODO)",
}
```

- `"Version"`, `"Realm"` and `"RequestId"` are repeated from the earlier request to link this with the earlier transaction.
- `"BearerToken"` is the bearer token itself.
- `"ExpiresAt"` is the time when this Bearer token will expire.
- `"HashHex"` is the result of running HMACSHA256 on the Bearer token with the *MacKeyHex* value from the earlier request

The issuer can be sure only the legitimate requestor received the Bearer token thanks to TLS. The r
equestor can be sure the tken is genuine because it was verified by the MAC using the key it supplied earlier.

Because the authentication is done at the level of a domain, the URL for the POST request is always done with the path `/.well-known/PickAName`. The URL `/.well-known/` is reserved for operations at the "site" level and should be reserved and sepaated from day-to-day use such as user names. The response to a POST request may be a 307 or 308 redirect. If this is the case the the caller should repeat the POST request at the new location.

Once the recipient has the Bearer token, it may confirm it is genuine by repeating the HMAC and confirming the hash matches the once supplied. If it does, it can be sure the Bearer token is genuine and can proceed to use it in subsequent requests.

Both requests handled, each side can close their respective requests with a 204 code.

-------------------------------------------------------------------

How do you know when an order is placed? This service allows you, as a supplier, to register a "callback". When a customer makes a purchase, it will call your API with a very simple POST request.
```
POST /api/deliver/those/rutabagas/
{
    "OrderId": "EBCAC9A1-99E5-4BF0-8354-16EEF15D8787",
    "Quanity": 5000,
    "Quality": "Excellent",
    "Delivery": "123 Fake Street, New Orleans"
}

204 OK
```

By responding 204 (the traditional way to respond with no-content other than a thumbs-up) you are agreeing to make the supply. Your service code has checked stock levels and written the order into the order book database. Once delivered, another API would reconcile invoices and arrange to make payment.

It isn't enough to have written this API if no-one knows to call it. Once tested and deployed, you need to log-in to the SaaS API Management Console and configure it with the URL on your web server. The settings page is organized well and there's a text-box for you paste the URL you've selected.

### Who is it?

If that was it, we'd have a problem. Without authentication, anyone could send in a POST request to your server. You don't know if the request came from a service that will pay you or from stranger hoping to chance some free rutabagas.

Fortunately, the SaaS people have thought of this and they won't let you set a URL without choosing an authentication option. The first option is a MAC using a shared secret, but we're trying to get away from those.

The next option is to use **Server to Server Bearer Token Exchange**. If you select this option there's another text box for you to type in the URL that will implement your side of the exchange. When their service needs to call your API to deliver some rutabagas, it will first perform this exchange and it will have a Bearer token, which it can then use to authenticate itself when it makes that POST call to place a delivery order.


## The Exchange

For this worked example, `rutabaga-farmer.example` is your website and `veggie-store.example` is the store-front service you've contracted to sell our rutabagas. You've implemented, tested and deplyed both the order-book API discussed above and your side of the token exchange API.

The store-front service needs a Bearer token for your website. Maybe it's deciding to get this transaction out of the way long in advance or maybe it will only do this when it needs to. Either way, it makes a POST request to your web server.

```
POST https://rutabaga-farmer.example/api/this/is/veggies-store/give/me/a/bearer/token/please/
{
   "Version": "DRAFTY-DRAFT-1",
   "InitiatorTempKey": "(fill this in)",
}
```

The `"version"` property indicates which version of this protocol it wishes to use. If your server doesn't support this version, it may respond with a 400 (Bad Request) error, listing the versions it does support.
```
400 Bad Request
{
   "AcceptableVersions": [ "DRAFTY-DRAFT-2", "DRAFTY-DRAFT-3" ]
}
```
In this case, the service will either decide to containue with a version it understands and accepts or logs an error. For the purposes of this example, we'll continue as if "DRAFTY-DRAFT-1" is acceptable by both sides, because that's the version that exists as I write this.

(Future versions might allow many different cryptographic algrithms. This mechanism would allow each side to select between (say) AES-128 and AES-256.)

The `"InitiatorTempKey"` property is some random bytes generated for the purpose of this exchange. Each exchange will have some fresh random bytes. It must be exactly 129 characters in length and be made up of only the base-64 characters.

(Note that the restrction of base-64 characters is only to avoid messiness with control characters and UTF-8. This property is a string of 129 bytes, not 774 base-64-encoded bits.)

### Responding to the request.

Your service, while it doesn't know where the request came from, it knows who the request is claiming to be from. Your service will next generate a new Bearer token, assuming for now the request is genuine.

Using the value of the `InitiatorTempKey`, your service will run PBKDF2 over this string, setting a target of 768 bits. This is for the following:
- A 256-bit AES key.
- A 256-bit AES IV.
- A 256-bit MAC key.
(PBKDF2 also requires a salt, which will be the string "(fill this in)" and a number of rounds which will be 10.) Using these keys, your service encypts (with the AES key and IV) and signs (with the MAC key) the Bearer token it has generated.

Instead of responding to the initiator's first request (because you don't know if it is genuine), your service makes a *new* POST request.
```
POST https://veggie-store.example/heres/your/bearer/token/
{
    "Version": "DRAFTY_DRAFT-1",
    "RequestId": "B85636A1-41C1-409B-B112-B5F56E9575D7",
    "BearerTokenEncrypted": "(Fill this in)",
    "BearerTokenMAC": "(Fill this in too)",
    "Expires": "(ISO timestamp)"
}
```
Repeating the version and request ID back is a courtesy to allow the two requests to be linked together. Nonetheless, the request-id might be guessable but the security isn't in knowing that.

As the initiator knows what their `InitiatorTempKey` was, they can repeat the same PBKDF2 operation with the same parameters and produce the same three keys. With these and the supplied items supplied in this second request, they can decrypt the bearer token and confirm it arrived safely. It can now use that Bearer token, safe in the knowledge it was issued and supplied safely.



