# HashBack Authentication: Trust, Verified.
HashBack is a two-step authentication exchange over HTTPS/TLS. Your identity is proven by publishing a hash, not sharing a secret. One Request. One Verification. Zero Secrets.

<img src="docs/assets/HashBack-Badge-Logo.png" align="right" alt="" width="150" height="150" />

This version of the document is a **public-draft** for review and discussion tagged as version **4.1**. I will update this number if I make any substantive updates. If you have any comments or notes, please open an issue on this project's public github.

This document is Copyright William Godfrey, 2025. You may use its contents under the terms of the Creative-Commons Attribution license.

## 🚠 The elevator pitch.
<table>
  <tr>
    <td><img src="docs/assets/PhoneCall-Frame-1.png" alt="Alice: Hi Bob. I'm Alice." width="200" height="200" /></td>
    <td><img src="docs/assets/PhoneCall-Frame-2.png" alt="Bob: Prove it." width="200" height="200" /></td>
    <td><img src="docs/assets/PhoneCall-Frame-3.png" alt="Alice: You know my number. Call me back." width="200" height="200" /></td>
    <td><img src="docs/assets/PhoneCall-Frame-4.png" alt="Bob: Hi Alice. Did you call me?" width="200" height="200" /></td></tr>
</table>

Did you notice what **didn't** happen? No-one needed a password, cryptographic tokens, or even recognizing each other's voice.

While a recipient of a call *can't* be certain who a caller is, the caller *can* be certain of who they are calling. By both parties calling each other, both can be reassured of each other's identity.

Now apply that thought to web authentication. The client can be sure (thanks to TLS) who the server is, but the server can't be sure who the client is, much like the analogy with phone calls. HashBack applies this same "call me back" reassurance to web APIs using two HTTPS transactions.

## &#x1F994; Meet Hashbert, the brainy hedgehog.
<p align="left">
  <img src="docs/assets/Hashbert-In-Laboratory.png" alt="Hashbert the brainy hedgehog" align="right" width="150" height="150" style="border-radius: 16px; box-shadow: 2px 2px 6px #888;">
</p>

He's the unofficial mascot of HashBack. Cautious, clever, and always prepared. He's a friendly chap, isn't he? Hashbert is here to help you understand HashBack Authentication.

> 🦔 *"Hello! I'm Hashbert. Bill thinks this is cute. I'm not so sure."*

## 🔨 What is the problem this is meant to fix?
If you're running a service out in the cloud which interacts with an external service, you probably have cryptographic keys or a password or token squirreled away somewhere. This is probably encrypted or stored in a purpose-built repository of secret keys and tokens. Either way, your code will need to unlock that material whenever it needs to interact with that external service.

This repository of secrets will need to be managed. The service won't be able to manage these things for itself because it'll need to identify itself to the service that issues these tokens, moving the problem one layer away without eliminating the problem itself. Either that or you make the decision that these secret tokens stay valid for long periods of time.

Repositories of secret tokens or keys. They have to be so secure that passersby can't access them, but so available that your code running in cloud can access them.

HashBack Authentication is an attempt to eliminate the need for long-term secret storage entirely.

> 🦔 *"That's a tricky balance to strike. If only there was a way to prove who you are without needing to store secrets."*

## 🤝 The Exchange
In a nutshell, a client proves their identity by publishing a short string on their TLS-secured website. The server downloads that string and thanks to TLS, is reassured that the client is indeed someone who is in control of that website.

To add a little more detail, the client builds a claim for authentication in the form of a JSON object. That object's bytes are themselves hashed and the hash result string is published on the client's website. To complete the loop, the server gets that string in its own separate HTTP/TLS transaction. Once the server can confirm that the hash published on the client's website matches its own calculated hash for the supplied JSON object's bytes, the server passes the request.

> 🦔 *"It's like we're calling each other to prove who we are."*

### "Isn't that like ACME?" (Let's Encrypt)
Yes, the exchange used by ACME has a lot in common with HashBack, especially the "call me back" verification step at its core.

HashBack and ACME have these significant differences:

