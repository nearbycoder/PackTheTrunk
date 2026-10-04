#!/usr/bin/env python3
"""Cuts the Steam-style feature trailer, its poster frame, the README teaser loop and the
screenshots from the footage that Tools/record_trailer.sh captured.

    Tools/make_trailer.py [capture-dir ...] [--only trailer|teaser|stills]

capture-dir defaults to Recordings/trailer-capture. With several folders (a full capture plus
re-shoots from PTT_TRAILER_ONLY), beats and stills from later folders replace earlier ones. Needs ffmpeg (libx264, libwebp) and Pillow
(`pip install pillow`). Writes into docs/media/:

    pack-the-trunk-trailer.mp4   1920x1080, 30 fps, H.264 + AAC
    trailer-poster.jpg           a still with a play button, for the README
    teaser.webp                  looping animated WebP for the top of the README
    screenshots/*.jpg            the clean stills the capture took

How it's put together: every beat in EDL below becomes a segment (footage trimmed from the
capture, with a scrapbook caption card that slides in), the title and end cards are drawn
frame by frame with the game's own fonts, the segments are joined with crossfades, and a
lo-fi track from the game's soundtrack is laid underneath, ducked by the game's sound effects.
"""
import argparse
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONTS = ROOT / "Assets/Resources/Fonts"
DISPLAY = str(FONTS / "LilitaOne-Regular.ttf")
HAND = str(FONTS / "PatrickHand-Regular.ttf")
BODY = str(FONTS / "VarelaRound-Regular.ttf")
MUSIC = ROOT / "Assets/Resources/Music/a_cup_of_tea.ogg"
SFX = ROOT / "Assets/Resources/Audio/Sfx"
MEDIA = ROOT / "docs/media"
WORK = ROOT / "Recordings/trailer-build"

W, H, FPS = 1920, 1080, 30
URL = "github.com/nearbycoder/PackTheTrunk"

# UiTheme colours (Assets/Scripts/UI/UiTheme.cs): the "summer road-trip scrapbook" look.
PAPER = (255, 247, 232)
PAPER_SHADE = (235, 219, 194)
INK = (43, 41, 56)
INK_SOFT = (107, 99, 120)
ACCENT = (255, 122, 61)
TEAL = (41, 168, 158)
GOLD = (247, 184, 46)
NIGHT = (41, 48, 77)
TAPE = (244, 226, 160, 210)

