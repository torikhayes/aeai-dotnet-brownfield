#!/usr/bin/env bash
# run-all.sh — Run all TC-* scripts in order and print a summary
#
# USAGE:
#   ./run-all.sh              # run every test case
#   ./run-all.sh tc-010 tc-011  # run specific test cases by name

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# Determine which scripts to run
if [[ $# -gt 0 ]]; then
  SCRIPTS=()
  for arg in "$@"; do
    SCRIPTS+=("${arg%.sh}.sh")
  done
else
  SCRIPTS=(tc-*.sh)
fi

PASS=0
FAIL=0
SKIP=0
RESULTS=()

run_script() {
  local script="$1"
  if [[ ! -f "$SCRIPT_DIR/$script" ]]; then
    echo "⚠️  $script not found — skipping"
    RESULTS+=("SKIP  $script")
    ((SKIP++)) || true
    return
  fi

  echo ""
  echo "────────────────────────────────────────"
  local output
  output=$(bash "$SCRIPT_DIR/$script" 2>&1)
  local exit_code=$?
  echo "$output"

  if echo "$output" | grep -q "^PASS"; then
    RESULTS+=("PASS  $script")
    ((PASS++)) || true
  elif echo "$output" | grep -q "^FAIL"; then
    RESULTS+=("FAIL  $script")
    ((FAIL++)) || true
  else
    RESULTS+=("????  $script  (no PASS/FAIL printed)")
    ((SKIP++)) || true
  fi
}

# ── Setup: get tokens first if not already cached ──────────────────────────
echo "=== Setup: fetching tokens ==="
if [[ ! -f "$SCRIPT_DIR/.alice_token" ]]; then
  echo "Running get-token.sh ..."
  bash "$SCRIPT_DIR/get-token.sh"
fi
if [[ ! -f "$SCRIPT_DIR/.alice_id" ]]; then
  echo "Running get-alice-id.sh ..."
  bash "$SCRIPT_DIR/get-alice-id.sh"
fi
if [[ ! -f "$SCRIPT_DIR/.bob_token" ]]; then
  echo "Running get-bob-token.sh ..."
  bash "$SCRIPT_DIR/get-bob-token.sh"
fi

# ── Run each test script ────────────────────────────────────────────────────
for script in "${SCRIPTS[@]}"; do
  run_script "$script"
done

# ── Summary ─────────────────────────────────────────────────────────────────
echo ""
echo "════════════════════════════════════════"
echo " TEST SUMMARY"
echo "════════════════════════════════════════"
for r in "${RESULTS[@]}"; do
  echo "  $r"
done
echo ""
echo "  Passed: $PASS  Failed: $FAIL  Skipped: $SKIP  Total: $(( PASS + FAIL + SKIP ))"
echo "════════════════════════════════════════"

if [[ $FAIL -gt 0 ]]; then
  exit 1
fi