| &nbsp;                                          | ACME               | HashBack           |
|------------------------------------------------:|--------------------|--------------------|
| Number of transactions needed to complete auth: | **3**              | **2**              |
|             General purpose API authentication: | :x:                | :heavy_check_mark: | 
|                Works without TLS already setup: | :heavy_check_mark: | :x:                |  
|                    Useful for establishing TLS: | :heavy_check_mark: | :x:                |

*HashBack is a general purpose authentication mechanism.* You could use HashBack for any API that needs caller authentication.    
*HashBack is simpler.* You can complete the exchange with two transactions - a request and response in each direction.

HashBack requires that both sides already have TLS established and configured before you even start. Without TLS on both sides, this mechanism is going to fail. It is thanks to Let's Encrypt and the ACME protocol making TLS ubiquitous that HashBack is even possible.

I am very much open to the next version of this draft exchange reusing parts of ACME. Especially if we can keep it to two transactions, or a security analysis reveals that we really do need that third transaction. 

> 🦔 *"It's like ACME, but fewer coyotes are maimed."*

### Ahead of time.
Before any exchange can occur, the client's administrator must declare the exact narrow URL range the client will use for publishing verification hashes. Ideally, this would be a single fixed URL with a single query string parameter as only variation allowed, or a folder without allowing further subfolders. This URL must use TLS via the HTTPS scheme.

This exchange relies on the server having a clear mapping of which URLs belong to which clients, so it is important the range is not too broad.

:heavy_check_mark: `https://example.com/hashback?id=*`   
:heavy_check_mark: `https://example.com/hashback/*.txt`    
:x: `http://example.com/hashback/` (no TLS)   
:x: `https://example.com/` (too broad)    
:x: `https://example.com/hashback/` (too broad if you allow subfolders)   
:x: `https://example.com/blog/` (may allow comments)   
:x: `https://example.com/wiki/` (may allow public edits)

> 🦔 *"Pick a URL and shake on it. I'd offer a paw, but I'm mostly spines."*

### The Authorization header
The header is constructed as follows:
```
Authorization: HashBack (BASE64 encoded JSON) 
```

The BASE64 encoded block must be a single string with no spaces or end-of-line characters and must include the trailing `=` characters per the rules of BASE64. (The examples in this document split the string into multiple lines for clarity only. The normal rules of HTTP prefer that headers arrive as a single line.) The bytes inside the BASE64 block are the UTF-8 representation of a JSON object.

#### JSON Properties
The JSON object is made from the following properties. All are required and the values are string type unless otherwise noted.

- **`Version`**
  - A string indicating the version of this exchange in use.
  - This version is indicated by the string `"BILLPG_DRAFT_4.1"`.
- **`Host`**
  - The full domain name of the server being called in this request.
  - Because load balancers and CDN systems might modify the `Host:` header, a copy is included here so there's no doubt exactly which string was used in the verification hash.
  - The recipient service must reject all requests that come with a name that belongs to someone else or generic names such as `localhost`, as this may be an attacker attempting to re-use a request that was made for a different server.
- **`Now`**
  - The current UTC time, expressed as an integer of the number of seconds since the start of 1970.
  - The recipient service should reject this request if the timestamp is too far from its current time. This document does not specify a threshold in either direction but instead this is left to the service's configuration.
  - The integer type should be greater than 32 bits to ensure this exchange will continue to work beyond the year 2038.
- **`Unus`**
  - 128 bits of cryptographic-quality randomness, encoded in BASE-64 including trailing `==`.
  - This is to make reversal of the verification hash practically impossible. The other JSON property values listed here are "predictable". The security of this exchange relies on this one value not being predictable.
  - The value must be unique for each request. Servers should reject any reused value within the allowed drift it places on the `Now` value.
  - I am English and I would prefer not to name this property using a particular five-letter word starting with N, as it has an unfortunate meaning in my culture.
- **`Verify`**
  - An `https://` URL belonging to the client where the verification hash may be retrieved with a GET request.
  - The URL must be one that server knows as belonging to a specific user. Exactly which URLs belong to which users is beyond the scope of this document.

If either or both of the two properties that include domain names (`Host` and `Verify`) uses IDN, those non-ASCII characters should be normalized and must be either UTF-8 encoded or use JSON's `\u` notation. Both properties must not use the `xn--` form.

