#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
make_icon.py —— 从立绘头部裁切生成程序/托盘图标（assets/fish.ico）
用法：python make_icon.py --in assets/sprites/idle.png --out assets/fish.ico
"""
import argparse
import sys
from PIL import Image, ImageDraw


def head_crop(src):
    w, h = src.size
    cw = int(w * 0.66)
    ch = cw
    cx = w // 2 - cw // 2
    cy = int(h * 0.03)
    if cx < 0:
        cx, cw = 0, w
    if cy + ch > h:
        ch = max(8, h - cy)
    return src.crop((cx, cy, cx + cw, cy + ch))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", default="assets/sprites/idle.png")
    ap.add_argument("--out", dest="dst", default="assets/fish.ico")
    args = ap.parse_args()

    im = Image.open(args.src).convert("RGBA")
    head = head_crop(im)

    sizes = [16, 24, 32, 48, 64, 128, 256]
    base = 256
    canvas = Image.new("RGBA", (base, base), (0, 0, 0, 0))

    # 圆形底板（避免小尺寸下透明边缘发灰）
    mask = Image.new("L", (base, base), 0)
    ImageDraw.Draw(mask).ellipse((1, 1, base - 2, base - 2), fill=255)
    plate = Image.new("RGBA", (base, base), (246, 249, 255, 255))

    # 头部按比例填满圆形
    scale = base / float(max(head.size))
    hw = int(head.width * scale)
    hh = int(head.height * scale)
    head2 = head.resize((hw, hh), Image.LANCZOS)
    ox = (base - hw) // 2
    oy = int(base * 0.02)
    canvas.alpha_composite(plate, (0, 0))
    canvas.alpha_composite(head2, (ox, oy))
    canvas.putalpha(mask)

    canvas.save(args.dst, format="ICO", sizes=[(s, s) for s in sizes])
    print("图标已生成: %s  尺寸: %s" % (args.dst, ", ".join(str(s) for s in sizes)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
