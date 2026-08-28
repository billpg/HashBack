# HashBack Internet-Draft

`draft-godfrey-hashback-00.xml` is the first-draft RFC text for the HashBack
protocol, written in the [xml2rfc version 3](https://authors.ietf.org/en/xml2rfcv3)
vocabulary and ported from the protocol description in the root
[README.md](../README.md).

This is a starting point, not a finished submission - in particular:

- The Security Considerations and IANA Considerations sections should get a
  close read from someone experienced with the IETF process before this goes
  anywhere near a real submission.
- The example vectors (JSON object, Authorization header, verification hash)
  are copied from the current draft 4.2 README and should be kept in sync if
  the protocol changes.
- No working group has adopted this; it is an individual ("independent
  submission" track) draft, per the `submissionType="independent"` attribute
  on the `<rfc>` root element.

## 🖨️ Rendering it

This environment doesn't have `xml2rfc` installed (no working `pip`), so the
XML has only been checked for well-formedness and internal cross-reference
consistency, not rendered or run through the IETF's own validator. To
actually render or validate it:

```bash
pip install xml2rfc
xml2rfc draft-godfrey-hashback-00.xml --text --html
```

Alternatively, upload the `.xml` file to the IETF's own author tools at
<https://author-tools.ietf.org/> for rendering and validation without
installing anything locally.

## 🥾 Next steps

- Render it (see above) and proofread the output.
- Consider circulating it for informal review before an official
  `-00` submission via the IETF Datatracker.
- Keep the JSON examples and Fixed Salt bytes in sync with
  `billpg.HashBackCore/Helpers.cs` if the protocol version changes.