For example:<!--1066_EXAMPLE_REQUEST-->
```
{
    "Version": "BILLPG_DRAFT_4.1",
    "Host": "server.example",
    "Now": 529297200,
    "Unus": "Rpgt4Fc5nMDq14LOps/hYQ==",
    "Verify": "https://client.example/api/hashback?id=502542886"
}
```
This JSON string is BASE64 encoded and added to the end of the `Authorization:` header.<!--1066_EXAMPLE_AUTH_HEADER-->
```
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v
 dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi
 aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=
```

> 🦔 *"Secure as a hedgehog in a sleeping bag."*

### Verification Hash Calculation and Publication
Once the client has built the request, it will need to find the JSON object's hash in order to publish it on their website. The server will also need to repeat this hashing process in order to verify the request is genuine.

The hashing process takes the following steps.
1. Join two byte blocks together:
   - The following 32 bytes of salt.<!--FIXED_SALT-->
     - ```
       113,218,98,9,6,165,151,157,
       46,28,229,16,66,91,91,72,
       150,246,69,83,216,235,21,239,
       162,229,139,163,6,73,175,201
       ```
   - The bytes that the went into the BASE64-encoded block used in the Authorization header.
2. Hash the combined block using a single round of SHA-256.
3. Encode the hash result using BASE-64, including the trailing `=` character.

Note that the hash is performed on the same bytes that were encoded inside the BASE64 block. Because of this, the JSON itself may be flexible with formatting whitespace or JSON character encoding, as long as the JSON object is valid according to the requirements of JSON itself and the rules stated above.

The salt ensures that HashBack verification hashes cannot be mistaken or misused in other contexts. Because these extra bytes are not sent over the wire with a request, there's no risk of a general purpose hashing service being misued to perform HashBack verification hash calculations. A valid hash is only meaningful in light of this document. (In case it isn't clear, the salt is fixed and public. It is not a secret.)

For your convenience, here is the 32 byte fixed salt block in a variety of encodings:
- Base64: `cdpiCQall50uHOUQQltbSJb2RVPY6xXvouWLowZJr8k=`<!--FIXED_SALT_B64-->
- Hex: `71DA620906A5979D2E1CE510425B5B4896F64553D8EB15EFA2E58BA30649AFC9`<!--FIXED_SALT_HEX-->
- URL: `q%dab%09%06%a5%97%9d.%1c%e5%10B%5b%5bH%96%f6ES%d8%eb%15%ef%a2%e5%8b%a3%06I%af%c9`<!--FIXED_SALT_URL-->

Once the Caller has calculated the verification hash for itself, it then publishes the hash under the URL listed in the JSON with the type `text/plain`. The returned string itself must be one line with the BASE-64 encoded hash in ASCII as that only line. It must either have no end-of-line sequence, or end with either a single CR, LF, or CRLF end-of-line sequence. The response must be `200 OK` and the TLS certificate must be valid.

The expected hash of the above example is: 
- `0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=`<!--1066_EXAMPLE_HASH-->

Once the service has downloaded that verification hash, it should compare it against the result of hashing the bytes inside the BASE64 block. If the two hashes match, the server may be reassured that the client is indeed the user identified by the URL from where the hash was downloaded and proceed to process the remainder of the request.

If there is any problem with the authentication process, including errors downloading the verification hash or that the supplied hash doesn't match the expected hash, the server must respond with an applicable error response. This should include sufficient detail to assist a reasonably experienced developer to fix the issue in question.

#### Generation of the fixed salt block
The salt string itself was generated by a PBKDF2 call with a high iteration count. For reference, the following parameters were used:
- Password: "To my Treacle." (14 bytes, summing to 1239.)<!--FIXED_SALT_PASSWORD-->
- Salt: "I love you to the moon and back." (32 bytes, summing to 2827.)<!--FIXED_SALT_DEDICATION-->
- Hash Algorithm: SHA512
- Iterations: 477708<!--FIXED_SALT_ITERATIONS-->
- Output: 256 bits / 32 bytes

> 🦔 *"That Bill sure loves his treacle."*

