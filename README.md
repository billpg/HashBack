# BearerTokenExchange
Server-to-Server Bearer Token Exchange Protocol

## What problem am I trying to fix?

Web Server **A** wants to make a request to Web Server **B**. Because we have TLS, A can be sure that it is talking with server B. However, B can't be at all certain the request it is processing came from A.

Most APIs deal with this using a process of shared secrets. The management console of an SaaS service will have a way to generate a shared secret that needs to be configured into the service making the request. Because that secret will be expected to be kept long term, it needs to be kept secure, requiring secret stores.

Since we do have TLS, if both sides of the conversation are already web servers, both sides already have a system of exchanging keys that's been established for decades. TLS itself.

This protocol allows servers to pass short-term "Bearer" authentication tokens between servers without having to store long-term secrets.

## How would it work?

You'd log into your SaaS API management console as normal. There would be a settings page where you can configure how your tenant in the system will work. 

| ------------- | ------------- |
| Content Cell  | Content Cell  |
