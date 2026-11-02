#!/usr/bin/env bash
# The evaluator node's gates (graph_loop_template/nodes/05_evaluator.md, E1–E9),
# runnable on a machine that has the .NET 10 SDK.
#
#   ./scripts/verify.sh
#
# Exit code 0 means the slice passes the mechanical gates. Anything else is a
# FAIL and routes to the debugger node with this output as active_errors.

set -uo pipefail
cd "$(dirname "$0")/.."

FAILURES=0
pass() { printf '  \033[32mPASS\033[0m  %s\n' "$1"; }
fail() { printf '  \033[31mFAIL\033[0m  %s\n' "$1"; FAILURES=$((FAILURES + 1)); }
skip() { printf '  \033[33mSKIP\033[0m  %s\n' "$1"; }

echo "E1  build, warnings as errors"
if dotnet build SiraraCBA.slnx --nologo -v quiet > /tmp/sirara-build.log 2>&1; then
  pass "dotnet build"
else
  fail "dotnet build — see /tmp/sirara-build.log"
  tail -40 /tmp/sirara-build.log
fi

echo "E2  unit tests"
if dotnet test SiraraCBA.slnx --nologo --no-build \
     --filter "FullyQualifiedName!~IntegrationTests" > /tmp/sirara-unit.log 2>&1; then
  pass "unit + architecture suites"
else
  fail "unit tests — see /tmp/sirara-unit.log"
  grep -E "^\s+(Failed|Error)" /tmp/sirara-unit.log | head -40
fi

echo "E3  integration tests (real PostgreSQL)"
if ls tests/*IntegrationTests/*.csproj > /dev/null 2>&1; then
  if dotnet test SiraraCBA.slnx --nologo --no-build \
       --filter "FullyQualifiedName~IntegrationTests" > /tmp/sirara-integration.log 2>&1; then
    pass "integration suites"
  else
    fail "integration tests — see /tmp/sirara-integration.log"
  fi
else
  skip "no integration suites yet (they arrive with S03)"
fi

echo "E5  migrations apply to an empty database"
if ls src/Modules/*/*.Infrastructure/Migrations/*.cs > /dev/null 2>&1; then
  skip "run manually: dotnet ef database update against an empty database"
else
  skip "no migrations yet (they arrive with S03)"
fi

echo "E7  no unfinished markers in completed code"
MARKERS=$(grep -rnE "TODO|FIXME|HACK|NotImplementedException" src tests \
  --include="*.cs" 2>/dev/null | grep -v "/obj/" | grep -v "/bin/" || true)
if [ -z "$MARKERS" ]; then pass "none found"; else fail "found:"; echo "$MARKERS"; fi

echo "E8  no secrets in source or committed config"
SECRETS=$(grep -rnE "Password=|BEGIN (RSA )?PRIVATE KEY|ApiKey\"\s*:\s*\"[^\"]+" src \
  --include="*.cs" --include="*.json" 2>/dev/null | grep -v "/obj/" | grep -v "/bin/" || true)
if [ -z "$SECRETS" ]; then pass "none found"; else fail "found:"; echo "$SECRETS"; fi

echo "E9  no skipped or commented-out tests"
SKIPPED=$(grep -rnE "\[Fact\(Skip|\[Theory\(Skip|Assert\.Skip" tests \
  --include="*.cs" 2>/dev/null | grep -v "/obj/" || true)
if [ -z "$SKIPPED" ]; then pass "none found"; else fail "found:"; echo "$SKIPPED"; fi

echo
if [ "$FAILURES" -eq 0 ]; then
  printf '\033[32mEVALUATOR: PASS\033[0m — route to 07_architecture_reviewer\n'
else
  printf '\033[31mEVALUATOR: FAIL (%s gate(s))\033[0m — route to 06_debugger\n' "$FAILURES"
fi
exit "$FAILURES"