## 💂‍ 401 responses and the WWW-Authenticate header
HTTP Authentication is typically triggered by the client first attempting to perform a particular transaction without any authentication, but for the response to reject that attempt with a `401` response and a `WWW-Authenticate` header that lists the many available authentication methods the client could use. (Or many such headers, each one listing an available method.)

For a server to respond when HashBack authentication is available, the `WWW-Authenticate` header must include an `<auth-scheme>` of `HashBack`. A `realm` parameter may be present but this is optional.

For example:
```
HTTP/1.1 401 Authentication Required
WWW-Authenticate: HashBack realm="My_Wonderful_Realm"
```

Clients may skip that initial transaction if it is already known that the server supports HashBack authentication.

> 🦔 *"If you see a 401, don't panic. Even I get those before breakfast."*

## 🍪 "Do we need to perform this exchange for every API request?"
Yes, but also, No.

Yes, each time you make an API request authenticated by HashBack, you need to make a new header and arrange for the new verification hash to be made available. That is an expensive operation and there's no shortcut. Every single time you want to make an API request with HashBack, you need to start entire process over. Even if you have a thousand requests to make.

But because it is expensive, maybe plan for the response to the first successful response to include a `Set-Cookie` header or some form of bearer token. Once the caller has one of those, they can use the cookie for (say) ten minutes until it expires. At that point, perform that hashBack exchange again for a fresh cookie.

I've avoided specifying that mechanism in this document to keep it focused to the main idea. For earlier drafts, supplying a bearer token was the *only* thing you could do with HashBack, thinking that no-one would ever want to use it in any other mode. I changed my mind when I relaised that someone wanting to make a single request every hour would prefer to skip the bearer token step.

If you are developing the receiving end of a HashBack request, please add a `Set-Cookie` to the response that the caller can use for a little while. If you're developing the requesting end, please have your code check the response for that cookie and use it next time. Or some other mechanism. 

> 🦔 *"Cookies are tasty."*

## 💼 Case Study
**The Rutabaga Republic** is a large agricultural concern that grows and sells rutabagas and other root vegetables. They have a secure API at `RutabagaRepublic.example` for their regular customers use to place orders directly from their own systems.

One such customer is Petunia Parsnip, founder of **The Underground Supper Club**, a high-end vegan patisserie that specializes in root-vegetable-themed banquets. Her clients expect nothing less than the finest rutabaga souffles and parsnip pavlovas, delivered with flair and precision.

Petunia has recently signed up with The Rutabaga Republic and logged into their customer portal. On her authentication page under the *HashBack Authentication* section, she's configured her account affirming that `https://petunia.example/hashback` is under her sole control and where her verification hashes will be made available.

> 🦔 *"All the world's a rutabaga."*

### Making the request.
Petunia needs to place a large rutabaga order for an upcoming "Turnip the Volume" gala. Her stock management system constructs the following JSON payload:<!--CASE_STUDY_REQUEST-->
```
{
    "Version": "BILLPG_DRAFT_4.1",
    "Host": "RutabagaRepublic.example",
    "Now": 682718520,
    "Unus": "sGhK1rIbEWjW6Sg25s+KPg==",
    "Verify": "https://Petunia.example/api/hashback?id=901983180"
}
```

Note the `Verify` property corresponds to the URL Petunia had registered ahead of time. This is where the trust boundary lies.

The system calculates the verification hash from this JSON object (`GcBDESw5S+0HjSEY/ia6VQ7NyQvjHvy9Yk/lyQO0bQs=`) and publishes it on their server at the specified URL, ready for retrieval.<!--CASE_STUDY_HASH-->

To complete the request, an `Authorization` header is constructed by encoding the JSON with BASE64. The complete request is as follows.<!--CASE_STUDY_AUTH_HEADER-->
```
POST /api/order HTTP/1.1
Host: RutabagaRepublic.example
User-Agent: Petunia's Wonderful Stock Management System.
Authorization: HashBack
 eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJSdXRhYmFnYVJlcHVibGljLmV4
 YW1wbGUiLCJOb3ciOjY4MjcxODUyMCwiVW51cyI6InNHaEsxckliRVdqVzZTZzI1cytLUGc9PSIs
 IlZlcmlmeSI6Imh0dHBzOi8vUGV0dW5pYS5leGFtcGxlL2FwaS9oYXNoYmFjaz9pZD05MDE5ODMx
 ODAifQ==
Accept: application/json
Content-Type: application/json

{ "Product": "Rutabagas!", "Quality": "Tasty!", "Quantity": "Lots!" }
```

