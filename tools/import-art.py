#!/usr/bin/env python3
"""
Kenney CC0 에셋팩과 주아체(OFL)를 받아 Unity 프로젝트에 필요한 파일만 골라 넣는다.

- 팩 전체를 넣지 않는다. 쓰는 그림만 알아보기 쉬운 이름으로 복사한다.
- 방화복 5벌은 기본 소방관(Man Blue) 그림의 옷·머리 색을 바꿔 만든다(옷 색, 헬멧, 반사띠).
- 출동 지도 배경은 Map Pack 색감으로 바다·섬·길을 합성해 만든다.
  섬 가장자리 타일을 번호로 이어 붙이는 것보다 틀릴 여지가 적고, 현장 좌표와 길을 정확히 맞출 수 있다.
- 다시 실행해도 같은 결과가 나온다(선별 목록에 없는 파일은 지우고 .meta는 보존).

사용법:  python3 tools/import-art.py
필요:    Python 3 + Pillow
"""
import io
import os
import random
import shutil
import sys
import urllib.request
import zipfile

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(ROOT, "tools", ".art-cache")
ART = os.path.join(ROOT, "unity", "Assets", "Resources", "Art")
FONTS = os.path.join(ROOT, "unity", "Assets", "Resources", "Fonts")
LICENSES = os.path.join(ROOT, "unity", "Assets", "Licenses")

PACKS = {
    "top-down-shooter": "https://kenney.nl/media/pages/assets/top-down-shooter/230204340a-1677694684/kenney_top-down-shooter.zip",
    "map-pack": "https://kenney.nl/media/pages/assets/map-pack/86e20aaba9-1677662163/kenney_map-pack.zip",
    "ui-pack": "https://kenney.nl/media/pages/assets/ui-pack/f651646eab-1718203990/kenney_ui-pack.zip",
    "particle-pack": "https://kenney.nl/media/pages/assets/particle-pack/f8fe0f8cb8-1677578741/kenney_particle-pack.zip",
    "racing-pack": "https://kenney.nl/media/pages/assets/racing-pack/c4cd68480a-1677662443/kenney_racing-pack.zip",
}
FONT_URL = "https://github.com/google/fonts/raw/main/ofl/jua/Jua-Regular.ttf"
FONT_LICENSE_URL = "https://raw.githubusercontent.com/google/fonts/main/ofl/jua/OFL.txt"

TD = "top-down-shooter/PNG"
FX = "particle-pack/PNG (Transparent)"
UI = "ui-pack/PNG"

