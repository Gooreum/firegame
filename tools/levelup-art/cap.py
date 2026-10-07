"""승인 샘플(tools/levelup-art/index.html?cap=<id>)을 헤드리스 Chrome으로 돌려 프레임을 PNG로 쓴다(Unity 벤치와 나란히 비교용).
사용: python3 tools/levelup-art/cap.py <id> <출력 폴더> [fps=10] [width=640]  →  <출력>/<id>/f000.png …"""
import base64, html, io, os, re, subprocess, sys
from PIL import Image

CHROME = os.environ.get('CHROME', '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome')
here = os.path.dirname(os.path.abspath(__file__))
iid, out = sys.argv[1], sys.argv[2]
fps = int(sys.argv[3]) if len(sys.argv) > 3 else 10
w = int(sys.argv[4]) if len(sys.argv) > 4 else 640
url = f'file://{here}/index.html?cap={iid}&fps={fps}&w={w}'
dom = subprocess.run([CHROME, '--headless=new', '--disable-gpu', '--allow-file-access-from-files', '--virtual-time-budget=600000', '--dump-dom', url],
                     capture_output=True, text=True, timeout=900).stdout
m = re.search(r'<pre id="out"[^>]*>(.*?)</pre>', dom, re.S)
lines = [l for l in html.unescape(m.group(1)).splitlines() if l.startswith('data:')] if m else []
if not lines:
    sys.exit('프레임 0장: ' + dom[:300])
d = os.path.join(out, iid)
os.makedirs(d, exist_ok=True)
for i, l in enumerate(lines):
    Image.open(io.BytesIO(base64.b64decode(l.split(',', 1)[1]))).convert('RGB').save(os.path.join(d, f'f{i:03d}.png'))
print(f'{iid}: {len(lines)}장 → {d}')
