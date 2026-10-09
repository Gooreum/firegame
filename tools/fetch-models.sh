#!/usr/bin/env bash
# 5단계 3D 모델(전부 CC0)을 받아 unity/Assets/Resources/Models/ 아래에 둔다. 받은 파일은 저장소에 커밋한다.
#
#   Houses  Kenney City Kit Suburban   https://kenney.nl/assets/city-kit-suburban
#   Cars    Kenney Car Kit             https://kenney.nl/assets/car-kit
#   Nature  Kenney Nature Kit          https://kenney.nl/assets/nature-kit
#   Industrial  Kenney City Kit Industrial  https://kenney.nl/assets/city-kit-industrial
#   People  Quaternius Ultimate Animated Character Pack  https://quaternius.com/packs/ultimatedanimatedcharacter.html
#
#   Kenney 디자인 패스(2026-10-10, docs §25) — 코드로 쌓던 가게·숲·항구·야시장 건물과 몹을 Kenney 모델로:
#   Commercial  City Kit Commercial  https://kenney.nl/assets/city-kit-commercial   마을 가게 몸체
#   Market      Mini Market          https://kenney.nl/assets/mini-market           가게 마당(냉동고·카트·진열대)
#   Survival    Survival Kit         https://kenney.nl/assets/survival-kit          숲 캠프·마당·제재소
#   Fantasy     Fantasy Town Kit     https://kenney.nl/assets/fantasy-town-kit      숲 집 벽·지붕, 야시장 좌판
#   Watercraft  Watercraft Kit       https://kenney.nl/assets/watercraft-kit        항구 배·부표·컨테이너
#   Food        Food Kit             https://kenney.nl/assets/food-kit              가게 지붕 상징물, 야시장 진열
#   Pets        Cube Pets            https://kenney.nl/assets/cube-pets             몹(불에 홀린 동물 요괴, idle·walk·run 클립)
#
# 다시 돌리면 같은 파일을 덮어쓴다.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$REPO_ROOT/unity/Assets/Resources/Models"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

HOUSES="building-type-a building-type-c building-type-e building-type-h building-type-k building-type-m building-type-p building-type-t"
CARS="sedan suv taxi van hatchback-sports firetruck"
NATURE="tree_default tree_oak tree_fat tree_pineRoundA tree_pineTallA tree_cone plant_bush plant_bushLarge rock_largeA rock_smallB"
INDUSTRIAL="building-b building-c building-e building-f building-k building-l building-m building-r detail-tank detail-tank-large shipping-container-a shipping-container-b shipping-container-c water-tower"
# 이름=구글 드라이브 파일 ID (Quaternius FBX 폴더)
PEOPLE="Worker_Male=1rS4HxTBJKur-T_BZRDsvv68J2zr278wn Worker_Female=1-l5q7N7Q7ggdALBt_FfZszpf2otp3-QH Casual_Male=1Ls3XUkHqIcz3HL9JaW-dNWNnLiQLdkrs Casual_Female=1AYI79-CtvncVV0DW0Mg6_9hRKpX8Y1Pe OldClassy_Male=1mPqpDpTXYlFNZEVaf2rx39_o65k2-AhW"

# kenney <폴더> <zip 주소> <모델 이름들> [텍스처 있음]
kenney() {
  local folder="$1" url="$2" names="$3" textured="${4:-}"
  local zip="$WORK/$folder.zip" out="$WORK/$folder"
  curl -sfL "$url" -o "$zip"
  mkdir -p "$out" && unzip -qo "$zip" -d "$out"
  mkdir -p "$DEST/$folder"
  for n in $names; do
    cp "$out/Models/FBX format/$n.fbx" "$DEST/$folder/"
  done
  if [ -n "$textured" ]; then
    mkdir -p "$DEST/$folder/Textures"
    cp "$out/Models/FBX format/Textures/colormap.png" "$DEST/$folder/Textures/"
  fi
  cp "$out/License.txt" "$DEST/$folder/License.txt"
  echo "$folder: $(ls "$DEST/$folder"/*.fbx | wc -l | tr -d ' ')개"
}

