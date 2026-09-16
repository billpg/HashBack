# HashBack Demo Service

Welcome! This service demonstrates the [HashBack](https://github.com/billpg/HashBack/)
authentication protocol in action — no accounts, no API keys, just two short HTTPS
transactions built on the TLS you already trust.

Pick an endpoint to get started:

- **[/hello](/hello)** — Test your **client** code. Send a valid HashBack `Authorization`
  header and get a friendly hello back.
- **[/call](/call)** — Test your **service** code. Give it your URL and it'll make a fully
  HashBack-authenticated request to you, then report exactly what happened.
- **[/hash](/hash)** — Nowhere to publish your verification hash yet? This service will host
  it for you, briefly.
- **[/permit](/permit)** — Before this service will send GET requests your way, your site
  needs to opt in. Find out how here.

Prefer the raw API surface? See the [OpenAPI spec](/openapi).
