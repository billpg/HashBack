import os
import uuid
import requests
import base64
import json
from time import time_ns
import hashlib

# This is the Issuer URL that will receive the POST request
# requesting a Bearer token. Remember to change it to your
# own service URL.
hashback_service_url = "http://localhost:3001/issuer"
print(f"HashBack Service URL: {hashback_service_url}")

# This is the base verification hash service URL. The ID
# query string paameter will have a UUID we select identifying
# this request.
verification_hash_url = "http://localhost:3001/hashes"
print(f"Verifcation Hash URL: {verification_hash_url}")

# Select an ID to identify the hash. The hash storage service
# will use this as the primary key for the hash itself.
hash_id = uuid.uuid1()
print(f"Hash ID: {hash_id}")

# Build a "Unus" value from 256 cryptographic quality random bits.
unus = str(base64.b64encode(os.urandom(256 // 8)), "ascii")
print(f"Unus: {unus}")

# Build a request JSON in the order RFC8785 expects.
request_body = {
    "HashBack": "HASHBACK-PUBLIC-DRAFT-3-1",
    "IssuerUrl": hashback_service_url,
    "Now": time_ns() // (1000 * 1000 * 1000),
    "Rounds": 1,
    "TypeOfResponse": "BearerToken",
    "Unus": unus,
    "VerifyUrl": f"{verification_hash_url}?ID={hash_id}",
}

# Display the request body.
print("Request Body JSON:")
for request_property_name in request_body:
    print(f"  {request_property_name} = {request_body[request_property_name]}")

# Convert the request body JSON into bytes.
request_body_as_string = json.dumps(request_body, separators=(",", ":"))
request_body_as_bytes = bytes(request_body_as_string, "utf-8")

# Find the verification hash using PBKDF2, per the HashBack draft version 3.1.
hash_as_bytes = hashlib.pbkdf2_hmac(
    hash_name="SHA256",
    password=request_body_as_bytes,
    salt=base64.b64decode("cdpiCQall50uHOUQQltbSJb2RVPY6xXvouWLowZJr8k="),
    iterations=1,
    dklen=32,
)

# Encode the completed hash.
hash_as_string = str(base64.b64encode(hash_as_bytes), "ascii")
print(f"Hash: {hash_as_string}")

# Upload the hash to the hash service.
# Note: For actually-secure exchanges, use your own website.
#
# My example hash service is open to all and sundry, and the security
# of this exchange depends on you having a place to publish hashes
# that you can affirm as being the only one in control of it.
store_hash_resp = requests.post(
    verification_hash_url,
    json={"ID": str(hash_id), "Hash": hash_as_string},
)

# Validate the response.
if store_hash_resp.status_code != 200:
    print(f"Hash Service failure. Status={store_hash_resp.status_code}")
    print(store_hash_resp.content)
    exit()

# Now actually send the request to the actual HashBack service.
hashback_resp = requests.post(hashback_service_url, json=request_body)

# Validate the response.
if hashback_resp.status_code != 200:
    print(f"HashBack Issuer Service failure. Status={hashback_resp.status_code}")
    print(hashback_resp.content)
    exit()

# Display the result.
print("HashBack Issuer Service response:")
response_as_json = hashback_resp.json()
for response_property_name in response_as_json:
    print(f"  {response_property_name} = {response_as_json[response_property_name]}")

# End.
print("Issuer Demo complete.")
