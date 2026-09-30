#!/usr/bin/env bash
# 실제 Unity로 컴파일하고 화면을 찍는다. 에디터를 열지 않고 결과만 확인하는 도구.
#
#   tools/unity-check.sh compile          실제 Unity 컴파일. error CS가 있으면 실패
#   tools/unity-check.sh shots [폴더]      화면을 PNG로 찍는다(기본: tools/.shots)
#   tools/unity-check.sh proto-shots [폴더] 재미 검증 시험판 화면(기본: tools/.shots-proto)
#   tools/unity-check.sh ios [폴더]        시험판 C 아이폰용 Xcode 프로젝트(기본: unity/Builds/ios). 설치는 tools/ios-install.sh
#   tools/unity-check.sh urp-setup         URP 파이프라인 애셋을 만들고 그래픽·품질 설정에 꽂는다(결과는 커밋)
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

  proto-shots)
    # 재미 검증 시험판 화면(unity/Assets/Scripts/Prototypes). 시험판을 버리면 이 모드도 지운다.
    OUT="${2:-$REPO_ROOT/tools/.shots-proto}"
    mkdir -p "$OUT"
    rm -f "$OUT"/*.png
    "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOG" \
      -executeMethod FireGame.Prototypes.EditorTools.PrototypeShots.CaptureAll -shotDir "$OUT"
    status=$?
    report_compile_errors || exit 1
    grep -E "\[ProtoShots\]" "$LOG" | sed 's/^/  /'
    exceptions=$(grep -cE "[A-Za-z]Exception: " "$LOG" || true)
    [ "$exceptions" -eq 0 ] || { echo "예외 ${exceptions}건 (로그: $LOG)"; grep -E "[A-Za-z]Exception: " -A2 "$LOG" | head -20; }
    [ $status -eq 0 ] || { echo "시험판 캡처 실패, 종료 코드 $status (로그: $LOG)"; exit 1; }
    ls "$OUT"/*.png
    ;;

  ios)
    # 빌드 대상(iOS)과 PlayerSettings를 바꾸므로 늘 따로 둔 복사본(tools/.unity-ios)에서 돈다.
    # 실제 프로젝트·에디터 복사본은 그대로 Mac 대상으로 남는다. Library는 두고 가서 두 번째부터 빠르다.
    OUT="${2:-$REPO_ROOT/unity/Builds/ios}"
    PROJECT="$REPO_ROOT/tools/.unity-ios"
    mkdir -p "$PROJECT"
    rsync -a --delete --exclude Temp --exclude Logs --exclude Library --exclude Builds "$REPO_ROOT/unity/" "$PROJECT/"
    "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$LOG" -buildTarget iOS \
      -executeMethod FireGame.Prototypes.EditorTools.PrototypeBuild.BuildIOS -buildPath "$OUT"
    status=$?
    report_compile_errors || exit 1
    grep -E "\[ProtoBuild\]" "$LOG" | sed 's/^/  /'
    [ $status -eq 0 ] || { echo "iOS 빌드 실패, 종료 코드 $status (로그: $LOG)"; grep -E "error|Error" "$LOG" | grep -v "^$" | tail -15; exit 1; }
    echo "Xcode 프로젝트: $OUT/Unity-iPhone.xcodeproj"
    ;;

  urp-setup)
    "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$LOG" \
      -executeMethod FireGame.Prototypes.EditorTools.UrpSetup.Apply
    status=$?
    report_compile_errors || exit 1
    grep -E "\[UrpSetup\]" "$LOG" | sed 's/^/  /'
    [ $status -eq 0 ] || { echo "URP 설정 실패, 종료 코드 $status (로그: $LOG)"; exit 1; }
    # 복사본에서 돌았으면 만든 애셋·설정을 실제 프로젝트로 가져온다(에디터는 다시 열어야 반영된다).
    if [ "$PROJECT" != "$REPO_ROOT/unity" ]; then
      rsync -a "$PROJECT/Assets/Settings" "$REPO_ROOT/unity/Assets/"
      for f in UniversalRenderPipelineGlobalSettings.asset DefaultVolumeProfile.asset; do
        cp "$PROJECT/Assets/$f" "$PROJECT/Assets/$f.meta" "$REPO_ROOT/unity/Assets/"
      done
      cp "$PROJECT/ProjectSettings/ShaderGraphSettings.asset" "$REPO_ROOT/unity/ProjectSettings/" 2>/dev/null
      cp "$PROJECT/Assets/Settings.meta" "$PROJECT/Assets/Scripts/Prototypes/Editor/UrpSetup.cs.meta" "$REPO_ROOT/unity/Assets/" 2>/dev/null
      mv "$REPO_ROOT/unity/Assets/UrpSetup.cs.meta" "$REPO_ROOT/unity/Assets/Scripts/Prototypes/Editor/" 2>/dev/null
      cp "$PROJECT/ProjectSettings/GraphicsSettings.asset" "$PROJECT/ProjectSettings/QualitySettings.asset" "$REPO_ROOT/unity/ProjectSettings/"
      cp "$PROJECT/Packages/packages-lock.json" "$REPO_ROOT/unity/Packages/"
      echo "복사본 결과를 unity/로 가져왔다. 열린 에디터는 다시 열어야 URP가 반영된다."
    fi
    ;;

  *)
    echo "사용법: $0 compile | shots [폴더] | proto-shots [폴더] | ios [폴더] | urp-setup" >&2
    exit 2
    ;;
esac
