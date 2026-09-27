#!/usr/bin/env bash
# 시험판 C를 USB(또는 같은 와이파이)로 연결된 아이폰에 빌드·설치·실행한다.
#
#   tools/ios-install.sh            Unity 빌드 → Xcode 서명·빌드 → 설치 → 실행
#   tools/ios-install.sh --no-unity Unity 빌드는 건너뛰고 이미 있는 Xcode 프로젝트로 설치만
#
# 필요한 것: Unity iOS 모듈, Xcode에 로그인한 개발자 팀(LHW4ZX343L), 개발자 모드를 켠 아이폰(잠금 해제).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEAM="LHW4ZX343L"
BUNDLE="com.mingu.firegame.proto"
OUT="$REPO_ROOT/unity/Builds/ios"
DERIVED="$REPO_ROOT/unity/Builds/ios-derived"

# 1) 연결된 아이폰부터 찾는다(없으면 몇 분짜리 빌드를 하기 전에 멈춘다).
DEVICES_JSON="$(mktemp)"
xcrun devicectl list devices --json-output "$DEVICES_JSON" >/dev/null 2>&1 || true
read -r DEVICE_ID UDID NAME < <(python3 - "$DEVICES_JSON" <<'PY'
import json, sys
try:
    devices = json.load(open(sys.argv[1]))["result"]["devices"]
except Exception:
    devices = []
for d in devices:
    hw, conn = d.get("hardwareProperties", {}), d.get("connectionProperties", {})
    if hw.get("platform") == "iOS" and conn.get("pairingState") == "paired" and conn.get("tunnelState") != "unavailable":
        print(d["identifier"], hw.get("udid", ""), d.get("deviceProperties", {}).get("name", "iPhone").replace(" ", "_"))
        break
PY
) || true
rm -f "$DEVICES_JSON"
if [ -z "${DEVICE_ID:-}" ] || [ -z "${UDID:-}" ]; then
  echo "연결된 아이폰이 없다. 케이블로 연결하고 잠금을 푼 뒤 다시 실행한다." >&2
  exit 1
fi
echo "아이폰: ${NAME//_/ } ($UDID)"

# 2) Unity → Xcode 프로젝트
if [ "${1:-}" != "--no-unity" ]; then
  "$REPO_ROOT/tools/unity-check.sh" ios "$OUT"
fi
[ -d "$OUT/Unity-iPhone.xcodeproj" ] || { echo "Xcode 프로젝트가 없다: $OUT" >&2; exit 1; }

# 3) 서명·빌드. 자동 서명이 이 폰을 팀에 등록하고 프로비저닝 프로필을 만든다.
echo "Xcode 빌드 중..."
XCODE_LOG="$(mktemp -t ios-xcodebuild).log"
if ! xcodebuild -project "$OUT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
    -destination "id=$UDID" -derivedDataPath "$DERIVED" -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic build >"$XCODE_LOG" 2>&1; then
  echo "Xcode 빌드 실패 (로그: $XCODE_LOG)" >&2
  grep -E "error:|Signing|provisioning" "$XCODE_LOG" | sort -u | head -20 >&2
  exit 1
fi
APP="$(ls -d "$DERIVED"/Build/Products/Release-iphoneos/*.app | head -1)"
echo "빌드됨: $APP"

# 4) 설치·실행
xcrun devicectl device install app --device "$DEVICE_ID" "$APP" >/dev/null
echo "설치됨"
xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE" >/dev/null
echo "실행됨: 폰을 가로로 눕혀서 해 보세요"