> 🦔 *"You can tell someone highly skilled in API design wrote that example JSON."*

### Checking the request
The Rutabaga Republic website receives this request and validates it, performing the following checks:
- :heavy_check_mark: The request arrived via HTTPS.  
- :heavy_check_mark: The `Authorization` header is `HashBack` type.
- :heavy_check_mark: The `Host` value is a domain it owns - `RutabagaRepublic.example`.
- :heavy_check_mark: The `Now` time-stamp is reasonably close to the server's internal clock.
- :heavy_check_mark: The `Verify` value is an HTTPS URL belonging to a known user - *Petunia*.

The service has passed the request for basic validity, but it still doesn't know if the request has genuinely come from Petunia's service or not. To perform this step, it proceeds to check the verification hash.

> 🦔 *"All checks so far are good. Now to see if Petunia really is Petunia."*

### Retrieval of the verification hash
Having the URL to get the client's verification hash, the service performs a GET request for that URL. As part of the request, it makes the following checks:
- :heavy_check_mark: The TLS certificate is valid.
- :heavy_check_mark: The response is `text/plain`.
- :heavy_check_mark: The response body is 256 bits in BASE-64.

Having successfully retrieved a verification hash, it must now find the expected hash to check it is genuine.

> 🦔 *"Fetching the hash from Petunia's site."*

### Checking the verification hash
The service performs the same hashing operation on the block of BASE64-encoded bytes request that the caller performed earlier. If they match, the request is authenticated and may continue processing it, reassured that the client is actually Petunia. <!--CASE_STUDY_SET_COOKIE-->

```
HTTP/1.1 200 OK
Set-Cookie: RutabagaAuth=jTqkkDGt.IGu55JOH.cGlsgwiC;
  Domain=RutabagaRepublic.example;
  Expires=Tue, 20 Aug 1991 22:18:41 GMT;
  Secure; HttpOnly; SameSite=Strict
Content-Type: application/json

{
   "OrderID": "12",
   "ExpectedDelivery": "Tomorrow"
}
```

> 🦔 *"Success! Petunia is who she says she is."*

### Outcome
Petunia's rutabagas are on their way. Rutabaga Republic is confident the request came from a verified source - no secrets, no tokens, no passwords. The entire exchange completed without either side having to manage any long-term secrets.

> 🦔 *"I wagged a spine in approval."*

## ❓ Answers to Anticipated Questions

### What's wrong with keeping a pre-shared secret long term?
They require management and secure storage. Your server-side code will need a way to access them without access to your master passwords or MFA codes. There are solutions for secure password storage that your unattended service code can use but they still need to be managed while this exchange utilizes TLS (which both sides will have already made an investment in) to secure the exchange.

### I don't have a web server.
Then this exchange is not for you. It works by having two web servers make requests to each other.

### I have a web server on the other side of the Internet but not the same machine.
Your web site needs to be covered by TLS, and for your code to be able to publish a small static file to a folder on it. If you can be reasonably certain that no-one else can publish files on that folder, it'll be suitable for this exchange.

### What range of verification hash URLs should be allowed for identifying a single user?
The server should allow only a **controlled, user-specific range** of URLs - wide enough to support dynamic hash publication, but narrow enough to prevent impersonation.

Suppose a user's website also hosts a blog with comments or a wiki, an attacker wishing to impersonate that user could publish a verification hash in a comment or wiki page. The server should not allow this.

This is why the user must, ahead of time, affirm to the server exactly which URLs belong to them and are suitable for this exchange. The server must only accept verification hashes from those URLs. Treat the URL as a scoped trust boundary.

### TLS supports client-side certificates.
To use client-side certificates, the client side would need access to a private key. This would need secure storage for the key which the caller code has access to. Avoidance of this is the main motivation of this exchange.

### What if an attacker attempts to eavesdrop on either request?"
The attacker can't eavesdrop because TLS is securing the channel.

