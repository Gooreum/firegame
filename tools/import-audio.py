#!/usr/bin/env python3
"""
효과음을 Unity 프로젝트에 넣는다.

- Kenney CC0 오디오 팩(impact / interface)에서 쓰는 소리만 알아보기 쉬운 이름으로 복사한다.
- Kenney에 없는 소리(불 타는 소리, 분사, "칙", 역효과, 발화, 승리·패배 징글)는 numpy로 합성한다.
  시드가 고정이라 다시 실행해도 바이트 단위로 같은 파일이 나온다.
- 선별 목록에 없는 소리 파일은 지운다(.meta는 보존).

사용법:  python3 tools/import-audio.py
필요:    Python 3 + numpy + scipy
"""
import io
import os
import shutil
import sys
import urllib.request
import wave
import zipfile

import numpy as np
from scipy import signal

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(ROOT, "tools", ".art-cache")
AUDIO = os.path.join(ROOT, "unity", "Assets", "Resources", "Audio")
LICENSES = os.path.join(ROOT, "unity", "Assets", "Licenses")

PACKS = {
    "impact-sounds": "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
    "interface-sounds": "https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip",
}

# 대상 이름 <- 팩 안의 경로
SELECTION = {
    "pickup": "interface-sounds/Audio/confirmation_001.ogg",
    "rescued": "interface-sounds/Audio/confirmation_004.ogg",
    "civ_lost": "interface-sounds/Audio/error_006.ogg",
    "critical": "interface-sounds/Audio/error_004.ogg",
    "collapse_0": "impact-sounds/Audio/impactWood_heavy_000.ogg",
    "collapse_1": "impact-sounds/Audio/impactWood_heavy_001.ogg",
    "collapse_2": "impact-sounds/Audio/impactWood_heavy_002.ogg",
    "door_jam": "impact-sounds/Audio/impactPlank_medium_000.ogg",
}

RATE = 44100
FIRE_LOOP_SECONDS = 4.0


# ---------------------------------------------------------------------------
# Kenney 팩
# ---------------------------------------------------------------------------

def download(url):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, os.path.basename(url.split("?")[0]))
    if not os.path.exists(path):
        print("  받는 중:", url)
        # kenney.nl은 기본 urllib 사용자 에이전트를 막는다.
        request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(request, timeout=180) as r, open(path, "wb") as f:
            shutil.copyfileobj(r, f)
    return path


def read(packs, rel):
    pack, inner = rel.split("/", 1)
    z = packs[pack]
    for entry in z.namelist():
        if entry.endswith(inner):
            return z.read(entry)
    raise SystemExit(f"팩 안에서 파일을 찾지 못했다: {rel}")


def write_if_changed(path, data):
    if not os.path.exists(path) or open(path, "rb").read() != data:
        with open(path, "wb") as f:
            f.write(data)


# ---------------------------------------------------------------------------
# 합성
# ---------------------------------------------------------------------------

def seconds(n):
    return np.arange(int(RATE * n)) / RATE


def band(x, low, high, order=4):
    sos = signal.butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, cutoff, order=4):
    sos = signal.butter(order, cutoff, btype="lowpass", fs=RATE, output="sos")
    return signal.sosfilt(sos, x)


def highpass(x, cutoff, order=4):
    sos = signal.butter(order, cutoff, btype="highpass", fs=RATE, output="sos")
    return signal.sosfilt(sos, x)


def envelope(t, attack, decay):
    """빠르게 올라갔다 지수로 사라지는 모양."""
    rise = np.clip(t / max(attack, 1e-6), 0.0, 1.0)
    return rise * np.exp(-np.maximum(t - attack, 0.0) / decay)


def normalize(x, peak):
    top = np.max(np.abs(x))
    return x if top == 0 else x * (peak / top)


def fade_out(x, length):
    n = int(RATE * length)
    x = x.copy()
    x[-n:] *= np.linspace(1.0, 0.0, n)
    return x