# (대상 폴더, 대상 이름) <- 팩 안의 경로
SELECTION = {
    # ---- 현장 바닥·사물 (Top-down Shooter, 64px 타일) ----
    ("TopDown", "grass_a"): f"{TD}/Tiles/tile_01.png",
    ("TopDown", "grass_b"): f"{TD}/Tiles/tile_02.png",
    ("TopDown", "dirt"): f"{TD}/Tiles/tile_05.png",
    ("TopDown", "floor_wood_a"): f"{TD}/Tiles/tile_43.png",
    ("TopDown", "floor_wood_b"): f"{TD}/Tiles/tile_44.png",
    ("TopDown", "floor_tile_a"): f"{TD}/Tiles/tile_08.png",
    ("TopDown", "floor_tile_b"): f"{TD}/Tiles/tile_09.png",
    ("TopDown", "dirt_b"): f"{TD}/Tiles/tile_06.png",
    ("TopDown", "floor_stone_a"): f"{TD}/Tiles/tile_07.png",
    ("TopDown", "floor_stone_b"): f"{TD}/Tiles/tile_11.png",
    ("TopDown", "brick_a"): f"{TD}/Tiles/tile_15.png",
    ("TopDown", "brick_b"): f"{TD}/Tiles/tile_16.png",
    ("TopDown", "oil_puddle"): f"{TD}/Tiles/tile_320.png",
    ("TopDown", "electric_panel"): f"{TD}/Tiles/tile_296.png",
    ("TopDown", "door_vertical"): f"{TD}/Tiles/tile_441.png",
    ("TopDown", "door_horizontal"): f"{TD}/Tiles/tile_466.png",
    ("TopDown", "hydrant"): f"{TD}/Tiles/tile_316.png",
    # ---- 사람 ----
    ("TopDown", "civilian_woman"): f"{TD}/Woman Green/womanGreen_stand.png",
    ("TopDown", "civilian_old"): f"{TD}/Man Old/manOld_stand.png",
    ("TopDown", "civilian_man"): f"{TD}/Man Brown/manBrown_stand.png",
    # ---- 효과 (Particle Pack, 흰색이라 색을 입혀 쓴다) ----
    **{("Effects", f"flame_0{i}"): f"{FX}/flame_0{i}.png" for i in range(1, 7)},
    ("Effects", "fire_01"): f"{FX}/fire_01.png",
    ("Effects", "fire_02"): f"{FX}/fire_02.png",
    **{("Effects", f"smoke_0{i}"): f"{FX}/smoke_0{i}.png" for i in range(1, 6)},
    **{("Effects", f"scorch_0{i}"): f"{FX}/scorch_0{i}.png" for i in range(1, 4)},
    **{("Effects", f"spark_0{i}"): f"{FX}/spark_0{i}.png" for i in range(1, 5)},
    ("Effects", "water_drop"): f"{FX}/circle_05.png",
    ("Effects", "water_trace"): f"{FX}/trace_01.png",
    ("Effects", "glow"): f"{FX}/light_01.png",
    # ---- 지도 장식 (Map Pack) ----
    ("Map", "tree_pine"): "map-pack/PNG/mapTile_040.png",
    ("Map", "tree_round"): "map-pack/PNG/mapTile_055.png",
    ("Map", "bush"): "map-pack/PNG/mapTile_115.png",
    ("Map", "rock"): "map-pack/PNG/mapTile_039.png",
    ("Map", "node_1"): "map-pack/PNG/mapTile_131.png",
    ("Map", "node_2"): "map-pack/PNG/mapTile_132.png",
    ("Map", "node_3"): "map-pack/PNG/mapTile_133.png",
    ("Map", "node_4"): "map-pack/PNG/mapTile_134.png",
    ("Map", "node_5"): "map-pack/PNG/mapTile_135.png",
    ("Map", "node_6"): "map-pack/PNG/mapTile_148.png",
    # ---- 차량 (Racing Pack) ----
    ("Vehicles", "firetruck"): "racing-pack/PNG/Cars/car_red_1.png",
    ("Vehicles", "cone"): "racing-pack/PNG/Objects/cone_straight.png",
    # ---- UI (UI Pack) ----
    ("UI", "button_blue"): f"{UI}/Blue/Default/button_rectangle_depth_flat.png",
    ("UI", "button_blue_round"): f"{UI}/Blue/Default/button_round_depth_flat.png",
    ("UI", "button_yellow"): f"{UI}/Yellow/Default/button_rectangle_depth_flat.png",
    ("UI", "button_yellow_round"): f"{UI}/Yellow/Default/button_round_depth_flat.png",
    ("UI", "button_red"): f"{UI}/Red/Default/button_rectangle_depth_flat.png",
    ("UI", "button_red_round"): f"{UI}/Red/Default/button_round_depth_flat.png",
    ("UI", "button_green"): f"{UI}/Green/Default/button_rectangle_depth_flat.png",
    ("UI", "button_grey"): f"{UI}/Grey/Default/button_rectangle_depth_flat.png",
    ("UI", "panel_grey"): f"{UI}/Grey/Default/button_square_depth_flat.png",
    ("UI", "panel_blue"): f"{UI}/Blue/Default/button_square_depth_flat.png",
    ("UI", "star"): f"{UI}/Yellow/Default/star.png",
    ("UI", "star_empty"): f"{UI}/Grey/Default/star_outline_depth.png",
    ("UI", "bar_fill"): f"{UI}/Green/Default/slide_horizontal_color.png",
    ("UI", "bar_back"): f"{UI}/Grey/Default/slide_horizontal_grey.png",
    ("UI", "icon_play"): f"{UI}/Extra/Default/icon_play_light.png",
    ("UI", "icon_repeat"): f"{UI}/Extra/Default/icon_repeat_light.png",
    ("UI", "icon_lock"): f"{UI}/Grey/Default/icon_square.png",
}

