# HashBack "Hello" Service

Send a `GET` request to this endpoint with a valid HashBack `Authorization` header and it will
say hello back. Use it to test that your client code builds a correct claim and publishes a
correct verification hash, without needing a real API to call.

## 🙋 What you get back
- **200 OK** — a `text/plain` body: `Hello {your-host}! (HashBack validated.)`
- **401 Unauthorized** — this page, plus a `WWW-Authenticate` header describing what this
  service accepts.

## 🔑 Making an authenticated request
Build a JSON claim in this exact key order:

``` json
{
    "Version": "BILLPG_DRAFT_4.2",
    "Host": "demo.hashback.dev",
    "Now": 529297200,
    "Unus": "Rpgt4Fc5nMDq14LOps/hYQ==",
    "Verify": "https://your-domain.example/hashback/some-fresh-id"
}
```

- `Host` must be `demo.hashback.dev` — this service will reject a claim addressed elsewhere.
- `Now` is the current UTC time as Unix seconds. It must be close to this server's own clock.
- `Unus` is a fresh, random, base64-encoded value, unique to this one request.
- `Verify` is the URL where this service can fetch your verification hash back. See
  "Publishing your verification hash" below for where that's allowed to be.

Hash the raw UTF-8 bytes of that JSON with the salted SHA-256 scheme described in the
[HashBack documentation](https://github.com/billpg/HashBack/), base64-encode the result, and
publish it at the `Verify` URL. Then send the base64-encoded JSON bytes as the `Authorization`
header:

``` bash
curl "https://demo.hashback.dev/hello/" \
    -H "Authorization: HashBack eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMiIsIkhvc3Qi..."
```

Both `BILLPG_DRAFT_4.2` and the older `BILLPG_DRAFT_4.1` are accepted.

## 🌍 Publishing your verification hash
The `Verify` URL has to be somewhere this service is actually allowed to fetch from. You have
two options:

1. **Use this service's own hash store** at `/hash/` as your `Verify` URL. No opt-in needed —
   see the [hash service's own documentation](/hash) for how to PUT a hash there first.
2. **Host it yourself**, on a domain you control. Since this service will make an outbound GET
   to fetch it, your domain needs to have first opted in — see [/permit](/permit) for how to
   publish that consent.

Skip either of those and the fetch will fail, which surfaces here as a failed authentication
rather than a hello.

## 🍪 The cookie shortcut
Once you've authenticated successfully, this service sets a `HashBackDemoService` cookie
(valid for 30 minutes) so repeated test calls don't need to repeat the full claim/hash/fetch
dance every time. Send the cookie back and you'll get the same hello message without a fresh
`Authorization` header.

## Example code
See the project's GitHub [Example](https://github.com/billpg/HashBack/tree/main/examples) section
for sample code. 

## 🦉 More information
For full HashBack protocol details, see the
[HashBack documentation](https://github.com/billpg/HashBack/). File issues and feature
requests there.

Hashback demo service engineered by William Godfrey, [billpg industries](https://billpg.com/)