### What if either HTTP transaction uses a self-signed TLS certificate or one signed by an untrusted root?
If a connection to an untrusted TLS certificate is found, abandon the request and maybe log an error. Fortunately, this is the default of most (all?) HTTP client libraries.

If you want to allow for self-signed TLS certificates, since this exchange relies on a pre-existing relationship, you could perhaps allow for "pinned" TLS certificates to be configured.

### What if an attacker has a TLS certificate signed by a trusted CA?
Then the attacker has broken TLS itself and we have bigger problems.

If this is a serious concern, you could keep your own collection of trusted TLS certificates and refuse to recognize any TLS certificates not on your list. You'd effectively be running your own CA if you can't trust the ones built into your HTTP library.

### What if an attacker sends a fake Authorization header?
The recipient will attempt to retrieve a verification hash file from the real client's website. As there won't be a verification hash that matches the fake header, the attempt will fail.

### What if an attacker can predict the verification hash URL or has a verification hash intended for another server?"
Let them.

Suppose an attacker knows a current request's verification hash URL. They would be able to make that GET request and from that know the verification hash. Additionally, they could construct their own Authorization header to a genuine server, using the known `Verify` value with knowledge the genuine client's website will respond again to a second GET request with the same known verification hash.

To successfully perform this attack, the attacker will need to construct the JSON block such that its hash will match the verification hash, or else the server will reject the request. This will require finding the value of the `Unus` property which is unpredictable because it was generated from cryptographic-quality randomness, sent over a TLS protected channel to the genuine server, and is never reused. 

For an attacker to exploit knowing a current verification hash, they would need to be able to reverse that hash back into the original JSON request, including the unpredictable `Unus` property. Reversing SHA-256 is considered practically impossible.

Nonetheless, it is trivial to make the verification hash URL unpredictable by using cryptographic-quality randomness and it may be considered prudent to do so. (Note to anyone performing a security analysis, please assume the URL *is* predictable and thus the verification hash may be exposed to attackers.)

### Does it matter if any part of the Authorization header is predictable?
Only the value of the `Unus` property needs to be unpredictable. All of the other values may be completely predictable to an attacker because only one unpredictable element is enough to make the verification hash secure.

### What if a client sends a legitimate Authorization header to a server, but that server is evil and it copies that request along to a different server?
The second server will reject the request because they will observe the `Host` property of the request is for the first server, not itself. For this reason it is important that servers reject all requests with a `Host` value other than the domain belonging to them, including "localhost" and similar.

### What if an attacker floods the POST request URL with many fake requests?
Any number of fake requests will all be rejected by the server because the real user is not publishing hashes that match these fake requests.

Despite this, the fact that a request for authentication will trigger a second GET request might be used as a denial-of-service attack. For this reason, it may be prudent for a server to track IP address blocks with a history of making bad authentication requests and rejecting subsequent requests that originate from these blocks, or even requiring that clients be at a pre-agreed range of IPs and rejecting anyone outside this range. (Note that I suggest this only as a means to prevent abuse. The security of the authentication method is not dependent on any IP block analysis.)

### What if there's a website that will host files from anyone?
Maybe don't claim that website as one that you have exclusive control over.

At its core, you pass authentication by being someone who was able to demonstrate control of a particular URL. If the group of people who have that control is "anyone" then that's who can pass authentication.

