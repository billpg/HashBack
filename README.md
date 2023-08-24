# Cross Request Token Exchange
An authentication exchange between two web services.

This document is Copyright William Godfrey, 2023. It may be freely copied under the terms of the  Creative Commons Attribution-NoDerivs license.
https://creativecommons.org/licenses/by-nd/3.0/

## The elevator pitch.

Alice and Bob are normal web servers with an API.
- (Alice opens an HTTPs request to Bob.)
- "Hey Bob. I want to use your API but I need a Bearer token."
  - (Bob opens a separate HTTPS request to Alice.)
  - "Hey Alice, have this Bearer token."
  - "Thanks Bob."
- "Thanks Alice."

It's not so much what happened but what didn't happen. Neither side needed a pre-shared key or shared secret. Neither side need a secure secret store. Both machines (Alice and Bob) in this example are web servers. They both have TLS keys signed by a mutally trusted CA enables the exchange to work.

When you make an HTTPS request, thanks to TLS you can be sure who you are connecting to, but the service receiving the requst can't be sure who the request is coming from. By using *two* HTTPS requests in opposite directions, two web servers may perform a brief handshake and exchange a *Bearer* token. All without needing any pre-shared secrets. They already have TLS configured and it protects that exhange for free.

### What's a Bearer token?

A Bearer token is string of characters. It might be a signed JWT or it might be a short string of random characters. If you know what that string is, you can include it any web request where you want to show who you are. The token itself is generated (or "issued") by the service that will accept that token later on as proof that you are who you say you are, because no-one else would have the token. 

```
POST /api/some/secure/api/
Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJuZ2d5dSI6Imh0dHBzOi8vYmlsbHBnLmNvbS9qMS8ifQ.nggyu
{ "Stuff": "Nonsense" }
```

That's basically it. It's like a password but very long and issued by the remote service. If anyone finds out what your Bearer token is they would be able to impersonate you, so it's important they go over secure channels only. Bearer tokens typically (but not always) have short life-times and you'd normally be given an expiry time along with the token itself. Cookies are a common variation of the Bearer token.

The exchange in this document describes a mechanism for a server to request a Bearer token from another server, so that subsequent requests can be secured.

## The exchange in a nutshell.

There are two particpants in this exchange:
- The **Initiator** is requesting a Bearer token.
- The **Issuer** issues that Bearer token back to the Initiator.

The exchange takes the form of these two HTTPS tranactions.

- **Initiate** where the *Initiator* starts of the exchange by calling the *Issuer*.
- **Issue** where the *Issuer* supplies the issued Bearer token to the *Initiator*.

Both participants will have had an established relationship by the time this exchange takes place and will have configured these details in advance.
- The URL for the Initiator to make a POST request to the Issuer.
- The URL for the Issuer to make a POST request back to the Initiator.

Depending on the specific needs, it may be prudent to include a user-id parameter in these URLs, but that will be an implementation detail.

### Initiate

The process is started by the Initiator, who needs a Bearer token from the issuer.

```
POST https://issuer.example/api/initiate?initiator_user_id=123456
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "InitiatorsKey": "rdMWf2RYgWC-OwTzzO8VHqK-27kAKK6qQf9-JqN2xU0ICcW"
}
```

- `POST`
  - The choice of URL is mutually agreed by both participants in advance. The issuer will need to know from this URL who the initiator is.
- `"CrossRequestTokenExchange":`
  - This indicates the client is attempting to use the CrossRequestTokenExchange process. The value of this property is the version string, with `DRAFTY-DRAFT-3` indicating this version of this document.
  - The client might prefer to use a later version. If the service does not support that version, it may indicate the versions it does know in a `400` response. (See section **Version Negotiaton** later.)
- `"ExchangeId":`
  - The Issue request that follows will include this value, which must be a valid GUID.
- `"InitiatorsKey":`
  - A key that, when combined with the issuer's own key, will be used to "sign" the Bearer token, confirming the Bearer key came from the expected source.
  - The value must consist of only printable ASCII characters (33 to 126) and must together represent at least 256 bits of cryptographic quality randomness.
  - The HMAC key will come from a PBKDF2 using this initiator's key and the isser's key, so it doesn't matter which encoding mechanism (hex, base64, etc) is used.
  - This value's length must be 1024 characters or shorter.

