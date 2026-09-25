---
title: HashBack | Demo Service
next: help.html
next-text: Next: Can you help?
---
# Try the Demo Service

Experience HashBack authentication in action. Use our demo with your own code to see how
the exchange works. We'll even host your verification hashes while you're testing.

## Give Me Permission First!

<div class="feature-item">
<div><strong><code><a href="https://demo.hashback.dev/permit/">https://demo.hashback.dev/permit/</a></code></strong></div>
This page shows you how to grant permission to this demo service to access your website.
Publish a small JSON file in your <code>.well-known</code> folder and we'll read it before
we start sending masses of GET requests to your server.
</div>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “Even a hedgehog knocks before wandering into someone's garden. Publish your permission file, and so will we.”</p>

## Test Your Client Code

<div class="feature-item">
<div><strong><code><a href="https://demo.hashback.dev/hello/">https://demo.hashback.dev/hello/</a></code></strong></div>
Send a GET request to this URL and it'll respond with a cheery "Hello" message, but only
if a valid HashBack authentication header is included in the request. (If you don't, it'll
return full documentation for this service including some sample code you can copy.) Use
this to test your client code will generate a valid claim and will publish valid hashes.
</div>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “Is it me you're looking for?”</p>

## Test Your Service Code

<div class="feature-item">
<div><strong><code><a href="https://demo.hashback.dev/call/">https://demo.hashback.dev/call/</a></code></strong></div>
Send a POST request, including the URL of <i>your</i> service in the request body, and
this demo service will make properly-formed HashBack authenticated GET request to that
URL. Once completed, the demo service will return a full log of the request and response,
including the requests when anyone tried to GET the verification hash.
</div>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “You can point the "call" handler at the "hello" handler if you want. It's like shaking hands with yourself.”</p>

## We'll host your hashes!

<div class="feature-item">
<div><strong><code><a href="https://demo.hashback.dev/hash/">https://demo.hashback.dev/hash/</a></code></strong></div>
If you're developing your own HashBack client but you don't have a web server ready to
host your verification hashes yet, we've got your back. You can upload your hash text on
the demo server, ready for anyone to request it. For a minute or so. And it'll be deleted
after three GETs.
</div>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “Don't use that for anything important. Anyone can claim to be the demo service. Even badgers!”</p>

## Try it now!

<div><a class="btn btn-primary btn-big-center" href="https://demo.hashback.dev/" target="_blank">Demo.HashBack.dev</a></div>
