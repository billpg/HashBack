# HashBack Hash Service

Welcome to the Hash Service!

This demo service hosts HashBack hashes so you can test clients that generate and verify HashBack packages. Do not use this demo service for anything important because anyone can use it.

## 🦔 Adding a hash (PUT)
1. Create your HashBack JSON and include a VerifyUrl in this form:
   `https://demo.hashback.dev/hash/{uuid}` — choose a fresh UUID for each upload.

2. Calculate the salted SHA-256 hash for your JSON package and base64-encode the resulting 256-bit (32-byte) hash. The service expects a base64-encoded 32-byte value.

3. Send a PUT request to your chosen verification URL with `Content-Type: text/plain` and the base64 value as the body.

Examples
``` bash 
curl -X PUT "https://demo.hashback.dev/hash/01234567-89ab-cdef-0123-456789abcdef" \
    -H "Content-Type: text/plain" \
    --data "YourSHA256HashResultGoesHere+++++++++++++++="
```

``` PowerShell
Invoke-WebRequest -Uri "https://demo.hashback.dev/hash/01234567-89ab-cdef-0123-456789abcdef"
    -Method PUT -Headers @{ "Content-Type" = "text/plain" } -Body "YourSHA256HashResultGoesHere+++++++++++++++="
```

Responses you may receive
- 200 OK — Hash stored, response body contains the stored base64 hash.
- 400 Bad Request — Request malformed (e.g. wrong size or not valid base64).
- 403 Forbidden — You are not allowed to use this service.
- 409 Conflict — An entry with that UUID already exists; pick a fresh UUID.
- 429 Too Many Requests — You have hit a rate limit.

## 👀 Retrieving a hash (GET)
After the PUT is accepted, you can GET the hash at the same URL for a short time and a limited number of retrievals.

Example
``` bash
curl "https://demo.hashback.dev/hash/01234567-89ab-cdef-0123-456789abcdef"
```
The GET response is `text/plain` and returns the stored base64-encoded 32-byte hash.

## 🦉 More information
For full HashBack details, see the HashBack documentation: [https://github.com/billpg/HashBack/]
File issues and feature requests there.

Hashback demo service engineered by William Godfrey, [billpg industries](https://billpg.com/)