def fire_loop(rng):
    """
    이음매 없이 도는 불 소리. 낮은 웅웅거림 위에 탁탁 튀는 소리를 얹는다.
    조금 길게 만든 뒤 넘친 끝을 처음에 겹쳐 섞어 이음매를 지운다.
    """
    extra = 0.5
    t = seconds(FIRE_LOOP_SECONDS + extra)
    n = len(t)

    roar = lowpass(rng.standard_normal(n), 500)
    # 불길이 일렁이듯 크기가 천천히 흔들린다.
    sway = lowpass(rng.standard_normal(n), 3)
    sway = 0.7 + 0.3 * sway / (np.max(np.abs(sway)) + 1e-9)
    roar *= sway
    body = band(rng.standard_normal(n), 150, 1200) * 0.35

    crackle = np.zeros(n)
    count = int((FIRE_LOOP_SECONDS + extra) * 22)
    for _ in range(count):
        start = int(rng.uniform(0, n - 800))
        length = int(rng.uniform(60, 500))
        burst = rng.standard_normal(length) * np.exp(-np.arange(length) / (length * 0.25))
        crackle[start:start + length] += burst * rng.uniform(0.3, 1.0)
    crackle = highpass(crackle, 1500) * 0.9

    x = normalize(roar, 1.0) * 0.55 + normalize(body, 1.0) * 0.25 + normalize(crackle, 1.0) * 0.5

    loop = int(RATE * FIRE_LOOP_SECONDS)
    overlap = int(RATE * extra)
    out = x[:loop].copy()
    ramp = np.linspace(0.0, 1.0, overlap)
    # 끝(loop..) 이 처음으로 녹아든다 — 마지막 샘플 다음이 x[loop]와 같아져 이어진다.
    out[:overlap] = x[:overlap] * ramp + x[loop:loop + overlap] * (1.0 - ramp)
    return normalize(out, 0.6)


def put_out(rng):
    """불이 꺼지는 "칙". 날카로운 고음 치익 + 짧은 지글거림."""
    t = seconds(0.4)
    hiss = band(rng.standard_normal(len(t)), 3000, 10000) * envelope(t, 0.004, 0.08)
    sizzle = band(rng.standard_normal(len(t)), 1200, 4000) * envelope(t, 0.02, 0.14) * 0.5
    return normalize(fade_out(hiss + sizzle, 0.05), 0.8)


def spray(rng, kind):
    """분사음. 물은 쏴아, 거품은 부글, 가스는 날카로운 쉭."""
    if kind == "water":
        t = seconds(0.24)
        x = band(rng.standard_normal(len(t)), 700, 4500) * envelope(t, 0.012, 0.09)
    elif kind == "foam":
        t = seconds(0.3)
        bubbles = 0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 28 * t + rng.uniform(0, 6)))
        x = band(rng.standard_normal(len(t)), 350, 2500) * envelope(t, 0.015, 0.11) * bubbles
    else:
        t = seconds(0.32)
        x = highpass(rng.standard_normal(len(t)), 4500) * envelope(t, 0.003, 0.12)
    return normalize(fade_out(x, 0.04), 0.7)


def backfire(rng):
    """역효과 "훅". 낮게 떨어지는 붐 + 불이 확 번지는 소리."""
    t = seconds(0.75)
    freq = 110 * np.exp(-t * 2.5) + 40
    phase = 2 * np.pi * np.cumsum(freq) / RATE
    boom = np.sin(phase) * envelope(t, 0.006, 0.22)
    whoosh = lowpass(rng.standard_normal(len(t)), 900) * envelope(t, 0.03, 0.25) * 0.8
    x = np.tanh((boom + whoosh) * 2.0)
    return normalize(fade_out(x, 0.08), 0.85)


