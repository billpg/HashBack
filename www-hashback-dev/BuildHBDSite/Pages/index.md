---
title: HashBack | Server-to-Server authentication without secret storage
next: example.html
next-text: Next: Walking through an example session
---
# HashBack

Cloud servers shouldn’t need to hold onto long‑lived secrets like passwords, API tokens or
private keys. **HashBack** removes that burden. Rather than storing credentials, your
service can rely on the TLS keys it already uses every day. With two short HTTPS
transactions — one to declare who you are, and one to prove it — both sides gain
confidence without ever sharing or keeping a secret. It’s simple, tidy and built on the
infrastructure you already trust.

<section class="cards">
<article class="card">

### Zero secret storage

Remove the need to keep and manage cryptographic keys or bearer tokens around for long
periods of time.

You've already invested in TLS. **Use it!**

</article>
<article class="card">

### Two HTTPS transactions

HashBack keeps the exchange short and friendly.

One call out, one call back.

</article>
<article class="card">

### Server-to-Server

Use it for general-purpose authentication between internet-facing services.

</article>
</section>

## From a simple analogy to a practical authentication mechanism

HashBack takes inspiration from a phone-call analogy. The caller knows who they are
calling, but the recipient doesn't know who that call is coming from. <small>("1471"?
"Star-69"? What's that?)</small>

But what if the recipient can call that original caller back! Now they can be reassured
that the caller actually was who they say they were.

<div class="gallery">
<img src="PhoneCall-Frame-1.png" alt="Hi Bob. I'm Alice!" />
<img src="PhoneCall-Frame-2.png" alt="Prove it." />
<img src="PhoneCall-Frame-3.png" alt="You know my number. Call me back." />
<img src="PhoneCall-Frame-4.png" alt="Hi Alice. Did you call me just now?" />
</div>

Did you see what **didn't** happen? **No-one** needed a cryptographic key or secret token.

Now apply that idea to web authentication. The client knows, thanks to TLS, who they are
connecting to, but the server doesn't know who that incoming connection is from. If the
server can call the client back, this time the server knows, thanks again to TLS, that the
client is who they say they are. The two connections in opposite directions complete the
loop.
