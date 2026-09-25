---
title: HashBack | Can you help?
next: https://github.com/billpg/HashBack/
next-text: Next: More technical docs at the Github!
---
# Can you help?

**HashBack** is still a work in progress. Can you help us get across the finishing line?

## Can you perform a security analysis?

Do you know security and cryptography?

<table class="questions" border="0">
<tr><td valign="middle" class="questionmark">🤨&#160;</td><td class="questions-evenrow">Was switching from PBKDF2 to a single salted SHA‑256 the right call for this threat model?</td></tr>
<tr><td valign="middle" class="questionmark">🤔&#160;</td><td class="questions-oddrow">Is the two‑transaction model cryptographically sufficient, or does ACME’s extra initiation step close an attack vector I haven’t considered?</td></tr>
<tr><td valign="middle" class="questionmark">🤔&#160;</td><td class="questions-evenrow">Should receiving services validate the entropy, uniqueness and freshness of the <code>Unus</code> value?</td></tr>
<tr><td valign="middle" class="questionmark">🤔&#160;</td><td class="questions-oddrow">Is using a fixed, public salt with SHA‑256 appropriate for this protocol’s goals?</td></tr>
<tr><td valign="middle" class="questionmark">❓&#160;</td><td class="questions-evenrow">Are there any attack scenarios where an adversary could trick the server into fetching a malicious verification hash URL?</td></tr>
<tr><td valign="middle" class="questionmark">🧙‍&#160;</td><td class="questions-oddrow">Does the protocol adequately protect against replay attacks if the client publishes the same verification hash twice?</td></tr>
<tr><td valign="middle" class="questionmark">🤔&#160;</td><td class="questions-evenrow">Is the verification hash sufficiently bound to the intended destination host? Is the "Host" value in the JSON enough?</td></tr>
<tr><td valign="middle" class="questionmark">🤨&#160;</td><td class="questions-oddrow">Is it safe to assume that TLS identity guarantees are symmetric between client and server in all deployment environments?</td></tr>
<tr><td valign="middle" class="questionmark">🤦‍&#160;</td><td class="questions-evenrow">Are there any risks if the either side is behind a reverse proxy or CDN that terminates TLS?</td></tr>
<tr><td valign="middle" class="questionmark">❔&#160;</td><td class="questions-oddrow">Is a fixed salt ever a liability?</td></tr>
<tr><td valign="middle" class="questionmark">❔&#160;</td><td class="questions-evenrow">Should the salt be versioned or rotated over time?</td></tr>
<tr><td valign="middle" class="questionmark">❓&#160;</td><td class="questions-oddrow">Should the JSON claim include an explicit expiration time or maximum validity window?</td></tr>
<tr><td valign="middle" class="questionmark">🤦‍&#160;</td><td class="questions-evenrow">Should the JSON claim include a client identifier beyond the verification URL?</td></tr>
<tr><td valign="middle" class="questionmark">❔&#160;</td><td class="questions-oddrow">Should servers rate‑limit or throttle verification‑hash fetches to avoid abuse? Could throttling enable a denial-of-service attack?</td></tr>
<tr><td valign="middle" class="questionmark">🕵️‍&#160;</td><td class="questions-evenrow">Should clients delete verification hashes after successful authentication?</td></tr>
<tr><td valign="middle" class="questionmark">🤔&#160;</td><td class="questions-oddrow">Is this protocol simple enough to be formally modelled and would that be worthwhile?</td></tr>
<tr><td valign="middle" class="questionmark">❔&#160;</td><td class="questions-evenrow">Are there any weaknesses or issues I haven’t considered?</td></tr>
</table>

If you've found any issues with my draft, please raise an issue on
[the project's GitHub](https://github.com/billpg/HashBack).

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “If anything looks suspicious, prod it gently. Preferably with a stick.”</p>

## Write a library

I've made a start with [a dot-net library](https://github.com/billpg/HashBack/tree/main/billpg.HashBackCore)
that implements the core of the exchange. I plan to extend this into a reusable module you
can drop into any dot-net web service. Can you help the project by implementing the
exchange into other frameworks?

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “I dream of a world where every language has a HashBack library and none of them segfault.”</p>

## Sponsor me! 💰💲🤑💲💰

Would you like to support ongoing development?

The exact mechanism is still to be discussed — but if you're interested, please let me
know.

Another good way to support this project is to employ me! I'm an experienced software
engineer with experience in robust designs, distributed systems, cloud systems and
resilience when the world reminds you that **Failure is Always an Option!™**.

**[CV.BILLPG.COM](https://cv.billpg.com/)**

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “If you help HashBack, you help me. And I am adorable.”</p>
