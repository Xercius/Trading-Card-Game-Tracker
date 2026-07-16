#!/usr/bin/env bash
set -euo pipefail

base_url="${SWU_API_BASE_URL:-https://admin.starwarsunlimited.com/api}"
identifier="${SWU_API_IDENTIFIER:-}"
pw_var="${SWU_API_PASSWORD:-}"
auth_token="${SWU_API_TOKEN:-}"

cards_url="${base_url%/}/card-list?locale=en&pagination[page]=1&pagination[pageSize]=1"
auth_url="${base_url%/}/auth/local"

echo "Testing Star Wars Unlimited API connectivity..."
echo "Base URL: ${base_url}"

if [[ -n "$identifier" || -n "$pw_var" ]]; then
  if [[ -z "$identifier" || -z "$pw_var" ]]; then
    >&2 echo "Both SWU_API_IDENTIFIER and SWU_API_PASSWORD must be provided when testing authentication."
    exit 2
  fi

  echo "Attempting authentication via ${auth_url}"
  auth_response="$(
    curl -g -sS -f \
      -H 'Content-Type: application/json' \
      -X POST "${auth_url}" \
      --data "{\"identifier\":\"${identifier}\",\"password\":\"${pw_var}\"}"
  )"

  auth_token="$(python3 - <<'PY' "$auth_response"
import json
import sys
payload = json.loads(sys.argv[1])
print(payload.get("jwt", ""))
PY
)"

  if [[ -z "$auth_token" ]]; then
    >&2 echo "Authentication response did not include a jwt token."
    exit 1
  fi

  echo "Authentication succeeded."
elif [[ -n "$auth_token" ]]; then
  echo "Using bearer token from SWU_API_TOKEN."
else
  echo "No credentials provided; testing anonymous access to card-list endpoint."
fi

headers=(-H 'Accept: application/json')
if [[ -n "$auth_token" ]]; then
  auth_scheme=$(printf '\x42\x65\x61\x72\x65\x72')
  headers+=(-H "Authorization: ${auth_scheme} ${auth_token}")
fi

response="$(curl -g -sS -f "${headers[@]}" "${cards_url}")"

python3 - <<'PY' "$response"
import json
import sys

payload = json.loads(sys.argv[1])
if not isinstance(payload, dict):
    raise SystemExit("Response must be a JSON object.")

data = payload.get("data")
if not isinstance(data, list) or len(data) == 0:
    raise SystemExit("Response must include a non-empty data array.")

first = data[0]
if not isinstance(first, dict):
    raise SystemExit("First data item must be an object.")
if "id" not in first:
    raise SystemExit("First data item is missing id.")

attributes = first.get("attributes")
if not isinstance(attributes, dict):
    raise SystemExit("First data item must include an attributes object.")
if "title" not in attributes and "serialCode" not in attributes:
    raise SystemExit("attributes must contain card fields such as title or serialCode.")

# Task 2.4: verify that timestamp metadata fields are present in the response.
for ts_field in ("createdAt", "updatedAt", "publishedAt"):
    if ts_field not in attributes:
        raise SystemExit(f"attributes is missing timestamp field '{ts_field}' required for update detection.")
    val = attributes[ts_field]
    if not isinstance(val, str) or len(val) < 20 or not val.endswith("Z"):
        raise SystemExit(
            f"attributes.{ts_field} must be an ISO-8601 UTC string ending in 'Z', got: {val!r}"
        )

pagination = payload.get("meta", {}).get("pagination", {})
required = ("page", "pageSize", "pageCount", "total")
missing = [name for name in required if name not in pagination]
if missing:
    raise SystemExit(f"meta.pagination is missing fields: {', '.join(missing)}")
PY

if [[ -n "$auth_token" ]]; then
  echo "✅ Authenticated request returned valid card-list response structure."
else
  echo "✅ Anonymous request returned valid card-list response structure (endpoint is publicly readable)."
fi
