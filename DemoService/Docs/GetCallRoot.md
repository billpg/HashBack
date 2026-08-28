# HashBack Call Demo Service

This is a simple service that will call **your** URL with a valid HashBack header, including a varification hash published
in the right place ready for you to read. The response to the POST will be a log of call, including any attempts to get the
verification hash.

## Making a request
The request body should be a `text/plain` string of the URL you want this service to call.

The service will make a POST request to that URL with a `HashBack` header containing the following items:
- A `Version` string of `BILLPG_DRAFT_4.2`.
- A `Host` string matching the host of the URL you provided.
- A `Now` from the server's system clock.
- A `Unus` string from randomness to make the hash unique.
- A `VerifyUrl` string that is a URL to this service where you can retrieve the hash.

The response (if a 200) will be a text/plain log of the call, including any attempts to retrieve the hash from the `VerifyUrl`.
The log will include the `VerifyUrl`, the base64-encoded hash, and the SHA-256 (base64-encoded) hash of the TLS certificate
your server presented, so you can confirm which certificate was actually seen.

If the service can't or won't perform the request, an applicable HTTP error code will be returned.

## Demo-Service-Ception!

You can call this demo service and have it call this service's own `/hello/` service. Pointing the two services at each other.

Here's an example of how to do that:
``` PowerShell
Invoke-WebRequest -Uri "https://demo.hashback.dev/call/" -Method POST
    -Headers @{ "Content-Type" = "text/plain" } 
    -Body "https://demo.hashback.dev/hello/"
```
