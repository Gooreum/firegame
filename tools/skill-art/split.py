"""bake.html을 헤드리스 Chrome --dump-dom으로 연 결과(표준 입력)에서 'name|data:image/png;base64,...' 줄을 PNG로 쓴다."""
import base64
import html
import os
import re
import sys


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    dom = sys.stdin.read()
    m = re.search(r'<pre id="out">(.*?)</pre>', dom, re.S)
    if not m:
        sys.exit('bake.html 결과를 찾지 못했다(<pre id="out"> 없음)')
    n = 0
    for line in html.unescape(m.group(1)).splitlines():
        if '|' not in line:
            continue
        name, url = line.split('|', 1)
        data = base64.b64decode(url.split(',', 1)[1])
        with open(os.path.join(out_dir, name + '.png'), 'wb') as f:
            f.write(data)
        n += 1
    if n == 0:
        sys.exit('구운 스프라이트가 0장이다')
    print(f'{n}장 → {out_dir}')


if __name__ == '__main__':
    main()
