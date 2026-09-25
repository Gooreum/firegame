#!/usr/bin/env python3
"""
import-audio.py 결과를 검사한다. 문제가 있으면 0이 아닌 코드로 끝난다.

- 선별·합성 파일이 전부 있는지
- 합성 WAV가 깨지지 않았는지(클리핑 없음, 무음 아님)
- 불 루프가 정해진 길이이고 이음매가 튀지 않는지
- 게임 코드(GameAudio.cs)가 부르는 이름이 전부 파일로 있는지
- 라이선스가 있는지

사용법:  python3 tools/check-audio.py
"""
import hashlib
import os
import re
import sys
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))
audio = __import__("import-audio")

GAME_AUDIO = os.path.join(ROOT, "unity", "Assets", "Scripts", "Unity", "GameAudio.cs")


def read_wav(path):
    with wave.open(path, "rb") as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2, path
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.int32), w.getframerate()


def main():
    problems = []

    for name in audio.SELECTION:
        if not os.path.exists(os.path.join(audio.AUDIO, name + ".ogg")):
            problems.append(f"없음: {name}.ogg")

    for name in audio.synth_all():
        path = os.path.join(audio.AUDIO, name + ".wav")
        if not os.path.exists(path):
            problems.append(f"없음: {name}.wav")
            continue
        samples, rate = read_wav(path)
        peak = int(np.max(np.abs(samples)))
        sha = hashlib.sha1(open(path, "rb").read()).hexdigest()[:12]
        print(f"  {name}.wav  {len(samples) / rate:.2f}s  peak={peak}  sha1={sha}")
        if peak >= 32767:
            problems.append(f"클리핑: {name}.wav")
        if peak <= 1000:
            problems.append(f"거의 무음: {name}.wav (peak {peak})")

        if name == "fire_loop":
            length = len(samples) / rate
            seam = abs(int(samples[-1]) - int(samples[0])) / max(peak, 1)
            print(f"  fire_loop 길이 {length:.3f}s, 이음매 차이 {seam * 100:.2f}%")
            if abs(length - audio.FIRE_LOOP_SECONDS) > 0.001:
                problems.append(f"불 루프 길이 {length}")
            if seam >= 0.05:
                problems.append(f"불 루프 이음매가 튄다 ({seam * 100:.1f}%)")

    if os.path.exists(GAME_AUDIO):
        used = set(re.findall(r'"Audio/([a-z_0-9]+)"', open(GAME_AUDIO, encoding="utf-8").read()))
        files = {os.path.splitext(f)[0] for f in os.listdir(audio.AUDIO) if f.endswith((".ogg", ".wav"))}
        print(f"  GameAudio가 부르는 소리 {len(used)}개")
        for name in sorted(used - files):
            problems.append(f"GameAudio가 부르는데 파일이 없다: {name}")

    for pack in audio.PACKS:
        if not os.path.exists(os.path.join(audio.LICENSES, f"kenney-{pack}.txt")):
            problems.append(f"라이선스 없음: kenney-{pack}.txt")

    if problems:
        for p in problems:
            print("문제:", p)
        return 1
    print("통과")
    return 0


if __name__ == "__main__":
    sys.exit(main())
