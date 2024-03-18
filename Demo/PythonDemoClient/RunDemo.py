import os
import uuid
import requests
import random
import base64
import json
from time import time_ns
import hashlib


api_root = "http://localhost:3001"

# Select an ID to store the hash. This is needed by the
# hash storage service and isn't part ofthe HashBack exchange.
hash_id = uuid.uuid1()
print(f"Hash ID: {hash_id}")

# Build a "Unus" value from 256 cryptographic quality random bits.
unus = str(base64.b64encode(os.urandom(256 // 8)), "ascii")
print(f"Unus: {unus}")

# Build a request JSON in the order RFC8785 expects.
request_body = {
    "HashBack": "HASHBACK-PUBLIC-DRAFT-3-1",
    "IssuerUrl": f"{api_root}/bleh",
    "Now": time_ns() // (1000 * 1000 * 1000),
    "Rounds": 1,
    "TypeOfResponse": "BearerToken",
    "Unus": unus,
    "VerifyUrl": f"{api_root}/hashes?ID={hash_id}",
}
request_body_as_string = json.dumps(request_body, separators=(",", ":"))
request_body_as_bytes = bytes(request_body_as_string,'utf-8')
print(request_body_as_bytes)


# Find the hash using PBKDF2
hash_as_bytes = hashlib.pbkdf2_hmac(
    hash_name ="SHA256",
    password=request_body_as_bytes,
    salt=bytes(
        [
            113,
            218,
            98,
            9,
            6,
            165,
            151,
            157,
            46,
            28,
            229,
            16,
            66,
            91,
            91,
            72,
            150,
            246,
            69,
            83,
            216,
            235,
            21,
            239,
            162,
            229,
            139,
            163,
            6,
            73,
            175,
            201,
        ]
    ),
    iterations=1,
    dklen = 32
)

# Encode the completed hash.
hash_as_string = str(base64.b64encode(hash_as_bytes), 'ascii')
print(f"Hash: {hash_as_string}")

# Store hash on hash store.
store_hash_resp = requests.post(
    f"{api_root}/hashes",
    json={"ID": str(hash_id), "Hash": hash_as_string},
)
print(store_hash_resp)
print(store_hash_resp.content)
print(f"{api_root}/hashes?ID={hash_id}")

# Now actually send the request to the actual HashBack service.
hashback_resp = requests.post(f"{api_root}/issuerDemo/request", json=request_body)

print(hashback_resp)
print(hashback_resp.json())

pass