kenney Houses https://kenney.nl/media/pages/assets/city-kit-suburban/2c871b7af2-1745479373/kenney_city-kit-suburban_20.zip "$HOUSES" textured
kenney Cars https://kenney.nl/media/pages/assets/car-kit/1a312ec241-1775131960/kenney_car-kit.zip "$CARS" textured
kenney Nature https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip "$NATURE"
kenney Industrial https://kenney.nl/media/pages/assets/city-kit-industrial/0ec35b139d-1788171848/kenney_city-kit-industrial_2.0.zip "$INDUSTRIAL" textured

COMMERCIAL="building-a building-b building-c building-d building-e building-g building-h building-j building-k building-n detail-parasol-a"
MARKET="shopping-cart freezer display-fruit display-bread"
SURVIVAL="tent tent-canvas campfire-pit campfire-stand tree-log tree-log-small resource-wood resource-planks workbench workbench-grind barrel box-large box signpost bucket"
FANTASY="wall-wood wall-wood-door wall-wood-window-shutters wall wall-door wall-window-shutters roof-gable roof-high-gable roof-flat roof-point chimney pillar-wood planks stall stall-red stall-green stall-bench stall-stool lantern"
WATERCRAFT="boat-fishing-small boat-house-a boat-sail-a boat-tug-a boat-row-small ship-cargo-a buoy-flag cargo-container-a cargo-container-b cargo-container-c cargo-pile-a"
FOOD="loaf loaf-baguette turkey cup-coffee whole-ham bowl-broth apple orange banana watermelon pancakes skewer soda-bottle can"
PETS="animal-chick animal-tiger animal-beaver animal-monkey animal-koala animal-polar animal-lion animal-fox animal-bee animal-hog animal-bunny animal-crab animal-parrot"

kenney Commercial https://kenney.nl/media/pages/assets/city-kit-commercial/a742d900eb-1753115042/kenney_city-kit-commercial_2.1.zip "$COMMERCIAL" textured
kenney Market https://kenney.nl/media/pages/assets/mini-market/463f38da51-1729865423/kenney_mini-market.zip "$MARKET" textured
kenney Survival https://kenney.nl/media/pages/assets/survival-kit/4065a8185b-1712149243/kenney_survival-kit.zip "$SURVIVAL" textured
kenney Fantasy https://kenney.nl/media/pages/assets/fantasy-town-kit/efe948d309-1754222374/kenney_fantasy-town-kit_2.0.zip "$FANTASY" textured
kenney Watercraft https://kenney.nl/media/pages/assets/watercraft-kit/a335cfed49-1713519620/kenney_watercraft-pack.zip "$WATERCRAFT" textured
kenney Food https://kenney.nl/media/pages/assets/food-kit/83086fa91c-1719418518/kenney_food-kit.zip "$FOOD" textured
kenney Pets https://kenney.nl/media/pages/assets/cube-pets/$(curl -sfL -A "Mozilla/5.0" https://kenney.nl/assets/cube-pets | grep -oE 'media/pages/assets/cube-pets/[^"]+\.zip' | head -1 | sed 's#media/pages/assets/cube-pets/##') "$PETS" textured

mkdir -p "$DEST/People"
for pair in $PEOPLE; do
  name="${pair%%=*}"
  id="${pair#*=}"
  curl -sfL "https://drive.usercontent.google.com/download?id=$id&export=download&confirm=t" -o "$DEST/People/$name.fbx"
  head -c 18 "$DEST/People/$name.fbx" | grep -q "Kaydara FBX" || { echo "People/$name.fbx가 FBX가 아니다(드라이브 응답 확인)" >&2; exit 1; }
done
cat > "$DEST/People/License.txt" <<'EOF'
Ultimate Animated Character Pack by Quaternius (https://quaternius.com)
License: CC0 1.0 Universal (public domain). Free for personal and commercial use.
EOF
echo "People: $(ls "$DEST/People"/*.fbx | wc -l | tr -d ' ')개"
