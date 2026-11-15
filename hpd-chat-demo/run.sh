#!/bin/sh
set -eu

if command -v node >/dev/null 2>&1; then
  node_bin="$(command -v node)"
else
  node_bin="/Users/localdmluser/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node"
fi

if [ ! -x "$node_bin" ]; then
  echo "Node.js was not found. Install Node 20+ or run this from Codex with its bundled runtime." >&2
  exit 1
fi

cd "$(dirname "$0")"
exec "$node_bin" ./node_modules/vite/bin/vite.js --host 127.0.0.1 --port 4173
