# Asking Permission

Part of the HashBack protocol is that ahead-of-time, the client (who wants to prove their identity) and the server (who wants to check the caller's authenticity) will need to agree exactly which range of URLs the client has exclusive control over for hosting verification hashes. Because this demo service is designed for the whole world to use, we need to confirm you permit us to send lots of GET requests your way.

To be clear, there are two IPs in question:
- The **Caller** is the IP making the request to the demo service at either the /hello or /call end-point.
- The **Target** is the IP of the web server that the GET request (either for a verification hash or an authentication request) will be made to.
- (There's also the IP of this demo service, but you don't control that.)

To do this, before we GET anything from your target server, we'll GET `/.well-known/demo-hashback-dev.json` where we hope to find your permission to make further GET requests to your domain. The JSON should have the following structure:

``` json
{
    "GetPermissionGrantedTo": "demo.hashback.dev",
    "CallerIP": ["1.2.3.4"],
    "Url": ["/api/hashback/"],
    "GetsPerHour": 42
}
```

- The `"GetPermissionGrantedTo"` property is required to have the value `"demo.hashback.dev"`. This is to avoid misinterpreting random JSON objects as permission, especially as the other properties are all optional.
- The `"CallerIP"` property is a list of IPv4 or IPv6 addresses or networks that the caller request might come from. Use `null` or omit this property to grant requests coming from all caller IPs.
- The `"Url"` property is a list of allowed prefixes to the local part of the target URL. Use `null` or omit this property to allow GETs to all URLs at this domain.
- The `"GetsPerHour"` property is the number of GET requests you permit the demo service to make in this hour. This is a single quota for the whole grant, shared across your whole website: it covers every matching GET regardless of which URL or caller it came from, and regardless of whether it's `/hello` fetching a verification hash or `/call` making an authenticated request against your site. Omit it, or set it to `null`, for no limit.

If the caller's request doesn't match the target's permission JSON, or the target site doesn't have a JSON file at this location, the demo service will reject the caller's request with a 400 error.

To allow maximal permission to your target service, use this basic JSON:
``` json
{ "GetPermissionGrantedTo": "demo.hashback.dev" }
```