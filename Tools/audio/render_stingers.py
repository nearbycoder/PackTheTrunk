"""Offline-rendered stingers and synth effects for Pack The Trunk (44.1 kHz stereo WAV)."""
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
import wave, os

SR = 44100
OUT = os.environ.get("PTT_SFX_OUT", "/tmp/sfx/out")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)

def t_(sec): return np.arange(int(sec * SR)) / SR
def note(n): return 440.0 * 2 ** ((n - 69) / 12)   # MIDI -> Hz

def lp(x, f, order=2): return sosfilt(butter(order, f, 'low', fs=SR, output='sos'), x, axis=-1)
def hp(x, f, order=2): return sosfilt(butter(order, f, 'high', fs=SR, output='sos'), x, axis=-1)
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], 'band', fs=SR, output='sos'), x, axis=-1)

def reverb(st, seconds=2.2, mix=0.28, predelay=0.012, bright=5000):
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = np.zeros((2, n + int(predelay * SR)))
    for c in range(2):
        noise = rng.standard_normal(n) * np.exp(-6.9 * tt / seconds)
        ir[c, int(predelay * SR):] = lp(noise, bright)
    ir /= np.sqrt((ir ** 2).sum(axis=1, keepdims=True))
    wet = np.stack([fftconvolve(st[c], ir[c])[: st.shape[1] + n] for c in range(2)])
    dry = np.pad(st, ((0, 0), (0, wet.shape[1] - st.shape[1])))
    return dry * (1 - mix) + wet * mix * 1.6

def place(buf, sig, at, pan=0.0, gain=1.0):
    i = int(at * SR)
    l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
    end = min(buf.shape[1], i + len(sig))
    buf[0, i:end] += sig[: end - i] * l * gain
    buf[1, i:end] += sig[: end - i] * r * gain

def epiano(f, dur, vel=1.0):
    """FM electric piano: 1:1 body plus a decaying 14:1 tine on the attack."""
    t = t_(dur)
    env = np.exp(-t * (1.6 + f / 900)) * (1 - np.exp(-t * 400))
    idx = 1.6 * vel * np.exp(-t * 5) + 0.25
    body = np.sin(2 * np.pi * f * t + idx * np.sin(2 * np.pi * f * t))
    tine = 0.35 * vel * np.sin(2 * np.pi * f * t + 2.2 * np.exp(-t * 30) * np.sin(2 * np.pi * f * 14 * t)) * np.exp(-t * 18)
    trem = 1 + 0.08 * np.sin(2 * np.pi * 4.6 * t)
    return (body + tine) * env * trem * vel

def bell(f, dur, vel=1.0):
    t = t_(dur)
    env = np.exp(-t * 3.2) * (1 - np.exp(-t * 900))
    idx = 3.0 * np.exp(-t * 4)
    s = np.sin(2 * np.pi * f * t + idx * np.sin(2 * np.pi * f * 3.5 * t))
    s += 0.3 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 6)
    return s * env * vel