def ignite(rng):
    """2차 발화 "화륵". 점점 커지다 확 붙는다."""
    t = seconds(0.65)
    swell = np.clip(t / 0.45, 0.0, 1.0) ** 2
    swell *= np.where(t > 0.45, np.exp(-(t - 0.45) / 0.07), 1.0)
    x = band(rng.standard_normal(len(t)), 250, 3000) * swell
    return normalize(fade_out(x, 0.03), 0.75)


def tone(freq, length, decay, harmonics):
    t = seconds(length)
    x = np.zeros(len(t))
    for h, weight in harmonics:
        x += np.sin(2 * np.pi * freq * h * t) * weight
    return x * envelope(t, 0.005, decay)


def jingle(win):
    """승리는 위로 오르는 장조, 패배는 아래로 내려가는 단조."""
    if win:
        notes = [(523.25, 0.11), (659.25, 0.11), (783.99, 0.11), (1046.5, 0.5)]
        harmonics = [(1, 1.0), (3, 0.3), (5, 0.12)]  # 사각파에 가까운 밝은 소리
        decay = 0.18
    else:
        notes = [(392.0, 0.2), (311.13, 0.2), (261.63, 0.7)]
        harmonics = [(1, 1.0), (3, 0.11), (5, 0.04)]  # 삼각파에 가까운 둥근 소리
        decay = 0.3
    parts = [tone(f, length, decay if i == len(notes) - 1 else length * 0.8, harmonics)
             for i, (f, length) in enumerate(notes)]
    return normalize(fade_out(np.concatenate(parts), 0.06), 0.7)


def synth_all():
    # 소리마다 따로 시드를 둔다. 한 소리를 고쳐도 다른 소리가 바뀌지 않게.
    return {
        "fire_loop": fire_loop(np.random.default_rng(101)),
        "putout": put_out(np.random.default_rng(102)),
        "spray_water": spray(np.random.default_rng(103), "water"),
        "spray_foam": spray(np.random.default_rng(104), "foam"),
        "spray_gas": spray(np.random.default_rng(105), "gas"),
        "backfire": backfire(np.random.default_rng(106)),
        "ignite": ignite(np.random.default_rng(107)),
        "jingle_win": jingle(True),
        "jingle_fail": jingle(False),
    }


def to_wav(samples):
    pcm = np.clip(np.round(samples * 32767), -32767, 32767).astype("<i2")
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    return buffer.getvalue()


# ---------------------------------------------------------------------------

def sync_dir(wanted):
    """선별 목록에 없는 소리 파일과 그 .meta를 지운다."""
    for f in os.listdir(AUDIO):
        base = f[:-5] if f.endswith(".meta") else f
        if base.endswith((".ogg", ".wav")) and base not in wanted:
            os.remove(os.path.join(AUDIO, f))


def main():
    os.makedirs(AUDIO, exist_ok=True)
    os.makedirs(LICENSES, exist_ok=True)
    packs = {name: zipfile.ZipFile(download(url)) for name, url in PACKS.items()}

    wanted = set()
    for name, rel in SELECTION.items():
        write_if_changed(os.path.join(AUDIO, name + ".ogg"), read(packs, rel))
        wanted.add(name + ".ogg")

    synthesized = synth_all()
    for name, samples in synthesized.items():
        write_if_changed(os.path.join(AUDIO, name + ".wav"), to_wav(samples))
        wanted.add(name + ".wav")

    sync_dir(wanted)

    for name, z in packs.items():
        lic = [e for e in z.namelist() if os.path.basename(e).lower() == "license.txt"]
        if not lic:
            raise SystemExit(f"{name} 팩에 License.txt가 없다")
        write_if_changed(os.path.join(LICENSES, f"kenney-{name}.txt"), z.read(lic[0]))

    print(f"완료: Kenney {len(SELECTION)}개, 합성 {len(synthesized)}개 -> {os.path.relpath(AUDIO, ROOT)}")


if __name__ == "__main__":
    sys.exit(main())
