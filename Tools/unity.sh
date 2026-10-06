#!/usr/bin/env bash
# Runs the Unity 6.6 editor against this project.
#
# The editor links against libxml2.so.2, but CachyOS/Arch now ship libxml2.so.16, so the
# editor exits immediately with "libxml2.so.2: cannot open shared object file". The proper
# fix is `sudo pacman -S libxml2-legacy`; until then this script points the loader at a
# copy of that library extracted to ~/.local/share/ptt-unity-libs.
#
#   Tools/unity.sh                       open the project in the editor
#   Tools/unity.sh build-linux           batch-build Builds/Linux/PackTheTrunk.x86_64
#   Tools/unity.sh build-mac             batch-build Builds/macOS/PackTheTrunk.app (universal, unsigned)
#   Tools/unity.sh build-windows         batch-build Builds/Windows (needs Windows Build Support)
#   Tools/unity.sh build-webgl           batch-build Builds/WebGL
set -euo pipefail

UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export LD_LIBRARY_PATH="$HOME/.local/share/ptt-unity-libs${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

case "${1:-open}" in
  open)
    exec "$UNITY" -projectPath "$PROJECT"
    ;;
  build-linux)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod PackTheTrunk.EditorTools.BuildScript.BuildLinux -logFile -
    ;;
  build-mac)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod PackTheTrunk.EditorTools.BuildScript.BuildMac -logFile -
    ;;
  build-windows)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod PackTheTrunk.EditorTools.BuildScript.BuildWindows -logFile -
    ;;
  build-webgl)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod PackTheTrunk.EditorTools.BuildScript.BuildWebGL -logFile -
    ;;
  *)
    echo "usage: $0 [open|build-linux|build-mac|build-windows|build-webgl]" >&2
    exit 2
    ;;
esac