# 지도 합성 팔레트 (Map Pack 타일에서 추출)
WATER = (166, 225, 245)
WATER_LIGHT = (184, 235, 252)
GRASS = (46, 204, 113)
GRASS_DARK = (40, 180, 99)
CLIFF = (215, 151, 101)
CLIFF_DARK = (176, 123, 81)
PATH = (255, 204, 0)
PATH_EDGE = (255, 255, 255)

# 방화복 레벨별 (옷, 헬멧, 반사띠). core GearStats.SuitName과 같은 순서.
# Lv0 근무복은 원본 그대로다.
SUITS = [
    None,                                              # 근무복
    ((214, 170, 80), (250, 206, 40), (235, 245, 120)),  # 방화복: 황갈색 + 노란 헬멧
    ((240, 120, 30), (220, 40, 40), (250, 250, 210)),   # 고급 방화복: 주황 + 빨간 헬멧
    ((200, 40, 40), (245, 245, 245), (250, 230, 90)),   # 특수 방화복: 빨강 + 흰 헬멧
    ((190, 195, 205), (215, 220, 230), (245, 248, 255)),  # 방열복: 은색
]
SUIT_SOURCE = f"{TD}/Man Blue/manBlue_hold.png"

# 현장 지도 좌표. core Campaign.cs의 MapX/MapY와 같아야 길이 노드에 닿는다.
STATION = (0.10, 0.16)
MISSIONS = [(0.22, 0.30), (0.50, 0.62), (0.78, 0.35), (0.88, 0.66), (0.64, 0.86), (0.36, 0.80)]


def download(url):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, os.path.basename(url.split("?")[0]))
    if not os.path.exists(path):
        print("  받는 중:", url)
        with urllib.request.urlopen(url, timeout=180) as r, open(path, "wb") as f:
            shutil.copyfileobj(r, f)
    return path


def load_packs():
    packs = {}
    for name, url in PACKS.items():
        packs[name] = zipfile.ZipFile(download(url))
    return packs


def read(packs, rel):
    pack, inner = rel.split("/", 1)
    z = packs[pack]
    for entry in z.namelist():
        if entry.endswith(inner):
            return z.read(entry)
    raise SystemExit(f"팩 안에서 파일을 찾지 못했다: {rel}")


def sync_dir(target, wanted):
    """target 폴더에서 wanted(파일 이름 집합)에 없는 PNG와 그 .meta를 지운다."""
    if not os.path.isdir(target):
        return
    for f in os.listdir(target):
        base = f[:-5] if f.endswith(".meta") else f
        if base.endswith(".png") and base not in wanted:
            os.remove(os.path.join(target, f))


def copy_selection(packs):
    wanted = {}
    for (folder, name), rel in SELECTION.items():
        wanted.setdefault(folder, set()).add(name + ".png")
        dest_dir = os.path.join(ART, folder)
        os.makedirs(dest_dir, exist_ok=True)
        data = read(packs, rel)
        dest = os.path.join(dest_dir, name + ".png")
        if not os.path.exists(dest) or open(dest, "rb").read() != data:
            with open(dest, "wb") as f:
                f.write(data)
    wanted.setdefault("Map", set()).add("map_background.png")
    for level in range(len(SUITS)):
        wanted.setdefault("TopDown", set()).add(f"player_suit_{level}.png")
    for folder, names in wanted.items():
        sync_dir(os.path.join(ART, folder), names)
    return sum(len(v) for v in wanted.values())


def _lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def _shade(color, ratio):
    return tuple(max(0, min(255, int(v * ratio))) for v in color)