The issuer service will keep this request open until the exchange has concluded. If the exchange was successful, it will return a `204` response to close the exchange. If the issuer detects any error in the proces of handling this request, it should instead return an applicable HTTP error with enough detail for a developer to diagnose and fix the error in the response body. Any error response from the issuer is an indication that any Bearer token it might have retreived should be discarded.

Once both HTTPS transactons have closed and the exchange was sucessful, the Initiator now has a Bearer token it may us with the Issuer's web servuce. 

### Issue

The Issue step is performed by the Issuer to pass the Bearer token to the Initiator. This happens in a separate HTTPS tranaction while the original Initiaate request is kept open.

Because the Issuer is sending the Bearer token to a pre-agreed POST URL over HTTPS, they can be sure no-one else will have eavesdropped on that transaction. Because the request body includes an HMAC signature based on the key material supplied by the Initiator, they can be sure the Bearer token genuinely from the Issuer.

```
POST https://initiator.example/api/Issue?issuer_user_id=12345
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
    "BearerToken": "Token_09561454469379876976083516242009314095393956",
    "ExpiresAt": "2023-10-24T14:15:16Z",
    "IssuersKey": "Ti9jLhtBj4l-FLj3MvjbXnU-6FAMineB5Tv-sHn9p8huIEj",
    "BearerTokenSignature": "EA888FB47D9FEE03229757E2F6865AF0CF279BA33EAA702271E2A8AC6190177B",
}
```

- `POST`
  - The URL agred in advance.
- `"CrossRequestTokenExchange"`
  - An acknowledgment that the Issuer is using this version of this protocol.
- `"ExchangeId"`
  - The GUID copied from the original Initiate request.
- `"BearerToken"`
  - This is the requested Bearer token. It must consist only of printable ASCII characters.
- `"ExpiresAt"`
  - The UTC expiry time of this Bearer token in ISO format. (yyyy-mm-ddThh:mm:ss)
- `"IssuersKey"`
  - Key material that will go into the HMAC signing key. This string may be zero to 1024 characters in length. All characters must be in the printable ASCII range, 33 to 126.
- `"BearerTokenSignature",`
  - The HMAC signature of the BearerToken, signed using the `InitiatorsKey` from the original Initiate request and the `IssuersKey` from this request.

If the intiator finds the Bearer token it has received to be acceptable (including checking the HMAC signature), it must respond to this inner request with a 204 code, indicating all is well. Nonetheless, it must not use the newly issued Bearer token until the issuer also closes its outer request with 204 code as well.

Any error response from the initiator to the issuer indicates it does not accept the issued Bearer token, including any kind of local error. The response body shoudl include enough detail to help a developer resolve the issue.

## Version Negotiation

The intial request JSON includes a property named `CrossRequestTokenExchange` with a value specifying the version of this protocol the client is using. As this is the first (and so far, only) version, all requests should include this string. 

If a request arrives with a different unknown string value to this property, the servive should respond with a `400` (bad request) response, but with a JSON body including a property named `"AcceptVersion"`, listing all the supported versions in a JSON array of strings.

```
POST https://bob.example/api/BearerRequest
{ "CrossRequestTokenExchange": "NEW-FUTURE-VERSION-THAT-YOU-DONT-KNOW-ABOUT", ... }

400 Bad Request
{ 
    "Message": "Unknown version.",
    "AcceptVersion": [ "DRAFTY-DRAFT-3" ] 
}
```

The requestor may now repeat that request but interacting according to the rules of this document.

## HMAC Key Derivation

The Intiate request body will include a property named `InitiatorsKey` that must contain at least 256 bits of cryptographic quality randomness, expressed in any convenient encoding such as Hex or base64.
The Issue request body must also include a property named `IssuersKey` that may contain any number of characters including an empty string. Both strings must consist only of printable ASCI characters from 33 to 126.

Both keys are combined and passed into a PBKDF2 function withe the parameters listed below. The output 256 bit block is the HMAC signing key. Both participants, possessing both the key they generated themselves and the key from the orther participant will perform the same operation to derive the HMAC signing key. The Issuer will sign their Bearer token with the HMAC key and the Initiator will verify the Bearer token is genuine by verifying the signature with the HMAC key.

The PBKDF2 function will have the following parameters:
- *Password* - The ASCII bytes of the following concatenated string:
  - The character length of the Intiator's key in ASCII decimal.
  - A single space character (32).
  - The Initiator's key in full.
  - A single space character (32).
  - The character length of the Issuer's key in ASCII decimal.
  - A single space character (32).
  - The Issuer's key in full.
  - A single '!' character (33).
