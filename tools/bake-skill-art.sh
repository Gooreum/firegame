#!/usr/bin/env bash
# 승인된 스킬 샘플의 캔버스 그리기로 무기·몹 스프라이트를 굽는다 → unity/Assets/Resources/Art/Skills/*.png
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
OUT="$REPO_ROOT/unity/Assets/Resources/Art/Skills"
"$CHROME" --headless=new --disable-gpu --virtual-time-budget=2000 --dump-dom "file://$REPO_ROOT/tools/skill-art/bake.html" 2>/dev/null \
  | python3 "$REPO_ROOT/tools/skill-art/split.py" "$OUT"