def recolor_suit(src, suit, helmet, stripe):
    """파란 옷 → 옷 색(등에 반사띠), 검은 머리 → 헬멧. 원래 명암 비율을 살려 입체감을 유지한다."""
    img = src.copy()
    px = img.load()
    w, h = img.size
    blue_ref = _lum((47, 149, 208))
    hair_ref = _lum((51, 51, 51))

    def is_cloth(r, g, b):
        return b > r + 40 and b >= g

    def is_hair(r, g, b):
        return abs(r - g) < 8 and abs(g - b) < 8 and r < 80

    hair = {(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0 and is_hair(*px[x, y][:3])}
    stripe_from, stripe_to = int(w * 0.20), int(w * 0.26)

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if is_cloth(r, g, b):
                near_head = any((x + dx, y + dy) in hair for dx in (-1, 0, 1) for dy in (-1, 0, 1))
                if near_head:
                    px[x, y] = _shade(helmet, 0.7) + (a,)                        # 헬멧 챙
                elif stripe_from <= x <= stripe_to:
                    px[x, y] = _shade(stripe, _lum((r, g, b)) / blue_ref) + (a,)  # 반사띠
                else:
                    px[x, y] = _shade(suit, _lum((r, g, b)) / blue_ref) + (a,)
            elif (x, y) in hair:
                px[x, y] = _shade(helmet, 0.75 + 0.25 * _lum((r, g, b)) / hair_ref) + (a,)
    return img


def make_suits(packs):
    src = Image.open(io.BytesIO(read(packs, SUIT_SOURCE))).convert("RGBA")
    for level, colors in enumerate(SUITS):
        img = src if colors is None else recolor_suit(src, *colors)
        buf = io.BytesIO()
        img.save(buf, "PNG", optimize=True)
        dest = os.path.join(ART, "TopDown", f"player_suit_{level}.png")
        if not os.path.exists(dest) or open(dest, "rb").read() != buf.getvalue():
            with open(dest, "wb") as f:
                f.write(buf.getvalue())
    return len(SUITS)


def to_px(point, w, h):
    # 지도 좌표는 왼쪽 아래 원점(Unity 방식), 이미지는 왼쪽 위 원점이다.
    return int(point[0] * w), int((1 - point[1]) * h)


def compose_map(packs):
    w, h = 1920, 1080
    rng = random.Random(7)
    img = Image.new("RGB", (w, h), WATER)
    d = ImageDraw.Draw(img)

    # 잔물결
    for _ in range(140):
        x, y = rng.randrange(w), rng.randrange(h)
        d.arc((x, y, x + 36, y + 14), 200, 340, fill=WATER_LIGHT, width=4)

    # 섬: 여러 덩어리를 겹쳐 Map Pack처럼 울퉁불퉁한 윤곽을 만든다.
    # 절벽(아래로 밀린 두 겹) → 잔디 순서로 같은 모양을 세 번 그린다.
    island = (90, 70, w - 90, h - 110)
    blobs = [
        ("rect", (150, 120, w - 160, h - 170), 150),
        ("ellipse", (90, 330, 620, h - 110), 0),
        ("ellipse", (560, 60, 1320, 520), 0),
        ("ellipse", (1260, 200, w - 90, 820), 0),
        ("ellipse", (700, 560, 1500, h - 120), 0),
    ]

    def draw_island(offset, color):
        for kind, (x0, y0, x1, y1), radius in blobs:
            box = (x0, y0 + offset, x1, y1 + offset)
            if kind == "rect":
                d.rounded_rectangle(box, radius, fill=color)
            else:
                d.ellipse(box, fill=color)

    draw_island(34, CLIFF_DARK)
    draw_island(18, CLIFF)
    draw_island(0, GRASS)
    for _ in range(120):
        x, y = rng.randrange(island[0] + 60, island[2] - 60), rng.randrange(island[1] + 60, island[3] - 60)
        if img.getpixel((x, y)) == GRASS and img.getpixel((x + 18, y + 10)) == GRASS:
            d.ellipse((x, y, x + 18, y + 10), fill=GRASS_DARK)

    # 길: 소방서 → 현장 1 → … → 6으로 섬을 한 바퀴 돈다. 곧은 선이면 현장 1이 소방서→현장 2
    # 직선 위에 묻혀 갈림길처럼 보이지 않으므로 구간마다 옆으로 휘게 그린다.
    points = [to_px(p, w, h) for p in [STATION] + MISSIONS]
    curve = []
    bends = [140, -160, 130, -120, 110, -100]
    for i, ((ax, ay), (bx, by)) in enumerate(zip(points, points[1:])):
        mx, my = (ax + bx) / 2, (ay + by) / 2
        nx, ny = -(by - ay), (bx - ax)
        length = max((nx * nx + ny * ny) ** 0.5, 1)
        cx, cy = mx + nx / length * bends[i], my + ny / length * bends[i]
        for t in range(0, 41):
            t /= 40
            x = (1 - t) ** 2 * ax + 2 * (1 - t) * t * cx + t * t * bx
            y = (1 - t) ** 2 * ay + 2 * (1 - t) * t * cy + t * t * by
            curve.append((x, y))
    for width, color in ((46, PATH_EDGE), (32, PATH)):
        d.line(curve, fill=color, width=width, joint="curve")
        for (x, y) in points:
            r = width // 2 + 6
            d.ellipse((x - r, y - r, x + r, y + r), fill=color)

    # 장식: 길과 노드에서 떨어진 곳에만 나무·덤불·바위
    decor = [Image.open(io.BytesIO(read(packs, SELECTION[("Map", n)]))).convert("RGBA")
             for n in ("tree_pine", "tree_round", "bush", "rock", "tree_pine")]

    def near_path(x, y):
        for (px, py) in points:
            if (px - x) ** 2 + (py - y) ** 2 < 150 ** 2:
                return True
        for (sx, sy) in curve:
            if (sx - x) ** 2 + (sy - y) ** 2 < 80 ** 2:
                return True
        return False

    def on_island(x, y):
        return img.getpixel((min(max(x, 0), w - 1), min(max(y, 0), h - 1))) in (GRASS, GRASS_DARK)

    placed = 0
    for _ in range(600):
        if placed >= 46:
            break
        x, y = rng.randrange(island[0] + 70, island[2] - 90), rng.randrange(island[1] + 50, island[3] - 110)
        if near_path(x + 32, y + 32):
            continue
        if not (on_island(x, y) and on_island(x + 64, y + 64) and on_island(x + 64, y) and on_island(x, y + 64)):
            continue
        sprite = decor[rng.randrange(len(decor))]
        scale = rng.choice((1.0, 1.25, 1.5))
        s = sprite.resize((int(sprite.width * scale), int(sprite.height * scale)), Image.LANCZOS)
        img.paste(s, (x, y), s)
        placed += 1

    out = os.path.join(ART, "Map", "map_background.png")
    img.save(out, optimize=True)
    return out


def copy_font_and_licenses(packs):
    os.makedirs(FONTS, exist_ok=True)
    os.makedirs(LICENSES, exist_ok=True)
    shutil.copyfile(download(FONT_URL), os.path.join(FONTS, "Jua-Regular.ttf"))
    shutil.copyfile(download(FONT_LICENSE_URL), os.path.join(LICENSES, "Jua-OFL.txt"))
    for name, z in packs.items():
        lic = [e for e in z.namelist() if os.path.basename(e).lower() == "license.txt"]
        if not lic:
            raise SystemExit(f"{name} 팩에 License.txt가 없다")
        with open(os.path.join(LICENSES, f"kenney-{name}.txt"), "wb") as f:
            f.write(z.read(lic[0]))


def main():
    packs = load_packs()
    count = copy_selection(packs)
    count += make_suits(packs)
    background = compose_map(packs)
    copy_font_and_licenses(packs)
    print(f"완료: 스프라이트 {count}개, 지도 배경 {os.path.relpath(background, ROOT)}")


if __name__ == "__main__":
    sys.exit(main())
