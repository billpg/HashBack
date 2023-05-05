# BearerTokenExchange
Server-to-Server Bearer Token Exchange Protocol

## What problem am I trying to fix?

Web Server **A** wants to make a request to Web Server **B**. Because we have TLS, A can be sure that it is talking with server B. However, B can't be at all certain the request it is processing came from A.

Most APIs deal with this using a process of shared secrets. The management console of an SaaS service will have a way to generate a shared secret that needs to be configured into the service making the request. Because that secret will be expected to be kept long term, it needs to be kept secure, requiring secret stores.

Since we do have TLS, if both sides of the conversation are already web servers, both sides already have a system of exchanging keys that's been established for decades. TLS itself.

This protocol allows servers to pass short-term "Bearer" authentication tokens between servers without having to store long-term secrets.

## How would it work?

You're a rutabaga farmer and you want to sell rutabagas. There's a service out there that handles shopping carts, payments and customer support. All you have to do is package up thse rutabagas and deliver them when that service tells you to.

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
   "RequestId: "B85636A1-41C1-409B-B112-B5F56E9575D7",
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



