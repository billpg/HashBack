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
  on the `<rfc>` root element, with `category="info"` (Informational) since
  Standards Track is reserved for IETF-stream documents.

## 🖨️ Rendering it

The XML currently renders cleanly through `xml2rfc` 3.30.2 with no errors or
warnings. `draft-godfrey-hashback-00.txt` and `.html` aren't checked in,
since they're generated output - reproduce them yourself with whichever of
the following fits your machine.

**If you have root**, `xml2rfc` is a regular Ubuntu package - no `pip`
needed:

```bash
sudo apt install xml2rfc
xml2rfc draft-godfrey-hashback-00.xml --text --html
```

**If you don't have root** (true of this dev environment - no working `pip`
either), `apt-get download` fetches `.deb` files without installing them, so
you can extract `xml2rfc` and its dependencies into a local directory and
point Python at it instead of touching the system:

```bash
mkdir -p /tmp/xml2rfc-local && cd /tmp/xml2rfc-local
apt-get download xml2rfc python3-configargparse python3-google-i18n-address \
    python3-intervaltree python3-platformdirs python3-pycountry \
    python3-wcwidth python3-sortedcontainers
mkdir -p prefix
for f in *.deb; do dpkg-deb -x "$f" prefix; done

export PYTHONPATH=/tmp/xml2rfc-local/prefix/usr/lib/python3/dist-packages
python3 /tmp/xml2rfc-local/prefix/usr/bin/xml2rfc draft-godfrey-hashback-00.xml --text --html
```

That package list is what this XML needed beyond what a typical Ubuntu
desktop already has installed (`python3-lxml`, `python3-jinja2`,
`python3-requests`, `python3-yaml`, `python3-pkg-resources`, `sgml-base`). If
`xml2rfc` complains about a missing module on your machine, `apt-get
download` the matching `python3-<module>` package the same way and add it to
the `for` loop.

**No local tooling at all:** upload the `.xml` file to the IETF's own author
tools at <https://author-tools.ietf.org/> for rendering and validation
without installing anything.

## 🥾 Next steps

- Proofread the rendered text output - a clean `xml2rfc` run only confirms
  the markup is valid, not that the prose is right.
- Consider circulating it for informal review before an official
  `-00` submission via the IETF Datatracker.
- Keep the JSON examples and Fixed Salt bytes in sync with
  `billpg.HashBackCore/Helpers.cs` if the protocol version changes.
