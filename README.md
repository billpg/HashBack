# BearerTokenExchange
Server-to-Server Bearer Token Exchange Protocol

## The elevator pitch.

Alice and Bob are normal web servers with an API.
- (Alice opens an HTTPs request to Bob.)
- "Hey Bob. I want to use yor API but I need a Bearer token."
  - (Bob opens a separate HTTPS request to Alice.)
  - "Hey Alice, have this Bearer token."
  - "Thanks Bob."
- "Thanks Alice."

When anyone opens an HTTPS request, thanks to TLS they can be sure who they are connecting to, but sometime the recipient also needs to know who the client is.

By using *two* HTTPS requests in opposite directions, two web servers may perform a brief handshake and exchange a *Bearer* token. All without needing pre-shared secrets, additiona PKI systems or having to deal with TLS client certficates.

## How does it work?

Alice Jones is a rutabaga farmer and her business is growing and selling rutabagas. She has a website, **rutabaga.example**, where she take orders from the public and allows deliveries to be tracked.

Bob Smith runs **veggies.example**, a website that sells boxes of a mixture of vegetables to the public. He doesn't own any farms but instead contracts with farming businesses to supply those vegetables which he sorts into boxes and ships to his customers.

Because both Alice and Bob own web servers on the public internet, each with TLS certificates already set up, they are ideal for this exchange to work. 

### 401

The traditional first step with an HTTP request that requires authentication is to make the request *without* authentication and be told what's missing. The HTTP response code for a request that requirs authenticaton is **401** with a **WWW-Authentication** header.
```
GET https://veggies.example/api/status.json

401 Needs authentication...
WWW-Authenticate: PickAName BearerRequest=https://veggies.example/api/bearer_request
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
    "Requestor"; "rutabagas.example",
    "RequestId: "B85636A1-41C1-409B-B112-B5F56E9575D7",
    "TempKey": "(todo)"
}
```

- `"Version"` is a string that identifies that we're doing a bearer token exchange and that this version of this document is being followed.
  - If the service does not support this version, it may respond with a 400 response and a list of versions it does support. See below.
- `"Requestor"` is the full domain name of the service that making the request.
- `"RequestId"` is a GUID that will be repeated in the subsequent request to supply the requested Bearer token.
- `"MacKey"` is a string to be used as the HMAC key later on.
  - It must only contain printable ASCII characters. (33 to 126.) 
  - It must contain at least 256 bits of cryptographic qualiy randomness.


### BearerIssue
At this point, the server *veggies.example* doesn't know if the request for a Bearer token came from the genuine *rutabagas.example* or not. Only that a someoe that *claims* to be this server is making the request.  

The service generates a Bearer token for the claimed domain. As only a service in possession on a legitimate TLS key for that domain will be capable of using it, it is no a problem if the requestor is not actually of the claimed domain. (There may be virtue in specifying an optional "was this you?" request to allow this possibility to be checked before generating the token.)

While keeping the BearerRequest connection open and unresponded-to, the recipient of the BearerRequest will open a new separate HTTPS request to the domain specified in the `Requestor` property.
```
POST https://rutabagas.example/.well-known/PickAName/
Content-Type: application/json
{
    "Version": "DRAFTY-DRAFT-2",
    "RequestId: "B85636A1-41C1-409B-B112-B5F56E9575D7",
    "BearerToken": "(TODO)",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "HashHex": "(TODO)",
}
```

- `"Version"` and `"RequestId"` are repeated from the earlier request to link this with the earlier transaction.
- `"BearerToken"` is the bearer token itself.
- `"ExpiresAt"` is the time when this Bearer token will expire.
- `"HashHex"` is the result of running HMACSHA256 on the Bearer token with the *MacKey* from the   

The issuer can be sure only the legitimate requestor received the Bearer token thanks to TLS. The requestor can be sure the tken is genuine because it was verified by the MAC using the key it supplied earlier.

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

### What's a Bearer token?

A Bearer token is string of characters. It might be a signed JWT or it might be a string of random characters. If you know what that string of characters are, you can include it any web request where you want to show who you are.

```
POST /api/some/secure/api/
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtZXNzYWdlIjoiaHR0cHM6Ly9iaWxscGcuY29tL2p3dG1zZy8ifQ.iSGtXjdOYkS4kedikg8eEVZczPnIdjaHMdMGqu0ai0M
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Cookies (those things websites keep asking if you consent to) use the same mechanism as Bearer tokens. In fact a lot of APIs will check both the `Authorization:` and `Cookie:` headers and treat them the same.

Bearer tokens typically have short life-times and you'd normally be given an expiry time along with the token itself.

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



