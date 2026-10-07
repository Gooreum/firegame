"""FIREGAME_FRAMES로 찍은 연속 캡처(tools/.shots-proto/<이름>/f###.png)를 <이름>.gif로 묶는다(사용자 확인용, 20fps)."""
import glob
import os
import sys

from PIL import Image


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '.shots-proto')
    width = int(sys.argv[2]) if len(sys.argv) > 2 else 720
    made = 0
    for d in sorted(glob.glob(os.path.join(root, '*/'))):
        files = sorted(glob.glob(os.path.join(d, 'f*.png')))
        if not files:
            continue
        frames = []
        for f in files:
            im = Image.open(f).convert('RGB')
            im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
            frames.append(im.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE))
        out = d.rstrip('/') + '.gif'
        frames[0].save(out, save_all=True, append_images=frames[1:], duration=50, loop=0, optimize=True)
        print(out, len(frames), '장')
        made += 1
    if made == 0:
        sys.exit('연속 캡처 폴더가 없다(FIREGAME_FRAMES로 찍었는지 확인)')


if __name__ == '__main__':
    main()
