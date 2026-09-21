#!/usr/bin/env bash
# 순수 C# 코어를 Unity 프로젝트로 복사한다(단방향: core -> Unity).
#
# 사용법:
#   ./sync-core.sh                      # 저장소 안의 unity/ 프로젝트로 복사
#   ./sync-core.sh /path/to/UnityProject  # 다른 곳에 만든 Unity 프로젝트로 복사
#
# 심볼릭 링크 대신 복사를 쓰는 이유: Unity는 Assets 안의 심볼릭 링크를
# 플랫폼·버전에 따라 다르게 다뤄서 임포트가 조용히 깨지는 일이 있다.
# 코어 원본은 core/ 에만 있고, Unity 쪽 Core/ 폴더는 언제든 다시 만들 수 있는 사본이다.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET="${1:-$REPO_ROOT/unity}"
TARGET="$(cd "$TARGET" 2>/dev/null && pwd)" || {
  echo "대상 폴더가 없습니다: ${1:-$REPO_ROOT/unity}" >&2
  exit 1
}

SCRIPTS="$TARGET/Assets/Scripts"
mkdir -p "$SCRIPTS/Core"

# 원본(core)에서 사라진 파일·폴더의 .meta를 먼저 지운다.
# .meta는 Unity가 만든 것이라 아래 rsync에서 보호(제외)하는데, 그러면 코어에서 파일이나
# 폴더를 지웠을 때 사본에 .meta만 남아 rsync가 폴더를 지우지 못하고 실패한다.
SRC="$REPO_ROOT/core/FireGame.Core"
if [ -d "$SCRIPTS/Core" ]; then
  find "$SCRIPTS/Core" -name '*.meta' | while read -r meta; do
    rel="${meta#"$SCRIPTS/Core/"}"
    [ -e "$SRC/${rel%.meta}" ] || rm -f "$meta"
  done
fi

# 코어 복사. 빌드 산출물과 .csproj는 제외한다(Unity가 자체 프로젝트를 만든다).
rsync -a --delete \
  --exclude 'bin/' \
  --exclude 'obj/' \
  --exclude '*.csproj' \
  --exclude '*.meta' \
  "$SRC/" "$SCRIPTS/Core/"

# 저장소 밖 프로젝트라면 Unity 레이어 스크립트도 함께 넣는다.
if [ "$TARGET" != "$REPO_ROOT/unity" ]; then
  mkdir -p "$SCRIPTS/Unity"
  rsync -a --delete --exclude '*.meta' \
    "$REPO_ROOT/unity/Assets/Scripts/Unity/" "$SCRIPTS/Unity/"
fi

CORE_FILES=$(find "$SCRIPTS/Core" -name '*.cs' | wc -l | tr -d ' ')
UNITY_FILES=$(find "$SCRIPTS/Unity" -name '*.cs' 2>/dev/null | wc -l | tr -d ' ')
echo "동기화 완료 -> $SCRIPTS"
echo "  Core  : ${CORE_FILES}개 .cs"
echo "  Unity : ${UNITY_FILES}개 .cs"
