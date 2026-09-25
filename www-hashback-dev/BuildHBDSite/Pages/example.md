---
title: HashBack | An Example Session
next: acme.html
next-text: Next: Wait! Isn't that ACME?
---
# An example HashBack session

Let's follow one request from the client's first thought to the server's final nod.
*Hashbert* the Hedgehog will annotate the important bits, because authentication is
easier to understand when someone points at the spiky details.

::: {.step}
## Declare the range of URLs you control

**Ahead of time**, the client administrator registers the exact narrow HTTPS URL or folder
where verification hashes will be published. The service uses this declaration to map the
URL back to the client and must reject URLs outside it. Avoid areas where the public might
be able to inject text such as comment forms.

```
My verification hashes will be published at:
    https://client.example/api/hashback?id=*
```

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “Choose a small patch of ground that you control. A single doorway is splendid, but handing over the whole house is a bit much, even for a hedgehog with a clipboard.”</p>
:::

::: {.step}
## Write the authentication request JSON

When you want to make **an authenticated request**, write a fresh JSON claim. This package
of data will combine the version, destination host, current UTC time, a unique random
value, and the URL where you're going to publish the hash of this JSON later.

```json
{
    "Version": "BILLPG_DRAFT_4.2",
    "Host": "server.example",
    "Now": 529297200,
    "Unus": "Rpgt4Fc5nMDq14LOps/hYQ==",
    "Verify": "https://client.example/api/hashback?id=502542886"
}
```

The **`Unus`** value prevents anyone from reusing your verification hash to impersonate
you. Everything else in the JSON is predictable, but this field is fresh, random and known
only to you and the server you’re talking to.

Cryptographers would call it a *“nonce”*, but in England that word has… other
connotations. I’m hoping the community will adopt this alternative name instead.
(**Unus** is Latin for *“Once”*.)

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “This claim is fresh, specific, and meant for one destination. The <code>Unus</code> value must be unpredictable and unique, so a stale claim cannot simply wander back in wearing a fake moustache.”</p>
:::

::: {.step}
## Hash the JSON and publish the result

Run a **salted SHA-256 hash over your JSON** and base-64 the result. Publish that string
as a one-line text file at the URL you listed in your JSON earlier.

```bash
$ cat hashback-salt.bin auth.json \
    | sha256sum \
    | cut -d ' ' -f1 \
    | xxd -r -p \
    | base64 > verification-hash.txt
$ scp verification-hash.txt user@host:/user/client.example/data/hashback/502542886.txt
```

The salt is fixed and exists to make sure that the hashes are only useful for this
exchange. Because the salt is not sent over the wire, we avoid the possibility of abusing
public hashing services.

The salt itself, along with how it was derived, is on the GitHub site.

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “The hash sits there, waiting to be retrieved, ready to exclaim <b>"That Was Me!"</b>.”</p>
:::

::: {.step}
## Make the HTTP request

Encode the bytes of your JSON with base-64 and **add it to your request's headers** as an
`Authorization: Hashback` header.

(You'd put the entire base-64 block as a single line without spaces. We've split it up on
this example for clarity.)

```
POST /api/order HTTP/1.1
Host: server.example
Accept: application/json
Authorization: HashBack eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMi
                        IsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v
                        dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYz
                        VuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi
                        aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaG
                        FzaGJhY2s/aWQ9NTAyNTQyODg2In0=
```

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “The header carries the claim. The verification hash confirms the claim is real.”</p>
:::

::: {.step}
## The server verifies the hash

Now the server has your JSON, they can now **check everything is right**.

<table class="checklist" border="0">
<tr><td>&#160;✅&#160;</td><td>The JSON is valid.</td></tr>
<tr><td>&#160;✅&#160;</td><td>The <code>Host</code> is correct.</td></tr>
<tr><td>&#160;✅&#160;</td><td>The <code>Now</code> timestamp is recent.</td></tr>
<tr><td>&#160;✅&#160;</td><td>The <code>Verify</code> URL belongs to a known user. (You.)</td></tr>
<tr><td>&#160;✅&#160;</td><td>The hash was retrieved over TLS with a known CA.</td></tr>
</table>

With the supplied verification hash in hand, the server may now repeat the salted SHA-256
and check it matches the expected verification hash. If they match, it must have come from
you!

<div class="flow">
<div><strong>1. Decode</strong> Read the JSON claim from the header.</div>
<div><strong>2. Recalculate</strong> Hash the claim with the fixed salt.</div>
<div><strong>3. Compare</strong> Accept an exact match.</div>
</div>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “A matching result confirms control of the registered website. The server has not been handed a long-lived secret but fresh proof from the right location. That is such a satisfactory conclusion it makes my spines wiggle.”</p>
:::
