#!/usr/bin/env bash
# Prints the Unity editor binary for the version this project pins.
#
# The Hub's install root is not fixed: it differs per OS, it is a per-user
# directory when the Hub was installed without admin rights, and the Hub lets
# you move it. So resolve it rather than hardcoding /Applications.
#
#   UNITY="$(Tools/unity-path.sh)"
#   "$UNITY" -batchmode -nographics -projectPath . ...
#
# Override with UNITY_PATH=/path/to/binary to skip the search entirely.
set -euo pipefail

if [ -n "${UNITY_PATH:-}" ]; then
  echo "$UNITY_PATH"
  exit 0
fi

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${UNITY_VERSION:-$(sed -n 's/^m_EditorVersion: //p' \
  "$project_root/ProjectSettings/ProjectVersion.txt")}"
[ -n "$version" ] || { echo "no m_EditorVersion in ProjectVersion.txt" >&2; exit 1; }

case "$(uname -s)" in
  Darwin)          binary="Unity.app/Contents/MacOS/Unity"; hub_config="$HOME/Library/Application Support/UnityHub" ;;
  Linux)           binary="Editor/Unity";                   hub_config="${XDG_CONFIG_HOME:-$HOME/.config}/UnityHub" ;;
  MINGW*|MSYS*|CYGWIN*) binary="Editor/Unity.exe";          hub_config="${APPDATA:-}/UnityHub" ;;
  *) echo "unsupported platform $(uname -s)" >&2; exit 1 ;;
esac

roots=()
# The Hub writes its relocated install root here as a bare JSON string.
if [ -f "$hub_config/secondaryInstallPath.json" ]; then
  relocated="$(tr -d '"[:space:]' < "$hub_config/secondaryInstallPath.json")"
  [ -n "$relocated" ] && roots+=("$relocated")
fi
roots+=(
  "/Applications/Unity/Hub/Editor"
  "$HOME/Applications/Unity/Hub/Editor"
  "$HOME/Unity/Hub/Editor"
  "/opt/unity/editors"
  "/opt/Unity/Hub/Editor"
  "${PROGRAMFILES:-/c/Program Files}/Unity/Hub/Editor"
)

for root in "${roots[@]}"; do
  candidate="$root/$version/$binary"
  if [ -x "$candidate" ]; then
    echo "$candidate"
    exit 0
  fi
done

{
  echo "Unity $version not found. Searched:"
  printf '  %s\n' "${roots[@]/%//$version/$binary}"
  echo "Set UNITY_PATH to the editor binary, or install $version from the Hub."
} >&2
exit 1
