#!/usr/bin/env bash
# 승인된 레벨업 샘플(tools/levelup-art/*.js)의 캔버스 그리기로 아이콘·별·금속 글자·연출 조각을 굽는다 → unity/Assets/Resources/Art/LevelUp/*.png
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
OUT="$REPO_ROOT/unity/Assets/Resources/Art/LevelUp"
"$CHROME" --headless=new --disable-gpu --allow-file-access-from-files --virtual-time-budget=4000 --dump-dom "file://$REPO_ROOT/tools/levelup-art/bake.html" 2>/dev/null \
  | python3 "$REPO_ROOT/tools/skill-art/split.py" "$OUT"
