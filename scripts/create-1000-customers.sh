#!/usr/bin/env bash

set -euo pipefail

graphql_url="${GRAPHQL_URL:-http://localhost:5000/graphql}"

mutation='mutation CreateCustomer($input: CustomerCreateInput!) {
  createCustomer(input: $input) {
    id
    name
    email
    birthDate
  }
}'

for number in $(seq 2 1000); do
  suffix=$(printf '%04d' "$number")
  day=$(printf '%02d' "$((number % 28 + 1))")
  payload=$(printf \
    '{"query":"%s","variables":{"input":{"name":"Customer %s","email":"customer%s@example.test","birthDate":"1990-01-%s"}}}' \
    "${mutation//$'\n'/\\n}" \
    "$suffix" \
    "$suffix" \
    "$day")

  response=$(curl --fail-with-body --silent --show-error \
    --request POST \
    --header 'Content-Type: application/json' \
    --data "$payload" \
    "$graphql_url")

  if [[ "$response" == *'"errors"'* ]]; then
    echo "Customer $suffix failed: $response" >&2
    exit 1
  fi

  printf 'Created customer %s/1000\r' "$number"
done

printf '\nCreated 1,000 customers successfully.\n'
