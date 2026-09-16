#!/usr/bin/env bash
# Builds the shipping Web player into dist/, from the editor version this project pins.
#
#   Tools/build-web.sh
#
# A thin wrapper over `npm run build`, which is the entry point the parent catalog calls when this
# repo is checked out at games/flappy-voice. Both land in build.mjs, so the shell path and the
# catalog path cannot drift; the pre-flight checks and the editor lookup live there.
#
# Nothing to configure. UNITY_EDITOR_PATH (or UNITY_PATH) overrides the editor.
# Close the Editor first; it locks the project.
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec node "$project_root/build.mjs" "$@"
