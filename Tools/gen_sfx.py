#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""按微信版 2048 sound.js 的参数合成可爱软软风音效（WAV 16bit mono 44100Hz）。
生成 5 个新音效：special（三连音）/ bank（轻啵）/ reward（开心上行）/ undo（柔和下滑）/ click（轻触）
输出到 Assets/Resources/Audio（Unity 编辑器）与 Assets/StreamingAssets/audio（TikTok 真机回退）。
"""
import math
import os
import struct
import wave

SR = 44100


def synth(freq_from, freq_to, dur, vol, delay=0.0):
    """单音：正弦波，频率指数渐变（近似 WebAudio exponentialRamp），快起快落包络。"""
    n_delay = int(delay * SR)
    n = int(dur * SR)
    total = n_delay + n + int(0.06 * SR)
    samples = [0.0] * total
    for i in range(n):
        t = i / n
        # 指数插值频率（线性在指数域）
        f = freq_from * (freq_to / freq_from) ** t
        phase = 2 * math.pi * f * i / SR
        # 指数包络：快起 0.018s，指数衰减到 0.0001
        env = math.exp(-3.0 * t)  # 近似指数衰减
        attack = min(1.0, (i / SR) / 0.018)
        v = vol * env * attack * math.sin(phase)
        samples[n_delay + i] = v
    return samples


def mix(tracks):
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i in range(min(n, len(t))):
            out[i] += t[i]
    # 软限幅
    peak = max(1e-9, max(abs(v) for v in out))
    if peak > 0.92:
        k = 0.92 / peak
        out = [v * k for v in out]
    return out


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for v in samples:
            x = int(max(-1.0, min(1.0, v)) * 32767)
            frames += struct.pack('<h', x)
        w.writeframes(bytes(frames))
    print('wrote', path, len(samples) / SR, 's')


def sfx_special():
    return mix([
        synth(660, 740, 0.09, 0.12),
        synth(880, 990, 0.09, 0.11, 0.08),
        synth(1046, 1318, 0.14, 0.11, 0.16),
    ])


def sfx_bank():
    return mix([synth(740, 900, 0.08, 0.10)])


def sfx_reward():
    return mix([
        synth(740, 988, 0.10, 0.12),
        synth(988, 1318, 0.14, 0.11, 0.09),
    ])


def sfx_undo():
    return mix([synth(520, 330, 0.16, 0.11)])


def sfx_click():
    return mix([synth(700, 760, 0.05, 0.09)])


BASE = r'E:\2048_TikTok_Unity-main\Assets'
OUT_RES = os.path.join(BASE, 'Resources', 'Audio')
OUT_SA = os.path.join(BASE, 'StreamingAssets', 'audio')

for name, maker in [('special', sfx_special), ('bank', sfx_bank),
                    ('reward', sfx_reward), ('undo', sfx_undo), ('click', sfx_click)]:
    samples = maker()
    write_wav(os.path.join(OUT_RES, name + '.wav'), samples)
    write_wav(os.path.join(OUT_SA, name + '.wav'), samples)

print('done')