# ---------------------------------------------------------------------------------------------
# The edit. Each clip names a beat from beats.tsv; "at" and "len" are seconds into that beat.
# Captions: (headline, line underneath). pos: where the card sits ("top" between the HUD's
# trip tag and packing list, "top-center", "bottom-left", "bottom-right").
# ---------------------------------------------------------------------------------------------
EDL = [
    # Cold open: the last two things go into the biggest trunk, it slams shut and drives off.
    dict(kind="clip", parts=[("coldopen", 2.8, 8.2)], caption=None),
    dict(kind="title", len=4.6),
    dict(kind="clip", parts=[("core", 2.2, 7.0)],
         caption=("Pick it up. Drop it in.", "Everything snaps to a grid. Green means it fits."), pos="top"),
    dict(kind="clip", parts=[("rotate", 0.4, 5.2)],
         caption=("Turn it. Tip it. Roll it.", "R, T and F spin anything until it finds its gap."), pos="top-left"),
    dict(kind="clip", parts=[("heights", 0.3, 6.4)],
         caption=("On top, or tucked under?", "The wheel (or W / S) picks the shelf or the gap it rests in."), pos="top"),
    dict(kind="clip", parts=[("fragile", 0.6, 7.6)],
         caption=("Fragile goes on top", "Eggs, cakes and lava lamps can't take any weight."), pos="top"),
    dict(kind="clip", parts=[("clown", 0.6, 6.2)],
         caption=("Awkward spaces", "Wheel wells, sloped glass, toolboxes... and a clown."), pos="top"),
    dict(kind="clip", parts=[("essentials", 1.6, 6.2)],
         caption=("Essentials first", "Pack everything on the list, then squeeze in extras for stars."), pos="top"),
    dict(kind="clip", parts=[("undo", 0.0, 4.7)],
         caption=("Changed your mind?", "Undo anything, or lift it right back out of the trunk."), pos="top"),
    dict(kind="clip", parts=[("close", 0.8, 8.2)],
         caption=("Slam it shut", "Close the trunk, hit the road, earn up to three stars."), pos="top-left"),
    # The texts arrive at reading pace in the game; 1.5x keeps the trailer moving.
    dict(kind="clip", parts=[("story", 5.6, 6.3)], speed=1.5,
         caption=("Every trip is a story", "Texts and notes from the family set up each trip."), pos="bottom-right"),
    dict(kind="clip", parts=[("chapter", 2.0, 5.8)], speed=1.25,
         caption=("33 trips. 30 years. One family.", "Six chapters, from Grandpa's red wagon in 1998 to 2027."), pos="bottom-right"),
    dict(kind="clip", parts=[("map", 1.0, 5.0)],
         caption=("The trip map", "Every trip waits on a chapter page, with its stars."), pos="bottom-left"),
    dict(kind="clip", parts=[("arrive_beach", 0.3, 1.3), ("arrive_house", 0.3, 1.3), ("arrive_honeymoon", 0.3, 1.3),
                             ("arrive_talent", 0.3, 1.3), ("arrive_grandma", 0.3, 1.3), ("arrive_mini", 0.3, 1.3)],
         caption=("11 rides, 116 things to pack", "From a little red wagon to a moving truck. Yes, that's a kitchen sink."), pos="top"),
    dict(kind="clip", parts=[("album", 4.6, 6.2)],
         caption=("The family album", "Every trunk you pack becomes a photo."), pos="bottom-left"),
    dict(kind="clip", parts=[("menu", 1.4, 2.2), ("menu", 5.8, 2.4), ("pause", 0.4, 2.4)],
         caption=("Settle in", "Lo-fi soundtrack, pause any time, every setting you'd want."), pos="bottom-left"),
    # Escalation: bigger and bigger loads, faster and faster, and one more slam.
    dict(kind="clip", parts=[("speed_festival", 0.3, 1.8), ("speed_house", 0.4, 2.2), ("speed_everything", 0.3, 7.0)],
         caption=("Big things first. Fragile on top.", "And always leave room for one more thing."), pos="top-center"),
    dict(kind="end", len=6.5),
]
FADE = 0.4          # crossfade between beats
TO_VIDEO = "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p"
MUSIC_GAIN_DB = -5  # music bed under the game's sound effects (ducked further by the sidechain)


def run(cmd, **kw):
    print("+", " ".join(str(c) for c in cmd)[:240], flush=True)
    subprocess.run([str(c) for c in cmd], check=True, **kw)


def ffprobe_duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)],
                         check=True, capture_output=True, text=True).stdout
    return float(out.strip())


# ---------------------------------------------------------------------------------------------
# Drawing (Pillow)
# ---------------------------------------------------------------------------------------------

def font(path, size):
    return ImageFont.truetype(path, size)


def wrap(draw, text, fnt, width):
    words, lines, line = text.split(), [], ""
    for w in words:
        test = (line + " " + w).strip()
        if draw.textlength(test, font=fnt) <= width or not line:
            line = test
        else:
            lines.append(line)
            line = w
    if line:
        lines.append(line)
    return lines