- *Salt* - The ASCII bytes of the following 120 character capital-letter-only string.
 - "EWNSJHKKHOJGAJBMKAYGKJKLMNCAAISFNKCFXJAT" +
 - "YFZFYVQHLZNKHCXWEEDAIOXWXYCVOHUGSAASAICT" +
 - "GMVYVATDOYXXQHNDRXXQHPXHFOSQPNPQKUWWCJUO"
- *Hash* - SHA256.
- *Rounds* - 99.
- *Output* - 256 bits.

The inclusion of the fixed salt is to ensure the derived key could only be found by reading this document. The use of PBKDF2 itself is to make it difficult to construct a selected key. It is belived that single round of hashing would be sufficient, given the input should already represent 256 bits of cryptographic quality randomness, but 99 rounds ups the burden a little.

# Case Studies

## A Saas API.

**saas.example** is a website with an API designed for their customers to use. When a customer wishes to use this API, it must first go through this exchange to obtain a Bearer token. The service documents that users of their API may make Initiate requests to the URL `https://saas.example/api/login/crte?userId=id`, filling in their unique user ID in the query string.

**Carol** is a customer of Saas. She's recently signed up and has been allocated her unique user id, 12. She's logged into the Saas customer portal and browsed to their authentiation page. Under the CRTE section, she's configued her asccount that `https://caarol.example/saas/crte-receive-token` is her Issue URL, where she's implemented a handler.

Time passes and Carol needs to make a request to the Saas API. As she has no Bearer tokens, she makes a POST request to the documented API:
```
POST https://saas.example/api/login/crte?userId=12
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "F952D24D-739E-4F1E-8153-C57415CDE59A",
    "InitiatorsKey": "d28a9nCdKiO-0zErstyHMRk-GTNVKcj8YSs-6x362hWA4wa"
}
```

The Saas website code looks up user 12 and finds an active user with CRTE configured - if not, it would respond with an error. At this point it does not yet know if the Initiate request came from the real Carol yet. 

Despite this, the Saas service generates a Brarer token by base64 encodig some securly generated bytes and signs the token using HMAC. It does not yet save this token to it's own database but holds it in memory until such time the providence of Carol as the Initiator can be confirmed.

Using Carol's configured URL and the generated Bearer token, the Saas web service software opens up a new HTTPS request:
```
POST https://carol.example/saas/crte-receive=client-token
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "F952D24D-739E-4F1E-8153-C57415CDE59A",
    "BearerToken": "Token_41401899608293768448699806747291819850802610",
    "ExpiresAt": "2023-10-24T14:15:16",
    "IssuersKey": "NEYH0hiltyU-mytenH9TYtZ-U6flEyxEBrR-Y8d71J41scH",
    "BearerTokenSignature": "7CE717B05DCDBC0301EAB3E1027CF64E7BA0E1BE9FD2B8951759384DA96EABBB",
}
```

As Carol really is the Initiator, her web service can look up the supplied ExchangeId and find the Initiate request it opened earlier. It has a Bearer token but it doesn't yet know if this is the genuinely the Saas service making an Issue request yet. To check this, it performs the same steps to generate the HMAC key for this exchange and uses that to check the signature. Happy that everything is verified, it stores the Bearer token but it can't use the token just yet.

To confirm that all is well, Carol's web service closes the Issue request by sending a 204 status, indicating that it accepts the Bearer token. The Saas web server finally writes the token it generated into the database. Once that step is complete and successful, the Saas software finally closes the Initiate request with a 204, this time signalling that Carol may now use the Beaer token it issued.

```
GET https://saas.example/api/status.json
Authorization: Bearer Token_41401899608293768448699806747291819850802610
```

## Webhooks

The authentication requests don't always go from Carol to Saas. Occasionally, the Saas service will need to call back to Carol's web service to deal with an event. When this happens, Carol's web service needs to be certain the request is coming from Saas and not someone else trying to get privilendged information from Cartol's Webhook handler.

To deal with this, as well as comfiguring a Webhook URL, Carol has also configured her own Initiate end-point. should the Saas service need to auuthenticate itself.

Time passes and the Saas service needs to call Carol's API to make a decision, but it doesn't have a value Bearer token from her. It looks up the URL she configured and makes an HTTPS request:
```
POST https://carol.example/saas/crte-generate-token-for-webhook
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "B405DE48-36F4-4F42-818C-9BE28D6B3832",
    "InitiatorsKey": "dAOkkvk9Ojm-Vuh20X2KX46-HgsPiksQHrw-iIApjGjvjMk"
}
```

