---
title: HashBack | Isn't that ACME?
next: demo.html
next-text: Next: Do you have a demo service?
---
# Isn't that like ACME?

Yes, HashBack has a lot in common with ACME, in particular the "call me back" verification
idea at its core. The important difference is the problem each of the two serves.

## Two related ideas, different jobs

ACME, the protocol behind **Let's Encrypt**, is designed to *establish* TLS certificates.
HashBack, in contrast, is simpler in operation because both sides *already have TLS*
working.

<table class="comparison">
<thead><tr><th>Question</th><th>ACME</th><th>HashBack</th></tr></thead>
<tbody>
<tr><td>Number of transactions needed</td><td>3</td><td>2</td></tr>
<tr><td>General-purpose API authentication</td><td class="no">No</td><td class="yes">Yes</td></tr>
<tr><td>Works without TLS already configured</td><td class="yes">Yes</td><td class="no">No</td></tr>
<tr><td>Useful for establishing TLS</td><td class="yes">Yes</td><td class="no">No</td></tr>
</tbody></table>

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “ACME helps put the lock on the door. HashBack uses the lock. Both jobs matter, but they are not the same job. Also, I strongly recommend the lock.”</p>

## HashBack needs TLS first

HashBack relies on TLS to reassure the server that the verification hash came from the
site the client controls. Without valid TLS on both sides, the protocol cannot provide its
intended identity check.

In that sense, HashBack only works because **ACME** and **Let's Encrypt** made it
possible. HashBack only works because TLS protection is now widespread and automated,
giving it the trustworthy channel it needs to build on.

Thank you ACME!

<p class="hashbert"><strong>🦔 Hashbert says:</strong> “HashBack is just like ACME, only fewer coyotes are maimed.”</p>