def paper_card(size, radius=18, color=PAPER, rotation=0.0, shadow=True, tape=True):
    """A paper card with a soft drop shadow and a strip of tape, like the game's UI."""
    w, h = size
    pad = 60
    img = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    if shadow:
        sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).rounded_rectangle((pad + 8, pad + 14, pad + w + 8, pad + h + 14), radius, fill=(26, 18, 30, 110))
        img = Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(12)))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((pad, pad, pad + w, pad + h), radius, fill=color + (255,))
    d.rounded_rectangle((pad, pad + h - 8, pad + w, pad + h), radius, fill=PAPER_SHADE + (255,))
    d.rectangle((pad, pad + h - 18, pad + w, pad + h - 8), fill=color + (255,))
    if tape:
        t = Image.new("RGBA", (150, 42), TAPE)
        td = ImageDraw.Draw(t)
        for x in range(0, 150, 6):  # torn ends
            td.polygon([(x, 0), (x + 3, 4), (x + 6, 0)], fill=(0, 0, 0, 0))
            td.polygon([(x, 42), (x + 3, 38), (x + 6, 42)], fill=(0, 0, 0, 0))
        t = t.rotate(-9, expand=True, resample=Image.BICUBIC)
        img.alpha_composite(t, (pad - 44, pad - 22))
    if rotation:
        img = img.rotate(rotation, expand=True, resample=Image.BICUBIC)
    return img, pad


def caption_card(headline, sub):
    """Two-line scrapbook caption: Lilita One headline, handwritten line underneath."""
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    fh, fs = font(DISPLAY, 56), font(HAND, 35)
    max_w = 640
    sub_lines = wrap(probe, sub, fs, max_w)
    tw = max([probe.textlength(headline, font=fh)] + [probe.textlength(l, font=fs) for l in sub_lines])
    padx, pady = 34, 20
    w = int(tw + padx * 2 + 14)
    h = int(pady + 60 + 6 + 38 * len(sub_lines) + pady + 6)
    card, pad = paper_card((w, h), rotation=0)
    d = ImageDraw.Draw(card)
    x0, y0 = pad + padx + 14, pad + pady
    d.rounded_rectangle((pad + 18, pad + 22, pad + 26, pad + h - 26), 4, fill=ACCENT + (255,))
    d.text((x0, y0 - 6), headline, font=fh, fill=INK)
    for i, line in enumerate(sub_lines):
        d.text((x0, y0 + 62 + i * 38), line, font=fs, fill=INK_SOFT)
    return card.rotate(1.2, expand=True, resample=Image.BICUBIC)


def ease_out_back(p, s=1.6):
    p = min(max(p, 0.0), 1.0) - 1.0
    return 1.0 + (s + 1.0) * p ** 3 + s * p ** 2


def ease_out_cubic(p):
    p = min(max(p, 0.0), 1.0)
    return 1.0 - (1.0 - p) ** 3


def logo(scale=1.0):
    """PACK THE / TRUNK in Lilita One: white with a thick ink outline, tilted like the title screen."""
    big = font(DISPLAY, int(190 * scale))
    small = font(DISPLAY, int(150 * scale))
    stroke = max(4, int(14 * scale))
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    w1 = probe.textlength("PACK THE", font=small)
    w2 = probe.textlength("TRUNK", font=big)
    w = int(max(w1, w2) + stroke * 4 + 40)
    h = int(150 * scale + 190 * scale + stroke * 4 + 40)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for layer, off, fill, st in ((sh, (8, 14), (20, 14, 24, 150), stroke + 2), (img, (0, 0), (255, 255, 255), stroke)):
        d = ImageDraw.Draw(layer)
        d.text(((w - w1) / 2 + off[0], 10 + off[1]), "PACK THE", font=small, fill=fill, stroke_width=st, stroke_fill=INK if layer is img else fill)
        d.text(((w - w2) / 2 + off[0], 10 + 150 * scale + off[1]), "TRUNK", font=big, fill=fill, stroke_width=st, stroke_fill=INK if layer is img else fill)
    out = Image.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)), img)
    return out.rotate(4, expand=True, resample=Image.BICUBIC)


