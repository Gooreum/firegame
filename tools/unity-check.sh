#!/usr/bin/env bash
# 실제 Unity로 컴파일하고 화면을 찍는다. 에디터를 열지 않고 결과만 확인하는 도구.
#
#   tools/unity-check.sh compile          실제 Unity 컴파일. error CS가 있으면 실패
#   tools/unity-check.sh shots [폴더]      화면을 PNG로 찍는다(기본: tools/.shots)
#
# 에디터가 같은 프로젝트를 열고 있으면 배치 모드가 실행되지 않는다. 먼저 에디터를 닫는다.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$REPO_ROOT/unity/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
MODE="${1:-compile}"
LOG="$(mktemp -t unity-check).log"

if [ ! -x "$UNITY" ]; then
  echo "Unity $VERSION 를 찾지 못했다: $UNITY" >&2
  exit 2
fi

"$REPO_ROOT/sync-core.sh" >/dev/null || exit 2

# 에디터가 프로젝트를 열고 있으면 배치 모드가 같은 폴더를 못 쓴다.
# 그때는 에디터를 닫지 않고 복사본(tools/.unity-mirror)에서 돌린다.
PROJECT="$REPO_ROOT/unity"
if pgrep -f "Unity.app/Contents/MacOS/Unity.*-projectPath $REPO_ROOT/unity( |\$)" >/dev/null; then
  PROJECT="$REPO_ROOT/tools/.unity-mirror"
  mkdir -p "$PROJECT"
  rsync -a --delete --exclude Temp --exclude Logs "$REPO_ROOT/unity/" "$PROJECT/"
  echo "에디터가 열려 있어 복사본에서 실행: $PROJECT"
fi

report_compile_errors() {
  local count
  # Unity 로그는 같은 오류를 여러 번 찍으므로 중복을 빼고 센다.
  count=$(grep -E "error CS[0-9]+" "$LOG" | sort -u | wc -l | tr -d ' ')
  if [ "$count" -gt 0 ]; then
    echo "컴파일 오류 ${count}건:"
    grep -E "error CS[0-9]+" "$LOG" | sort -u | sed 's/^/  /'
    return 1
  fi
  return 0
}

case "$MODE" in
  compile)
    "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$LOG"
    status=$?
    report_compile_errors || exit 1
    [ $status -eq 0 ] || { echo "Unity 종료 코드 $status (로그: $LOG)"; exit 1; }
    echo "컴파일 오류 0건"
    ;;

  shots)
    OUT="${2:-$REPO_ROOT/tools/.shots}"
    mkdir -p "$OUT"
    rm -f "$OUT"/*.png
    # 렌더링이 필요하므로 -nographics를 붙이지 않는다.
    "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOG" \
      -executeMethod FireGame.EditorTools.ScreenshotHarness.CaptureAll -shotDir "$OUT"
    status=$?
    report_compile_errors || exit 1
    grep -E "\[Harness\]" "$LOG" | sed 's/^/  /'
    # 실제 예외는 "XxxException: 메시지"로 찍힌다. 스택의 메서드 이름(EmitExceptionAsError 등)은 세지 않는다.
    exceptions=$(grep -cE "[A-Za-z]Exception: " "$LOG" || true)
    [ "$exceptions" -eq 0 ] || { echo "예외 ${exceptions}건 (로그: $LOG)"; grep -E "[A-Za-z]Exception: " -A2 "$LOG" | head -20; }
    [ $status -eq 0 ] || { echo "하네스 실패, 종료 코드 $status (로그: $LOG)"; exit 1; }
    ls "$OUT"/*.png
    ;;

  *)
    echo "사용법: $0 compile | shots [폴더]" >&2
    exit 2
    ;;
esac
