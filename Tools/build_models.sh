#!/usr/bin/env bash
# Regenerates every Blender model into Assets/Resources/Models (items, vehicles, props).
#   Tools/build_models.sh                     everything
#   Tools/build_models.sh items --only duck   just one item
#   Tools/build_models.sh --preview /tmp/prev  also render a PNG per model for review
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "${BLENDER:-blender}" -b --python "$ROOT/Tools/blender/build_models.py" -- "$@"
