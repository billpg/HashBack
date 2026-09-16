import requests
import uuid
import secrets
import base64
import json
import hashlib
import time

# Build a claim.
verify_url = f"https://demo.hashback.dev/hash/{uuid.uuid4()}"
hashback_claim = {
    "Version": "BILLPG_DRAFT_4.2",
    "Host": "demo.hashback.dev",
    "Now": int(time.time()),
    "Unus": base64.b64encode(secrets.token_bytes(128//8)).decode('us-ascii'),
    "Verify": verify_url
}

# Hash the claim.
json_as_bytes = json.dumps(hashback_claim).encode('utf-8')
fixed_salt = base64.b64decode("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=")
hash = base64.b64encode(hashlib.sha256(fixed_salt + json_as_bytes).digest())

# Upload the hash to the hashback demo server.
# Replace this with your own server upload!
put_response = requests.put(verify_url, data=hash, headers={"Content-Type": "text/plain"})
if put_response.status_code != 200:
    raise Exception(f"Failed to upload hashback claim: {put_response.status_code} {put_response.text}")

# Make the authenticated call to the permit api.
token = base64.b64encode(json_as_bytes).decode('us-ascii')
hello_response = requests.get("https://demo.hashback.dev/hello", headers={"Authorization": "HashBack " + token})
print(f"Hello response: {hello_response.status_code} {hello_response.text}")

# Use the cookie returned by the hello api to make another call to the hello api.
cookie_response = requests.get("https://demo.hashback.dev/hello", cookies=hello_response.cookies)
print(f"Cookie response: {cookie_response.status_code} {cookie_response.text}")

