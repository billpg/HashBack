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
