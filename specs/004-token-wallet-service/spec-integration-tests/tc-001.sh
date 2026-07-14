#!/usr/bin/env bash
source "$(dirname "$0")/config.env"

echo "=== TC-001: PaymentProcessor health check ==="

response=$(curl -sk -o /tmp/tc001_body.txt -w "%{http_code}" "$PP/health")
body=$(cat /tmp/tc001_body.txt)

if [ "$response" = "200" ] || echo "$body" | grep -qi "Healthy"; then
  echo "PASS: PaymentProcessor is healthy (HTTP $response)"
else
  echo "FAIL: PaymentProcessor health check failed (HTTP $response, body: $body)"
fi