Carol's web service opens a new HTTPS request back to the Saas web site and in a similar way to before, it populates this new request with a Bearer token it had generated and an HMAC signature. The Saas API conduments that the webhook CRTE Issue requests should go to `https://saas.example/api/issue-crte?user_id=is` with the user's id added to the query string parameter.
```
POST https://saas.example/api/issue-crte?user_id=12
Content-Type: application/json
{
    "CrossRequestTokenExchange": "DRAFTY-DRAFT-3",
    "ExchangeId": "B405DE48-36F4-4F42-818C-9BE28D6B3832",
    "BearerToken": "Token_51968699312599211031848828204659448702950696",
    "ExpiresAt": "2023-10-24T14:15:16",
    "IssuersKey": "HUXBnbHNajT-10GpQjxWwTQ-yPrf4cx206V-LBHezSlGVcB",
    "BearerTokenSignature": "5FF10ABB78EA3C250E68BA503ECE4E1DBB3342993B3E294651743300D91E07A7",
}
```

The Saas service first acknowledes acceptance of the token by returning 204 to its incoming HTTPS request. Carol's web service handler acknowledges the acknowledgment by also responding to its incomming request with 204.

With a token that's been confirmed valid, the Saas service may now make its Webhook call.

```
POST https://carol.example/saas/webhook
Authorization: Bearer Token_51968699312599211031848828204659448702950696
Content-Type: application/json
{ "IsThisAWebhook?": true }
```

## Anticipated Asked Questions

### What's wrong with keeping a pre-shared secret long term?
They require management and secure storage. If we've already made the investment in configuring TLS on both sides, why not utilize that and get rid of the pre-shared secrets?

### Isn't the HMAC key a pre-shared secret?
It has vital diffeences.   
First, it isn't pre-shared. The HMAC key can be generated as needed on the fly. All you need is a cryptograhpic quality random number generator.
Secondly, you only need it for the duration of the exchange, which could be over in a second.

### I don't have a web server. I'm behind a firewall.
Then this exchange is not for you. It works by utilizing that both sides can accept TLS connections and if one side can't do that then this isn't going to work.

### I'm not a web server, but I have one on the other side of the Internet.
Do you have a secure shared resource like a database that both you and the web server can access in a secure manner? Try storing the HMAC key in the database against the exchange ID, then allow your web server to acceot requests from the issuer.

### Why not encrypt the BearerToken in the Issue step?
It's already encrypted. By TLS.

I am open to this idea as it was a step in an earlier version. The Initiator's key was a much longer and the PBKDF2 produced anough bits for an AES Key+IV as well and the HMAC key. I took it out because I couldn't find a reasonable risk factor where the extra encryption layer could have helped.

At the end of the day, the Issuer has the Bearer token and passes it to the Initiator. A third party can't eaves-drop because TLS protects the channel.

### How long should a beaer token last until expiry?
Up to you but (finger in the air) I'd go for an hour. If the exchange takes too long, remember you can do it in advance and have the Beaerer token ready if needed.

## A brief security analysis

### "What if an attacker attempts to eavesdrop or spoof either request?"
TLS will stop this. The security of this protocol depends on TLS working. If TLS is broken then so is this exchange.

### "What if an attacker sends a fake Initiate request?"
Then the Issuer will generate a unasked-for Bearer token and send it to the real Initiator. They will reject the issued Beaer token because she wasn't expecting one and respond with an error to the Issuer, who may use this as a sign to delete the issued token and put the fake Initiator's IP address on a block-list.

### "What if the attacker sends a fake Initiate request to the real Ussuer, but the attacker knows what exchangeID GUID will use?
Some varieties of GUID are predictable and the attacker might predict when a genuine Initiator is about to Initiate and what exchangeID GUID they will use. In this event, the Issuer will make to Issue requests to the real Initiator. They will rejct one and accept the other, because the attacker doesn't know how to sign the token.

### "What if an attacker sends a fake Issue request?"
If the Initiator isn't expecting an Issue request, they won't have a HMAC key to check the signature, so can reject the request.

If the Initiator *is* expecting an Issue request, they will be able to test it came from the genuine Issuer by checking the HMAC hash.

### "What if Initiator uses predictable randomness when generating the HMAC key?"
The Initiator should use unpredictable cryptographic quality randomness when generating the Initiator's key. Any platform capable of making a TLS connection should be able to do this.