def finish(name, st, peak_db=-3.0, fade=0.02):
    st = st.copy()
    n = int(fade * SR)
    st[:, -n:] *= np.linspace(1, 0, n)
    st /= max(1e-9, np.abs(st).max())
    st *= 10 ** (peak_db / 20)
    data = (np.clip(st.T, -1, 1) * 32767).astype('<i2')
    with wave.open(f"{OUT}/{name}.wav", 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(name, f"{st.shape[1]/SR:.2f}s")

# ---- Trip complete: warm Fmaj9 roll on electric piano with a little sparkle on top.
buf = np.zeros((2, int(4.5 * SR)))
place(buf, epiano(note(41), 3.5, 0.9), 0.0, 0.0, 0.8)       # F2
place(buf, epiano(note(53), 3.5, 0.7), 0.0, -0.1, 0.5)      # F3
for i, (n, pan) in enumerate([(57, -0.35), (60, -0.15), (64, 0.1), (67, 0.3), (69, 0.45), (72, 0.2)]):
    place(buf, epiano(note(n), 3.2 - i * 0.2, 0.75), 0.07 + i * 0.075, pan, 0.55)
for i, n in enumerate([84, 88, 91]):
    place(buf, bell(note(n), 1.8, 0.4), 0.55 + i * 0.09, 0.5 - i * 0.4, 0.22)
finish("trip_complete", reverb(buf, 2.6, 0.32))

# ---- Chapter card: slow, tender Dbmaj9 roll.
buf = np.zeros((2, int(6 * SR)))
for i, (n, pan) in enumerate([(37, 0), (49, -0.2), (56, -0.3), (60, 0.0), (63, 0.25), (65, 0.4), (68, 0.15)]):
    place(buf, epiano(note(n), 4.8 - i * 0.2, 0.6 if i > 1 else 0.8), i * 0.16, pan, 0.6)
finish("chapter", reverb(buf, 3.4, 0.4, bright=3500))

# ---- Menu confirm ("press any key"): two-note EP lift.
buf = np.zeros((2, int(2.2 * SR)))
place(buf, epiano(note(65), 1.6, 0.8), 0.0, -0.2, 0.6)
place(buf, epiano(note(72), 1.6, 0.8), 0.08, 0.2, 0.6)
place(buf, epiano(note(77), 1.5, 0.6), 0.16, 0.0, 0.45)
finish("confirm", reverb(buf, 1.8, 0.3))

# ---- Star bells (C6, E6, G6) and a shimmer.
for i, n in enumerate([84, 88, 91]):
    buf = np.zeros((2, int(2.0 * SR)))
    place(buf, bell(note(n), 1.6, 1.0), 0.0, -0.3 + i * 0.3, 1.0)
    place(buf, bell(note(n + 12), 0.8, 0.5), 0.0, 0.3 - i * 0.3, 0.3)
    finish(f"star_{i+1}", reverb(buf, 1.6, 0.3), -4)

# ---- Whooshes: swept band-passed noise with a stereo pan.
def whoosh(dur, f0, f1, pan0, pan1, name, peak=-6):
    n = int(dur * SR)
    noise = rng.standard_normal(n)
    out = np.zeros(n)
    blocks = 64
    for b in range(blocks):
        a, z = b * n // blocks, (b + 1) * n // blocks
        f = f0 * (f1 / f0) ** (b / blocks)
        seg = bp(noise[max(0, a - 2048):z], f * 0.6, min(f * 1.6, 18000))
        out[a:z] = seg[-(z - a):]
    tt = np.linspace(0, 1, n)
    env = np.sin(np.pi * tt ** 0.7) ** 2
    out *= env
    pan = pan0 + (pan1 - pan0) * tt
    st = np.stack([out * np.cos((pan + 1) * np.pi / 4), out * np.sin((pan + 1) * np.pi / 4)])
    finish(name, reverb(st, 0.6, 0.15), peak)

whoosh(0.5, 300, 2600, -0.7, 0.7, "whoosh_in")
whoosh(0.55, 2200, 260, 0.7, -0.7, "whoosh_out")
whoosh(0.16, 900, 3200, -0.2, 0.2, "swish", -8)

# ---- Car horn: two detuned saws through a horn-ish band, "beep-beep".
def saw(f, t): return 2 * ((f * t) % 1.0) - 1
buf = np.zeros((2, int(1.0 * SR)))
for start, length in [(0.0, 0.16), (0.24, 0.34)]:
    t = t_(length)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 7 * t)
    s = saw(415 * vib, t) + saw(523 * vib, t) * 0.8 + saw(417 * vib, t) * 0.5
    s = bp(s, 300, 2600, 2)
    env = np.clip(t / 0.012, 0, 1) * np.clip((length - t) / 0.03, 0, 1)
    place(buf, np.tanh(s * 1.5) * env, start, 0.0, 1.0)
finish("honk", reverb(buf, 0.5, 0.12), -5)

# ---- Engine: starter, idle catch, rev and pull away (pitch rises then fades into the distance).
dur = 3.4
t = t_(dur)
rpm = np.interp(t, [0, 0.35, 0.5, 0.9, 1.4, 2.4, 3.4], [8, 12, 26, 20, 38, 46, 50])
phase = 2 * np.pi * np.cumsum(rpm) / SR
pulse = sum(np.sin(k * phase) / k ** 0.8 for k in range(1, 14))
crackle = lp(rng.standard_normal(len(t)), 900) * 0.6
starter = np.where(t < 0.45, np.sin(2 * np.pi * 24 * t) * 0.8 * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t)) ** 3, 0)
eng = lp(pulse * (1 + crackle * 0.3), 1400, 2) + starter
amp = np.interp(t, [0, 0.4, 0.55, 1.2, 2.0, 3.4], [0.5, 0.6, 1.0, 0.9, 0.7, 0.0])
eng = eng * amp
eng = eng * np.interp(t, [0, 1.8, 3.4], [1, 1, 0.3])
tyres = lp(hp(rng.standard_normal(len(t)), 200), 2500) * np.interp(t, [0, 1.3, 2.0, 3.4], [0, 0, 0.25, 0]) * 0.5
mono = np.tanh(eng * 0.9) + tyres
pan = np.interp(t, [0, 1.5, 3.4], [0, 0, 0.6])
st = np.stack([mono * np.cos((pan + 1) * np.pi / 4), mono * np.sin((pan + 1) * np.pi / 4)])
finish("engine", reverb(st, 0.9, 0.12), -4)

# ---- Party popper for the confetti.
buf = np.zeros((2, int(1.2 * SR)))
t = t_(0.05)
place(buf, hp(rng.standard_normal(len(t)), 600) * np.exp(-t * 60), 0.0, 0, 1.0)
for i in range(26):
    tt = t_(0.012)
    place(buf, hp(rng.standard_normal(len(tt)), 3000) * np.exp(-tt * 300), 0.05 + rng.random() * 0.6, rng.uniform(-0.8, 0.8), rng.uniform(0.1, 0.3))
finish("popper", reverb(buf, 0.8, 0.2), -6)

# ---- Soft "nope" for invalid drops: muted wood-block double knock, friendly not harsh.
buf = np.zeros((2, int(0.5 * SR)))
for at, f in [(0.0, 330), (0.11, 262)]:
    tt = t_(0.18)
    s = np.sin(2 * np.pi * f * tt + 1.2 * np.exp(-tt * 40) * np.sin(2 * np.pi * f * 2.3 * tt)) * np.exp(-tt * 28)
    place(buf, s, at, 0, 1.0)
finish("nope", reverb(buf, 0.4, 0.12), -5)
