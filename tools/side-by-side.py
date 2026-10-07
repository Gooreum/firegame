"""샘플(왼쪽)과 Unity(오른쪽) 프레임을 같은 순번끼리 나란히 붙여 GIF와 레벨 순간 시트를 만든다.
두 쪽 모두 실제 시간 1/fps마다 찍었고 장면 0초에 시작한다(cap.py, PrototypeShots.SampleBenchShot).
사용: python3 tools/side-by-side.py <샘플 폴더> <Unity 폴더> <출력 접두어> [fps=10] [폭=640]
시트: 레벨 순간(샘플 AT[lv] + 1.2초, Lv6은 +2초) 6칸."""
import os, sys
from PIL import Image, ImageDraw

sdir, udir, out = sys.argv[1], sys.argv[2], sys.argv[3]
fps = int(sys.argv[4]) if len(sys.argv) > 4 else 10
w = int(sys.argv[5]) if len(sys.argv) > 5 else 640
h = w * 9 // 16


def frames(d):
    return [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith('.png')]


sf, uf = frames(sdir), frames(udir)
n = min(len(sf), len(uf))
if n == 0:
    sys.exit('프레임 없음')


def pair(i):
    a = Image.open(sf[min(i, len(sf) - 1)]).convert('RGB').resize((w, h))
    b = Image.open(uf[min(i, len(uf) - 1)]).convert('RGB').resize((w, h))
    im = Image.new('RGB', (w * 2 + 6, h + 22), (16, 18, 26))
    im.paste(a, (0, 22))
    im.paste(b, (w + 6, 22))
    d = ImageDraw.Draw(im)
    d.text((8, 4), f'SAMPLE  t={i / fps:.1f}s', fill=(255, 220, 120))
    d.text((w + 14, 4), 'UNITY', fill=(140, 210, 255))
    return im


q = [pair(i).quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE) for i in range(n)]
q[0].save(out + '.gif', save_all=True, append_images=q[1:], duration=int(1000 / fps), loop=0, optimize=True)
AT = [0, 0, 3.6, 7.0, 10.4, 13.8, 17.6]
DUR = 23.6
# 슬로모션만큼 실제 프레임이 늘어나므로 장면 시각 비율로 고른다.
idx = [min(n - 1, int((AT[lv] + (2.0 if lv == 6 else 1.2)) / DUR * n)) for lv in range(1, 7)]
sheet = Image.new('RGB', ((w * 2 + 6), (h + 22) * 6), (16, 18, 26))
for k, i in enumerate(idx):
    sheet.paste(pair(i), (0, k * (h + 22)))
sheet.save(out + '_sheet.jpg', quality=82)
print(out + '.gif', n, '장;', out + '_sheet.jpg')
