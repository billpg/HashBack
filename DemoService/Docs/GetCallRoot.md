# HashBack Call Demo Service

Send a `POST` request to this endpoint with **your own service's URL** as the plain-text body,
and this demo service will make a properly-formed, HashBack-authenticated `GET` request to that
URL on your behalf. It publishes the verification hash on its own `/hash/` store first, so you
don't need anywhere of your own to host it. The response is a full report of what happened,
including any attempts your server made to fetch the hash back.

## 🔒 Before you start: your target needs to opt in
So this service can't be turned into an open relay for authenticated-looking requests against
domains that never asked for one, it will only send that outbound GET to a target that has
first published its consent at:

```
https://{your-domain}/.well-known/demo-hashback-dev.json
```

See [/permit](/permit) for the exact JSON format this file needs and what each field controls.

Calling this demo service's own `/hello/` endpoint (see "Demo-Service-Ception!" below) is
always allowed and needs no file of its own. Anything else without a matching grant is
rejected with `400 Bad Request` before any outbound request is attempted.

## 📮 Making a request
The request body should be `text/plain`: just the URL you want this service to call.

``` bash
curl -X POST "https://demo.hashback.dev/call/" \
    -H "Content-Type: text/plain" \
    --data "https://your-domain.example/some/endpoint"
```

The service builds a HashBack claim addressed to your URL's host, containing:
- A `Version` string of `BILLPG_DRAFT_4.2`.
- A `Host` string matching the host of the URL you provided.
- A `Now` from the server's system clock.
- A `Unus` string from randomness to make the hash unique.
- A `Verify` string: a URL on this service's own `/hash/` store where the hash can be
  retrieved.

## 📋 The report
A `200` response is a `text/plain`, markdown-formatted report of the whole exchange:
- The outbound GET this service made to your URL — its response status, headers and body,
  and the SHA-256 (base64-encoded) hash of the TLS certificate your server presented, so you
  can confirm which certificate was actually seen.
- Every GET request made against the verification hash's URL, including from anyone other
  than this service, if someone else came looking.

If the service can't or won't perform the request — your target hasn't opted in, the URL is
invalid, or the fetch itself fails — an applicable HTTP error is returned instead.

## 🦔 Demo-Service-Ception!
You can point this endpoint at this service's own `/hello/` endpoint, letting the two demo
endpoints authenticate each other. It needs no opt-in file, so it's the easiest way to see a
full report without setting anything up first.

``` bash
curl -X POST "https://demo.hashback.dev/call/" \
    -H "Content-Type: text/plain" \
    --data "https://demo.hashback.dev/hello/"
```

``` PowerShell
Invoke-WebRequest -Uri "https://demo.hashback.dev/call/" -Method POST `
    -Headers @{ "Content-Type" = "text/plain" } `
    -Body "https://demo.hashback.dev/hello/"
```

## 🦉 More information
For full HashBack protocol details, see the
[HashBack documentation](https://github.com/billpg/HashBack/). File issues and feature
requests there.

Hashback demo service engineered by William Godfrey, [billpg industries](https://billpg.com/)