def pill(text, fnt, fill, fg=(255, 255, 255), padx=34, h=72):
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    tw = probe.textlength(text, font=fnt)
    img = Image.new("RGBA", (int(tw + padx * 2) + 20, h + 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((10, 16, img.width - 10, h + 16), h // 2, fill=(0, 0, 0, 70))
    d.rounded_rectangle((10, 10, img.width - 10, h + 10), h // 2, fill=fill + (255,))
    bbox = d.textbbox((0, 0), text, font=fnt)
    d.text(((img.width - (bbox[2] - bbox[0])) / 2 - bbox[0], 10 + (h - (bbox[3] - bbox[1])) / 2 - bbox[1]), text, font=fnt, fill=fg)
    return img


def paste_center(canvas, img, cx, cy, alpha=1.0, scale=1.0):
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.BICUBIC)
    if alpha < 1.0:
        a = img.getchannel("A").point(lambda v: int(v * max(0.0, alpha)))
        img = img.copy()
        img.putalpha(a)
    canvas.alpha_composite(img, (int(cx - img.width / 2), int(cy - img.height / 2)))


def title_frame(t, assets):
    """Title card: logo pops in, tagline slides up, then everything holds."""
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    k = ease_out_back((t - 0.15) / 0.55)
    if t > 0.15:
        paste_center(im, assets["logo"], W / 2, H / 2 - 70, alpha=min(1, (t - 0.15) / 0.2), scale=0.5 + 0.5 * k)
    p = ease_out_cubic((t - 0.75) / 0.5)
    if t > 0.75:
        paste_center(im, assets["tagline"], W / 2, H / 2 + 200 + 40 * (1 - p), alpha=p)
    return im


def end_frame(t, assets):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = ease_out_cubic(t / 0.6)
    paste_center(im, assets["endcard"], W / 2, H / 2 + 60 * (1 - p), alpha=p)
    k = ease_out_back((t - 0.35) / 0.55)
    if t > 0.35:
        paste_center(im, assets["logo_small"], W / 2, H / 2 - 150, alpha=min(1, (t - 0.35) / 0.2), scale=0.6 + 0.4 * k)
    for i, key in enumerate(("end_tag", "end_url", "end_meta")):
        start = 0.9 + i * 0.25
        q = ease_out_cubic((t - start) / 0.45)
        if t > start:
            y = H / 2 + [55, 150, 228][i]
            paste_center(im, assets[key], W / 2, y + 30 * (1 - q), alpha=q)
    return im


def card_assets():
    a = {"logo": logo(1.0), "logo_small": logo(0.62)}
    tag = font(HAND, 58)
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    text = "A lifetime of fitting way too much into way too little."
    tw = probe.textlength(text, font=tag)
    flat, pad = paper_card((int(tw + 90), 92), rotation=0)
    ImageDraw.Draw(flat).text((pad + 45, pad + 14), text, font=tag, fill=INK)
    a["tagline"] = flat.rotate(-1.5, expand=True, resample=Image.BICUBIC)

    card, pad = paper_card((1060, 640), radius=26, tape=True)
    a["endcard"] = card.rotate(-1.0, expand=True, resample=Image.BICUBIC)
    a["end_tag"] = text_img("Pack every trip of one family's life, 1998 to 2027.", font(HAND, 46), INK_SOFT)
    a["end_url"] = pill(URL, font(BODY, 40), ACCENT, h=78)
    a["end_meta"] = text_img("Free on GitHub  ·  Linux  ·  Made with Unity and Blender", font(BODY, 30), INK_SOFT)
    return a


def text_img(text, fnt, fill):
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    bbox = probe.textbbox((0, 0), text, font=fnt)
    img = Image.new("RGBA", (bbox[2] - bbox[0] + 20, bbox[3] - bbox[1] + 20), (0, 0, 0, 0))
    ImageDraw.Draw(img).text((10 - bbox[0], 10 - bbox[1]), text, font=fnt, fill=fill)
    return img


# ---------------------------------------------------------------------------------------------
# Segments (ffmpeg)
# ---------------------------------------------------------------------------------------------

def caption_xy(pos, cw, ch):
    if pos == "top":            # between the trip tag (top-left) and the packing list (right)
        return 1105 - cw / 2, 18
    if pos == "top-center":
        return (W - cw) / 2, 18
    if pos == "top-left":
        return 24, 18
    if pos == "bottom-left":
        return 40, H - ch - 40
    if pos == "bottom-right":
        return W - cw - 40, H - ch - 40
    raise ValueError(pos)


def render_clip(index, seg, beats):
    out = WORK / f"seg_{index:02d}.mkv"
    inputs, filters, vlabels, alabels = [], [], [], []
    total = 0.0
    for k, (beat, at, length) in enumerate(seg["parts"]):
        cap_dir, start, end = beats[beat]
        avail = (end - start) / FPS
        if at + length > avail + 1e-3:
            print(f"warning: {beat} has {avail:.2f}s, edl wants {at}+{length}", file=sys.stderr)
            length = max(0.5, avail - at)
        first = start + int(round(at * FPS))
        frames = int(round(length * FPS))
        speed = seg.get("speed", 1.0)
        out_len = frames / FPS / speed
        inputs += ["-framerate", FPS, "-start_number", first, "-i", cap_dir / "frame_%05d.jpg"]
        inputs += ["-ss", f"{first / FPS:.4f}", "-t", f"{frames / FPS:.4f}", "-i", cap_dir / "audio.wav"]
        vi, ai = 2 * k, 2 * k + 1
        # The captured JPEGs are full-range BT.601; the trailer is limited-range BT.709 like any video.
        filters.append(f"[{vi}:v]trim=end_frame={frames},setpts=(PTS-STARTPTS)/{speed},fps={FPS},{TO_VIDEO},setsar=1[v{k}]")
        fade = 0.012 if len(seg["parts"]) > 1 else 0.0
        af = f"[{ai}:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,apad,atrim=0:{frames / FPS:.4f},asetpts=PTS-STARTPTS"
        if speed != 1.0:
            af += f",atempo={speed}"
        if fade:
            af += f",afade=t=in:d={fade},afade=t=out:st={out_len - fade:.4f}:d={fade}"
        filters.append(af + f"[a{k}]")
        vlabels.append(f"[v{k}]")
        alabels.append(f"[a{k}]")
        total += out_len
    n = len(seg["parts"])
    if n > 1:
        filters.append("".join(f"{v}{a}" for v, a in zip(vlabels, alabels)) + f"concat=n={n}:v=1:a=1[vc][ac]")
    else:
        filters.append("[v0]null[vc]")
        filters.append("[a0]anull[ac]")
    vout = "[vc]"
    if seg.get("caption"):
        img = caption_card(*seg["caption"])
        cap = WORK / f"caption_{index:02d}.png"
        img.save(cap)
        cw, ch = img.size
        x1, y1 = caption_xy(seg.get("pos", "top"), cw, ch)
        ci = 2 * n
        inputs += ["-loop", 1, "-framerate", FPS, "-t", f"{total:.4f}", "-i", cap]
        t0, d_in = 0.35, 0.5
        t_out = total - 0.55
        # Slide in from the side it's anchored to (ease-out-back), fade out before the cut.
        from_x = -cw if x1 < W / 2 - cw / 2 else W
        if seg.get("pos") in ("top", "top-center", "top-left"):
            x_expr = f"{x1:.1f}"
            y_from = -ch
            p = f"min(max((t-{t0})/{d_in},0),1)-1"
            y_expr = f"{y_from:.1f}+({y1 - y_from:.1f})*(1+2.6*pow({p},3)+1.6*pow({p},2))"
        else:
            p = f"min(max((t-{t0})/{d_in},0),1)-1"
            x_expr = f"{from_x:.1f}+({x1 - from_x:.1f})*(1+2.6*pow({p},3)+1.6*pow({p},2))"
            y_expr = f"{y1:.1f}"
        filters.append(f"[{ci}:v]format=rgba,fade=t=in:st={t0}:d=0.2:alpha=1,fade=t=out:st={t_out:.3f}:d=0.3:alpha=1[cap]")
        filters.append(f"[vc][cap]overlay=x='{x_expr}':y='{y_expr}':eval=frame:shortest=1,format=yuv420p[vo]")
        vout = "[vo]"
    run(["ffmpeg", "-y", "-v", "error", *inputs, "-filter_complex", ";".join(filters), "-map", vout, "-map", "[ac]",
         "-r", FPS, "-c:v", "libx264", "-preset", "veryfast", "-crf", 10, "-pix_fmt", "yuv420p",
         "-c:a", "pcm_s16le", "-ar", 48000, "-t", f"{total:.4f}", out])
    return out, total


def render_card(index, seg, beats, assets):
    """Title / end card: the game's footage, blurred and dimmed, under frames drawn with Pillow."""
    out = WORK / f"seg_{index:02d}.mkv"
    length = seg["len"]
    frames = int(round(length * FPS))
    # Blurred gameplay behind the card: the moving truck filling up, or the family album.
    bg_beat, bg_at = seg.get("bg", ("speed_house", 0.0) if seg["kind"] == "title" else ("album", 0.2))
    cap_dir, start, end = beats[bg_beat]
    first = start + int(round(bg_at * FPS))
    if first + frames > end:  # hold the last frame if the background beat is short
        first = max(start, end - frames)
    draw = title_frame if seg["kind"] == "title" else end_frame
    # Sound: a whoosh as the logo pops in; a little honk to finish the end card.
    stinger = SFX / ("whoosh_in.ogg" if seg["kind"] == "title" else "honk.ogg")
    delay = 120 if seg["kind"] == "title" else int((length - 1.6) * 1000)
    cmd = ["ffmpeg", "-y", "-v", "error",
           "-framerate", FPS, "-start_number", first, "-i", cap_dir / "frame_%05d.jpg",
           "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{W}x{H}", "-framerate", FPS, "-i", "-",
           "-i", stinger,
           "-filter_complex",
           f"[0:v]trim=end_frame={frames},setpts=PTS-STARTPTS,tpad=stop_mode=clone:stop_duration={length},"
           f"gblur=sigma=14,eq=brightness=-0.12:saturation=0.85,format=rgba[bg];"
           f"[bg][1:v]overlay=0:0:shortest=1,{TO_VIDEO}[v];"
           f"[2:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,volume=-4dB,adelay={delay}|{delay},apad,atrim=0:{length}[a]",
           "-map", "[v]", "-map", "[a]", "-r", FPS, "-c:v", "libx264", "-preset", "veryfast", "-crf", 10,
           "-pix_fmt", "yuv420p", "-c:a", "pcm_s16le", "-ar", 48000, "-frames:v", frames, out]
    print("+ card", seg["kind"], flush=True)
    proc = subprocess.Popen([str(c) for c in cmd], stdin=subprocess.PIPE)
    for f in range(frames):
        proc.stdin.write(draw(f / FPS, assets).tobytes())
    proc.stdin.close()
    if proc.wait() != 0:
        raise SystemExit("card render failed")
    return out, length


def assemble(segments):
    """Crossfade every segment into the next (video and the game's sound together)."""
    inputs, filters = [], []
    for path, _ in segments:
        inputs += ["-i", path]
    offset = 0.0
    vprev, aprev = "[0:v]", "[0:a]"
    for i in range(1, len(segments)):
        offset += segments[i - 1][1] - FADE
        vout, aout = f"[v{i}]", f"[a{i}]"
        trans = "fade"
        filters.append(f"{vprev}[{i}:v]xfade=transition={trans}:duration={FADE}:offset={offset:.4f}{vout}")
        filters.append(f"{aprev}[{i}:a]acrossfade=d={FADE}:c1=tri:c2=tri{aout}")
        vprev, aprev = vout, aout
    total = sum(d for _, d in segments) - FADE * (len(segments) - 1)
    out = WORK / "assembled.mkv"
    run(["ffmpeg", "-y", "-v", "error", *inputs, "-filter_complex", ";".join(filters), "-map", vprev, "-map", aprev,
         "-c:v", "libx264", "-preset", "veryfast", "-crf", 10, "-pix_fmt", "yuv420p", "-c:a", "pcm_s16le", out])
    return out, total


def finish(assembled, total, music_start):
    """Music bed (ducked under the game's sound), loudness to -16 LUFS, final H.264 + AAC encode."""
    out = MEDIA / "pack-the-trunk-trailer.mp4"
    music_len = total - music_start
    fade_out = 3.2
    ms = int(music_start * 1000)
    graph = (
        f"[0:a]aresample=48000,asplit=2[sfx][key];"
        f"[1:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,atrim=0:{music_len:.3f},"
        f"afade=t=in:d=0.25,afade=t=out:st={music_len - fade_out:.3f}:d={fade_out},volume={MUSIC_GAIN_DB}dB,"
        f"adelay={ms}|{ms},apad,atrim=0:{total:.3f}[bed];"
        f"[bed][key]sidechaincompress=threshold=0.035:ratio=5:attack=12:release=420:makeup=1[ducked];"
        f"[sfx][ducked]amix=inputs=2:normalize=0:duration=first,"
        f"loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[mix]"
    )
    # Budget: stay under 40 MB with headroom.
    max_kbps = int(min(7500, (37.5 * 8 * 1024 * 1024 / total - 192_000) / 1000))
    run(["nice", "-n", "10", "ffmpeg", "-y", "-v", "error", "-i", assembled, "-i", MUSIC,
         "-filter_complex", graph, "-map", "0:v", "-map", "[mix]",
         "-c:v", "libx264", "-preset", "slow", "-crf", 19, "-maxrate", f"{max_kbps}k", "-bufsize", f"{max_kbps * 2}k",
         "-profile:v", "high", "-pix_fmt", "yuv420p", "-r", FPS, "-g", FPS * 2,
         "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
         "-c:a", "aac", "-b:a", "192k", "-ar", 48000, "-movflags", "+faststart", out])
    return out


# ---------------------------------------------------------------------------------------------
# Poster, teaser, screenshots
# ---------------------------------------------------------------------------------------------

def poster(beats, total, assets):
    """Key art with a play button: blurred gameplay, the logo and tagline, and the trailer's length."""
    cap_dir, start, _ = beats["speed_house"]
    bg = Image.open(cap_dir / f"frame_{start + 60:05d}.jpg").convert("RGB").filter(ImageFilter.GaussianBlur(10))
    bg = Image.blend(bg, Image.new("RGB", bg.size, (24, 20, 34)), 0.28).convert("RGBA")
    paste_center(bg, assets["logo"], W / 2, 300, scale=0.86)
    paste_center(bg, assets["tagline"], W / 2, 545)
    overlay = Image.new("RGBA", bg.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    cx, cy, r = W / 2, 770, 108
    d.ellipse((cx - r + 6, cy - r + 12, cx + r + 6, cy + r + 12), fill=(0, 0, 0, 90))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=ACCENT + (255,), outline=(255, 255, 255, 255), width=9)
    d.polygon([(cx - 32, cy - 52), (cx - 32, cy + 52), (cx + 56, cy)], fill=(255, 255, 255, 255))
    bg = Image.alpha_composite(bg, overlay)
    label = pill(f"WATCH THE TRAILER  ·  {int(total // 60)}:{int(round(total % 60)):02d}", font(DISPLAY, 46), NIGHT, h=84)
    paste_center(bg, label, cx, cy + r + 85)
    bg.convert("RGB").resize((1280, 720), Image.LANCZOS).save(MEDIA / "trailer-poster.jpg", quality=88, optimize=True)


TEASER = [("core", 3.0, 3.4), ("speed_house", 0.5, 2.2), ("coldopen", 7.3, 3.5)]


def teaser(beats):
    """A ~9 s loop: a suitcase picked up, turned and dropped in, a moving truck filling up fast, the slam."""
    out = MEDIA / "teaser.webp"
    inputs, filters, labels = [], [], []
    for k, (beat, at, length) in enumerate(TEASER):
        cap_dir, start, _ = beats[beat]
        inputs += ["-framerate", FPS, "-start_number", start + int(at * FPS), "-i", cap_dir / "frame_%05d.jpg"]
        filters.append(f"[{k}:v]trim=end_frame={int(length * FPS)},setpts=PTS-STARTPTS[t{k}]")
        labels.append(f"[t{k}]")
    filters.append("".join(labels) + f"concat=n={len(TEASER)}:v=1:a=0,fps=15,scale=960:-2:flags=lanczos[v]")
    run(["nice", "-n", "10", "ffmpeg", "-y", "-v", "error", *inputs, "-filter_complex", ";".join(filters), "-map", "[v]",
         "-c:v", "libwebp_anim", "-lossless", 0, "-quality", 82, "-compression_level", 6, "-loop", 0, out])
    return out


STILLS = {  # capture still name -> README screenshot name
    "title": "01-title", "packing": "02-packing", "slam": "03-slam", "story": "04-story",
    "fragile": "05-fragile", "clown": "06-clown-car", "late": "07-everyone-everything", "postcard": "08-postcard",
    "map": "09-trip-map", "album": "10-family-album",
}


# Screenshots grabbed from the footage instead (beat, seconds in): the sedan's slam is closer to
# the camera than the minivan's.
STILL_FRAMES = {"slam": ("close", 2.23)}


def stills(cap_dirs, beats):
    dest = MEDIA / "screenshots"
    dest.mkdir(parents=True, exist_ok=True)
    latest = {}  # later capture folders win
    for cap_dir in cap_dirs:
        for png in sorted((cap_dir / "stills").glob("*.png")):
            latest[png.stem.split("-", 1)[1]] = png
    for name, (beat, at) in STILL_FRAMES.items():
        cap_dir, start, _ = beats[beat]
        latest[name] = cap_dir / f"frame_{start + int(round(at * FPS)):05d}.jpg"
    for name, png in sorted(latest.items()):
        if name not in STILLS:
            continue
        im = Image.open(png).convert("RGB")
        if im.size != (W, H):
            im = im.resize((W, H), Image.LANCZOS)
        target = dest / f"{STILLS[name]}.jpg"
        for q in (92, 88, 84, 80):
            im.save(target, quality=q, optimize=True, progressive=True, subsampling=0 if q >= 88 else 2)
            if target.stat().st_size <= 1_400_000:
                break
        print(f"{target.relative_to(ROOT)}  {target.stat().st_size // 1024} KB")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("capture", nargs="*", default=[str(ROOT / "Recordings/trailer-capture")])
    ap.add_argument("--only", choices=["trailer", "teaser", "stills"])
    args = ap.parse_args()
    caps = [Path(c).resolve() for c in args.capture]
    beats = {}  # name -> (capture folder, first frame, end frame); later folders win
    for cap in caps:
        for line in (cap / "beats.tsv").read_text().splitlines():
            name, a, b = line.split("\t")
            beats[name] = (cap, int(a), int(b))
    MEDIA.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)

    if args.only in (None, "stills"):
        stills(caps, beats)
    if args.only in (None, "teaser"):
        t = teaser(beats)
        print(f"{t.relative_to(ROOT)}  {t.stat().st_size / 1e6:.1f} MB")
    if args.only in (None, "trailer"):
        assets = card_assets()
        segments = []
        for i, seg in enumerate(EDL):
            if seg["kind"] == "clip":
                segments.append(render_clip(i, seg, beats))
            else:
                segments.append(render_card(i, seg, beats, assets))
        assembled, total = assemble(segments)
        # The music comes in with the title card, after the cold open.
        music_start = segments[0][1] - FADE
        out = finish(assembled, total, music_start)
        poster(beats, ffprobe_duration(out), assets)
        marks, t = [], 0.0
        for seg, (_, d) in zip(EDL, segments):
            marks.append((round(t, 2), seg.get("caption", [seg["kind"]])[0] if seg.get("caption") else seg["kind"] if seg["kind"] != "clip" else "cold open"))
            t += d - FADE
        (WORK / "beats.json").write_text(json.dumps(marks, indent=1))
        print(f"{out.relative_to(ROOT)}  {ffprobe_duration(out):.1f} s  {out.stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