### What if a malicious Caller supplies a verification URL that keeps the request open?
[I am grateful to "buzer" of Hacker News for asking this question.](https://news.ycombinator.com/item?id=38110536)

Suppose an attacker sets themselves up and configures their website to host verification hash files. However, instead of responding with verification hashes, this website keeps the GET request open and never closes it. As a result, the server is left holding two TCP connections open - the original authentication request and the GET request that won't end. If this happens many times it could cause a denial-of-service by the many opened connections being kept alive.

We're used to web services making calls to databases or file systems and waiting for those external systems to respond before responding to its own received request. The difference in this scenario is that the external system we're waiting for is controlled by someone else who may be hostile.

This can be mitigated by the server configuring a low timeout for the request that fetches the verification hash. The allowed time only needs to be long enough to perform the hash and the usual roundtrip overhead of a request. If the verification hash request takes too long the overall transaction can be abandoned.

Nonetheless, I have a separate proposal that will allow for the POST request to use a 202 "Accepted" response where the underlying connection can be closed and reopened later. Instead of keeping the POST request open, the Issuer can close the request and the Caller may reopen it at a later time.

### Why use a hash at all?
(I am grateful to m'colleague Rob Armitage for asking this question.)

You might ask, "Why not publish a random string and include that string directly in the request? Why bother with hashing at all?"

Because without a hash, a malicious server could forward your request to a third party and falsely claim it came from you.

It is necessary for the file retrieved from the client's website to be a hash (instead of a random string) to prevent an attack when a valid authorization request is fraudulently passed along to a third party. For example:

1. Server A sends: "I am server A. To prove it, I have placed "ABC" at (A's url)."    
2. Server B forwards that exact claim to Server C: "I am server A. To prove it, I have placed "ABC" at (A's url)."    
3. Server C fetches the URL from A and sees "ABC", mistakenly believing it is talking to A.

From server A's perspective, everything looks fine - it received a single GET request and returned the expected string, but Server B just impersonated Server A!

By requiring the published hash to be a *hash of the actual request*, this kind of "pass-along" attack is prevented. The hash ties the published value to the specific request being made - including the intended recipient (`Host`). If any part of the request is changed or reused, the hash won't match and the authentication fails.

In short: The hash makes the proof specific, unforeseeable, and bound to the request.

### Why BASE64 the JSON in the `Authorization` header?
To ensure there's an unambiguous sequence of bytes to feed into the hash. By transferring the JSON block in an encoded set of bytes, the recipient can simply pass the decoded byte array (with salt prepended) into the SHA-256 function.

### Shouldn't you have a server challenge like ACME?
This is something I'd like an expert to confirm, but I don't think we need one. The request is sent over TLS, which prevents an attacker seeing the request itself and also replaying it. The `Host` header prevents "passing along" attacks as described above.

If I am ever persuaded that a server challenge is needed, I'd make it a parameter to the `WWW-Authenticate: HashBack` header with the 401 response. The value of this parameter would then need to be included in the JSON that builds the `Authorization` header. The server would check this value is one it created and reject it if it isn't.

### Can I send the same Authorization header with each request?
Short version: No.    
Slightly longer version: Please don't.

Each HashBack request includes a timestamp (`Now`) and a cryptographic-quality random value (`Unus`). The `Now` value ensures the request is fresh and the `Unus` value ensures the request can't be predicted or replayed. Reusing either defeats the purpose of this exchange and opens the door to replay attacks. You'd only be making yourself less secure.

Instead, generate a fresh header for each request. It's lightweight enough and it's exactly what Hashbert would do.

### What are the previous public drafts?
- [Public Draft 1](https://github.com/billpg/HashBack/blob/22c67ba14d1a2b38c2a8daf1551f065b077bfbb0/README.md)
  - Initial published revision, then named "Cross Request Token Exchange".
  - Used two POST requests in opposite directions, with the second POST request acting as the response to the first.
- [Public Draft 2](https://github.com/billpg/HashBack/blob/2165a661e093754e038620d3b2be1caeacb9eba0/README.md)
  - Updated to allow a 202 "Accepted" response to the first POST request, avoiding the need to keep the connection open.
  - I had a change of heart to this approach shortly after publishing it.
- [Public Draft 3.0](https://github.com/billpg/HashBack/blob/bf7e2ff1876e9673b04bffb7d70766a10d326976/README.md)
  - Substantial refactoring. The client makes a POST request with a JSON body and puts a hash of that JSON body on their website. The server fetches that hash and compares it to their own expected hash. The POST response is always a Bearer token.
  - Added a "dot zero" to allow for minor updates, reserving 4.0 for another substantial refactor.
  - Changed name to "HashBack" Authentication, as a play on "Call Back".
- [Public Draft 3.1](https://github.com/billpg/HashBack/blob/d8886ce0cebb159f6484186f5b6ccd750d0dd97c/README.md)
  - The "fixed salt" is now the result of running PBKDF2 but without processing the result into capital letters. This means I no longer need to link to some "attached" C# code and can simply record the input parameters. (The original motivation of having only capital letters in the salt was to support implementations that only accept ASCII strings, but all implementations I could find will accept arbitrary blocks of bytes as input.)
  - Added "204SetCookie" as a third response type. Might be useful for a browser making the POST request.
- [Public Draft 4.0](https://github.com/billpg/HashBack/blob/5cef44b500f6885202d24eda51aa81fe865b8495/README.md)
  - Another substantial refactoring. The JSON request is now sent by the client in the form of an HTTP `Authorization` header.
  - The transaction being authenticated could be anything, including a request for a Bearer token, but not just that. This has the advantage of allowing a once-off request to skip the extra transaction to fetch a Bearer token and act more like traditional HTTP authentication. Also, as this header payload is BASE64 encoded, we don't need to canonicalize the JSON as the hash can be done on the BASE64 encoded bytes.
 - Public Draft 4.1 (This Document)
   - Replaced PBKDF2 with a single round of salted SHA-256 for the verification hash. I'm happy the extended hashing isn't needed.
   - Removed the mechanism to retrieve a temporal bearer token to simplify the document.

## 📘 Glossary
"HashBack": The name of this exchange, a play on "Call Back".

"Unus": A 128-bit cryptographic-quality random value, encoded in BASE-64. Equivalent to a cryptographic "nonce", but renamed for cultural sensitivity and clarity. The word is Latin for "one" or "single". I remain hopeful this word becomes adopted by the wider cryptographic community.

"Authorization header": The standard HTTP header used by the client to pass the JSON object to the server.

"Verification hash": The BASE-64 encoded salted SHA-256 hash of the JSON object bytes.

"Verify URL": The HTTPS URL where the client publishes the verification hash, which must be under that client's control.

"Host": The domain name of the server being called, included in the JSON object to prevent relay attacks.

"Now": The current UTC time (seconds since 1970) used to validate the freshness of the request.

"Fixed Salt": A 32-byte fixed value prepended to the payload before hashing, ensuring the hash is unique to HashBack. This is fixed for all requests and is not secret.

"TLS": Transport Layer Security, the protocol used to secure HTTP connections.

"WWW-Authenticate header": The standard HTTP header used by a server to advertise the authentication methods it supports.

## 🥾 Next Steps
This document is a public draft version. I'm looking (please) for clever people to review it and give feedback. In particular, I'd like some confirmation I'm using SHA-256 with its fixed salt correctly. I know not to "roll your own crypto" and this is very much using pre-existing components. Almost all the security is done by TLS and the hash is there to confirm the authenticity of the authentication request. If you have any comments or notes, please raise an issue on this project's github.

In due course, I plan to deploy a publicly accessible test API which you could use as the other side of the exchange. It'd perform both the role of an authenticating server by downloading your hashes and validating them, as well as perform the role of a client requesting authentication from you and publishing a verification hash for you to download. (And yes, you could point both APIs at each other, just for laughs.)

Ultimately, I hope to publish this as an RFC and establish it as a public standard.

<a href="billpg.HashBackCore/"`HashBackCore`</a> is my reference implementation, handling the process for both validating a header and generating one. This is written with hooks for you to supply your own code when needed, including for registering your own verification hashes and retrieving a client's verification hash. (See that project's README file for usage notes.) It deliberately doesn't interface with HTTP, leaving that to your handler code. By calling to handler code, it allows an extensive set of unit tests that bypass that complication. These are implemented in the <a href="HashBackCoreTests">`HashBackCoreTests`</a> libary.

> 🦔 *"Onward, brave hedgehog!"*

## 🙏 Acknowledgements

My thanks to Danny Wilson for his feedback and for developing his own service that performs this authentication. Multiple independent implementations are good for establishing a new standard. 

My thanks to Ollie Hayman for bringing ACME to my attention.

> 🦔 *"I helped too!"*

Thanks to Microsoft Copilot for taking a break from its plans for world domination and destroying all humanity just long enough to review my drafts, suggesting improvements and helping Hashbert sleep better at night.

Thank you to my wife for her love and support while I developed this idea. I couldn't have done this without you.

Regards, Bill. <a href="https://billpg.com/">billpg.com</a> 🦉
