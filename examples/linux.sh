#!/bin/bash

BASE_URL=http://localhost:5000
HEADER="Content-Type: application/json"

# API status
# curl -i "$BASE_URL" && echo

# connection status
# curl -i "$BASE_URL/status" && echo

# profile names
# curl -i "$BASE_URL/profilenames" && echo

# profile set
# curl -i -X POST "$BASE_URL/profile/set" -H "$HEADER" -d '{"profile":"idk"}' && echo

# routing
# curl -i -X POST "$BASE_URL/routing/set" -H "$HEADER" -d '{"action":""}' && echo
