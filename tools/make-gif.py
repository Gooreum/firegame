#!/usr/bin/env python3
"""플레이 장면 프레임(frame_000.png…)을 README용 반복 GIF로 묶는다.

사용: python3 tools/make-gif.py <프레임 폴더> <출력.gif> [--width 720] [--fps 12]
프레임은 tools/unity-check.sh clip 으로 찍는다. 모든 프레임이 같은 팔레트(첫·가운데·끝 프레임에서 뽑은 256색)를 써서
장면이 바뀌어도 색이 깜빡이지 않는다.
"""
import argparse
import glob
import os
import sys

from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("out")
    ap.add_argument("--width", type=int, default=720)
    ap.add_argument("--fps", type=int, default=12)
    args = ap.parse_args()

    paths = sorted(glob.glob(os.path.join(args.frames, "frame_*.png")))
    if not paths:
        sys.exit("프레임이 없다: " + args.frames)
    first = Image.open(paths[0])
    height = round(first.height * args.width / first.width)
    frames = [Image.open(p).convert("RGB").resize((args.width, height), Image.LANCZOS) for p in paths]

    # 공용 팔레트: 대표 프레임 셋을 한 장에 붙여 256색으로 줄인다.
    picks = [frames[0], frames[len(frames) // 2], frames[-1]]
    sheet = Image.new("RGB", (args.width, height * len(picks)))
    for i, f in enumerate(picks):
        sheet.paste(f, (0, height * i))
    palette = sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    quantized = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    quantized[0].save(args.out, save_all=True, append_images=quantized[1:], duration=round(1000 / args.fps), loop=0, optimize=True)
    size = os.path.getsize(args.out)
    print(f"{args.out}: {len(frames)}장, {args.width}x{height}, {args.fps}fps, {size / 1024 / 1024:.1f}MB")


if __name__ == "__main__":
    main()
