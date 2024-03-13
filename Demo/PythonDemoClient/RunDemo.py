import requests
import random
from time import time_ns

api_root = "http://localhost:3001"

print("Requesting user-folder from hash-store service.")
user_name = "rutabaga84"
get_user_resp = requests.get(f"{api_root}/devHashStore/user?name={user_name}")
if get_user_resp.status_code != 200:
    print(f"API returned status {get_user_resp.status_code}")
    exit()
user_folder = get_user_resp.json()["UserFolder"]
print(f"User Folder = {user_folder}")

# Build JSON Request body, including "User" property for the name.
filename_as_int = int(random.random() * 1000000)
request_body = {
    "User": user_name,
    "HashBack": "HASHBACK-PUBLIC-DRAFT-3-1",
    "TypeOfResponse": "BearerToken",
    "IssuerUrl": f"{api_root}/bleh",
    "Now": time_ns() // (1000 * 1000 * 1000),
    "Rounds": 1,
    "Unus": "RutabagaRutabagaRutabagaRutabagaRutabagaRut=",
    "VerifyUrl": f"{api_root}/devStoreHash/load/{user_folder}/{filename_as_int}.txt",
}
print(request_body)


# Send request to hash store so it'll be available for GETing by the HashBack service.
store_hash_resp = requests.post(f"{api_root}/devHashStore/store", json=request_body)
if store_hash_resp.status_code != 200:
    print(f"API returned status {get_user_resp.status_code}")
    exit()
request_hash = store_hash_resp.json()["Hash"]
hash_url = store_hash_resp.json()["VerifyUrl"]
print(f"Request Hash: {request_hash}")
print(f"Hash URL: {hash_url}")

# Now actually send the request to the actual HashBack service.
request_body.pop("User")
hashback_resp = requests.post(f"{api_root}/issuerDemo/request", json=request_body)


print(hashback_resp)
print(hashback_resp.json())

pass
