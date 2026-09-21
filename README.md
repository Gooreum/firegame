# FIREFIGHTER — 도스풍 소방관 어드벤처 (MVP)

불을 끄고 번 돈으로 장비를 사서 더 큰 불에 도전하는 모바일 게임.
320x200 · EGA 16색 · 8x8 도트 폰트.

| 스테이지 | 화재 | 필요한 장비 |
|---|---|---|
| 1 주택가 | 목재(A급) | 양동이(시작 장비) |
| 2 상가 | 목재 + 전기(C급) | CO2 소화기 500원 — 전기엔 물이 안 듣는다 |
| 3 주유소 | 목재 + 유류(B급) | 폼 소화기 10000원 — 유류에 물을 뿌리면 번진다 |

유류·전기 화재는 스스로 꺼지지 않는다. 맞는 약제를 사야만 끌 수 있고, 그게 장비를 사는 이유다.
물을 많이 뿌릴수록 수손 피해로 보상이 깎인다.

---

## 구조

```
core/FireGame.Core/          게임의 전부 (순수 C#, UnityEngine 의존성 0)
  Grid/   격자·재질·맵 로더
  Sim/    불 확산(셀룰러 오토마타)·진압·상성
  Game/   플레이어·장비·스테이지 진행·경제·세이브·화면 흐름(GameFlow)
  Data/   장비 4종·스테이지 3종
  Render/ 320x200 프레임버퍼·폰트·HUD·상점
core/FireGame.Core.Tests/    테스트 (xunit)
unity/Assets/Scripts/Unity/  Unity 레이어 3개 파일 — 입력 변환과 화면 출력만 한다
tools/unity-compile-check/   Unity 없이 Unity 스크립트를 컴파일해 보는 검사
sync-core.sh                 core → Unity 프로젝트 복사
```

게임 로직을 Unity 밖에 둔 이유: 불이 번지는 속도, 장비 위력, 보상 같은 숫자가 게임의 재미를 결정하는데,
이걸 Unity 컴파일을 기다리지 않고 `dotnet test`로 초 단위로 고치고 확인할 수 있다.

---

## 1. 코어 테스트 (Unity 불필요)

```bash
brew install dotnet
cd core
dotnet test
```

스테이지 × 장비 조합별 밸런스 표 보기 (자동 플레이 봇이 직접 플레이한다):

```bash
dotnet test --filter MeasureEveryStageAgainstEveryLoadout --logger "console;verbosity=detailed"
```

---

## 2. Unity에서 실행

### 준비
1. [Unity Hub](https://unity.com/download) 설치
2. Hub → Installs → **Unity 6 (6000.x) LTS** 설치. 모듈에서 **Android Build Support** 와/또는 **iOS Build Support** 체크

### 방법 A — 저장소의 `unity/` 폴더를 바로 열기 (권장)
```bash
./sync-core.sh
```
Hub → Projects → **Add → Add project from disk** → 이 저장소의 `unity` 폴더 선택 → Unity 6로 열기.

처음 열 때 Unity가 `ProjectSettings/`, `Packages/`를 만든다. 열리면 **Play** 를 누른다.
씬에 아무것도 배치할 필요가 없다 — 스크립트가 알아서 카메라와 화면을 만든다.

### 방법 B — Hub가 A를 거부하면
Hub → New project → **Universal 2D가 아니라 "2D (Built-In Render Pipeline)"** 템플릿으로 아무 곳에나 생성한 뒤:
```bash
./sync-core.sh /경로/새로만든프로젝트
```
Unity로 돌아가 **Play**.

Built-In을 쓰는 이유: 화면 전체가 스프라이트 한 장이라 URP가 줄 이득이 없고, 모바일에서 더 가볍다.

### 코어를 고쳤다면
`./sync-core.sh` 를 다시 실행하면 된다. `unity/Assets/Scripts/Core/`는 사본이라 직접 고치지 않는다.

### 입력 설정
Unity 6 신규 프로젝트의 기본 입력(새 Input System)과 구형 Input Manager 둘 다 지원하므로 따로 바꿀 필요가 없다.

### 폰에서 실행
File → Build Profiles → Android(또는 iOS) → Switch Platform → Player Settings에서
**Default Orientation = Landscape Left** 로 두고 Build And Run.

---

## 조작

| | 터치 | 키보드(에디터) |
|---|---|---|
| 이동 | 화면 왼쪽 절반을 누르고 드래그 | 방향키 / WASD |
| 발사 | 화면 오른쪽 절반을 누르고 있기 | Space |
| 장비 선택 | 하단 슬롯 탭 | 1 · 2 · 3 |
| 메뉴 | 항목 탭 | 마우스 클릭 |

조준 방향은 마지막으로 움직인 방향을 따른다.

**요령**: 양동이로 불꽃을 쫓으면 진다. 불이 아직 닿지 않은 칸을 먼저 적셔 길을 끊어라(젖은 칸은 파랗게 보인다).

---

## 검증 상태 — 정직하게

| 부분 | 상태 |
|---|---|
| 불 확산·진압·장비·경제·스테이지·세이브·화면 흐름·터치 판정 | ✅ 자동 테스트로 검증 |
| 3스테이지가 "맞는 장비로만 클리어 가능"한지 | ✅ 자동 플레이 봇으로 측정 |
| 화면 모양(도스풍 렌더) | ✅ 스크린샷으로 육안 확인 |
| Unity 스크립트 문법·C# 9 호환 | ✅ 스텁 컴파일 검사 (두 입력 방식 모두) |
| **Unity 에디터에서 실제 실행** | ⚠️ **미확인** — 개발 환경에 Unity가 없었다 |
| **폰 실기기 조작감** | ⚠️ **미확인** |

스텁 컴파일은 손으로 옮겨 적은 Unity API에 대고 하는 검사라, 실제 API와 다른 곳이 있으면 못 잡는다.
Unity에서 컴파일 오류가 나면 콘솔 메시지를 그대로 알려주면 된다.

### 알려진 한계
- 화면을 비율 유지로 늘리기 때문에 해상도에 따라 도트 크기가 한두 픽셀씩 고르지 않을 수 있다(정수배 스케일링은 아직 없음)
- 사운드 없음
- 스테이지 3개, 장비 4종
