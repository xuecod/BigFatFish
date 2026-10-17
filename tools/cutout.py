#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
cutout.py —— 把带背景的插画裁成透明立绘 PNG（桌宠精灵图）

用法示例：
  python cutout.py --in 原图.jpg --out assets/sprites/idle.png --mode white --trim --max 560 --checker 预览.png
  python cutout.py --in 原图.jpg --out x.png --mode color --seed 0.38,0.55 --poly "0.3,0.05;0.5,0.07;..." --trim

模式：
  white  —— 纯色/白色背景：从四边泛洪，把与边缘连通的近白像素设为透明（不会打穿角色内部白色）
  color  —— 花色背景：按颜色规则挑出角色（冷色/深色/中性白/肤色），再从种子点做测地膨胀取连通域
  sharp  —— 虚化背景：按"细节能量"分割（背景虚化能量低，角色锐利能量高）
  poly   —— 纯手工多边形遮罩（配合 --shrink 往里收）

附加：
  --erase x0,y0,x1,y1   抠图前把矩形填白（去掉文字气泡），可多次给
  --erase-mask x0,y0,x1,y1  poly 模式里从遮罩挖掉矩形
  --trim                按 alpha 裁到内容外接框
  --max N               最长边缩放到 N
  --checker PATH        额外输出棋盘底预览（校验 alpha）
"""
import argparse
import sys
import numpy as np
from PIL import Image, ImageFilter


def parse_rect(s):
    parts = [int(float(v)) for v in s.replace(" ", "").split(",")]
    if len(parts) != 4:
        raise ValueError("矩形要写成 x0,y0,x1,y1：" + s)
    return parts


def geodesic_fill(seed, allowed):
    """在 allowed 内从 seed 出发做 4 连通测地膨胀（步长倍增，几十次迭代收敛）"""
    cur = (seed & allowed).astype(bool)
    if not cur.any():
        return cur
    h, w = allowed.shape
    k = 1
    while True:
        d = np.zeros_like(cur)
        kk = min(k, h - 1, w - 1)
        if kk < 1:
            break
        d[kk:, :] |= cur[:-kk, :]
        d[:-kk, :] |= cur[kk:, :]
        d[:, kk:] |= cur[:, :-kk]
        d[:, :-kk] |= cur[:, kk:]
        nxt = cur | (d & allowed)
        if np.array_equal(nxt, cur):
            if k == 1:
                break
            k = 1
            continue
        cur = nxt
        k = min(k * 2, h - 1, w - 1)
    return cur


def border_flood(candidate):
    h, w = candidate.shape
    seed = np.zeros_like(candidate)
    seed[0, :] = candidate[0, :]
    seed[-1, :] = candidate[-1, :]
    seed[:, 0] = candidate[:, 0]
    seed[:, -1] = candidate[:, -1]
    return geodesic_fill(seed, candidate)


def fill_holes(mask):
    inv = ~mask
    outside = border_flood(inv)
    return mask | (~outside)


def poly_mask(spec, w, h):
    from PIL import ImageDraw
    pts = []
    for p in spec.split(";"):
        if not p.strip():
            continue
        a, b = [float(v) for v in p.split(",")]
        pts.append((a * w if a <= 1.0 else a, b * h if b <= 1.0 else b))
    pm = Image.new("L", (w, h), 0)
    ImageDraw.Draw(pm).polygon(pts, fill=255)
    return np.asarray(pm) > 127


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", dest="dst", required=True)
    ap.add_argument("--mode", choices=["white", "color", "sharp", "poly"], default="white")
    ap.add_argument("--crop", default=None)
    ap.add_argument("--erase", action="append", default=[])
    ap.add_argument("--erase-mask", action="append", default=[])
    ap.add_argument("--seed", default=None)
    ap.add_argument("--poly", default=None)
    ap.add_argument("--shrink", type=int, default=0)
    ap.add_argument("--white-thresh", type=int, default=236)
    ap.add_argument("--white-spread", type=int, default=26)
    ap.add_argument("--grow", type=int, default=0)
    ap.add_argument("--dark-lum", type=float, default=0.44)
    ap.add_argument("--blue-margin", type=int, default=6)
    ap.add_argument("--sharp-blur", type=float, default=6.0)
    ap.add_argument("--sharp-box", type=float, default=7.0)
    ap.add_argument("--sharp-thresh", type=float, default=0.06)
    ap.add_argument("--trim", action="store_true")
    ap.add_argument("--max", type=int, default=0)
    ap.add_argument("--feather", type=float, default=0.8)
    ap.add_argument("--checker", default=None)
    ap.add_argument("--close-size", type=int, default=3)
    ap.add_argument("--close-rounds", type=int, default=2)
    ap.add_argument("--median", type=int, default=0)
    ap.add_argument("--debug-points", default=None)
    args = ap.parse_args()

    img = Image.open(args.src).convert("RGB")
    if args.crop:
        img = img.crop(tuple(parse_rect(args.crop)))

    if args.erase:
        arr = np.asarray(img).copy()
        for r in args.erase:
            x0, y0, x1, y1 = parse_rect(r)
            x0 = max(0, x0); y0 = max(0, y0)
            x1 = min(arr.shape[1], x1); y1 = min(arr.shape[0], y1)
            arr[y0:y1, x0:x1] = 255
        img = Image.fromarray(arr)

    a = np.asarray(img).astype(np.int16)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx = a.max(axis=2)
    mn = a.min(axis=2)
    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    h, w = lum.shape

    if args.mode == "white":
        whiteish = (mn >= args.white_thresh) & ((mx - mn) <= args.white_spread)
        if args.grow > 0:
            near_white = (mn >= args.white_thresh - args.grow) & ((mx - mn) <= args.white_spread + args.grow)
            bg = border_flood(whiteish) | (border_flood(near_white) & near_white)
        else:
            bg = border_flood(whiteish)
        opaque = fill_holes(~bg)
        alpha = np.where(opaque, 255, 0).astype(np.uint8)
    elif args.mode == "poly":
        mask = poly_mask(args.poly or "", w, h)
        if args.shrink > 0:
            mk = Image.fromarray((mask * 255).astype(np.uint8), mode="L")
            for _ in range(args.shrink):
                mk = mk.filter(ImageFilter.MinFilter(3))
            mask = np.asarray(mk) > 127
        for rm in args.erase_mask:
            x0, y0, x1, y1 = parse_rect(rm)
            mask[y0:y1, x0:x1] = False
        alpha = np.where(mask, 255, 0).astype(np.uint8)
    elif args.mode == "sharp":
        gray = Image.fromarray(np.clip(lum * 255, 0, 255).astype(np.uint8), mode="L")
        blur = np.asarray(gray.filter(ImageFilter.GaussianBlur(args.sharp_blur)), dtype=np.float32) / 255.0
        detail = np.abs(lum - blur)
        d_img = Image.fromarray(np.clip(detail * 255 * 4, 0, 255).astype(np.uint8), mode="L")
        energy = np.asarray(d_img.filter(ImageFilter.GaussianBlur(args.sharp_box)), dtype=np.float32) / 255.0
        core = energy > args.sharp_thresh
        if args.poly:
            core = core & poly_mask(args.poly, w, h)
        core = fill_holes(core)
        alpha = np.where(core, 255, 0).astype(np.uint8)
    else:
        cool = (b.astype(int) > r.astype(int) + args.blue_margin)
        dark = lum < args.dark_lum
        neutral_white = (mn >= 196) & ((mx - mn) <= 34)
        skin = (r.astype(int) > b.astype(int) + 14) & (lum > 0.52) & (lum < 0.95) & (mx - mn < 110)
        allowed = cool | dark | neutral_white | skin
        if args.poly:
            allowed = allowed & poly_mask(args.poly, w, h)
        if not args.seed:
            print("color 模式需要 --seed", file=sys.stderr)
            return 2
        sx, sy = [float(v) for v in args.seed.split(",")]
        px = int(sx * w) if sx <= 1.0 else int(sx)
        py = int(sy * h) if sy <= 1.0 else int(sy)
        seed = np.zeros_like(allowed)
        seed[max(0, py - 3):py + 4, max(0, px - 3):px + 4] = True
        core = fill_holes(geodesic_fill(seed, allowed))
        alpha = np.where(core, 255, 0).astype(np.uint8)

    if args.debug_points:
        print("--- 调试点（alpha）---")
        for p in [x for x in args.debug_points.split(";") if x.strip()]:
            x, y = [int(float(v)) for v in p.split(",")]
            if 0 <= y < a.shape[0] and 0 <= x < a.shape[1]:
                print("  (%d,%d) rgb=%s alpha=%d" % (x, y, tuple(a[y, x]), int(alpha[y, x])))

    am = Image.fromarray(alpha, mode="L")
    cs = args.close_size if args.close_size % 2 == 1 else args.close_size + 1
    for _ in range(max(0, args.close_rounds)):
        am = am.filter(ImageFilter.MaxFilter(cs)).filter(ImageFilter.MinFilter(cs))
    if args.median and args.median >= 3:
        am = am.filter(ImageFilter.MedianFilter(args.median if args.median % 2 == 1 else args.median + 1))
    if args.feather and args.feather > 0:
        am = am.filter(ImageFilter.GaussianBlur(args.feather))

    out = img.convert("RGBA")
    out.putalpha(am)
    if args.trim:
        bbox = am.point(lambda v: 255 if v > 8 else 0).getbbox()
        print("   trim 前尺寸 %s alpha外接框 %s" % (out.size, bbox))
        if bbox:
            x0, y0, x1, y1 = bbox
            pad = 2
            bbox = (max(0, x0 - pad), max(0, y0 - pad), min(out.width, x1 + pad), min(out.height, y1 + pad))
            out = out.crop(bbox)
    if args.max and max(out.size) > args.max:
        scale = args.max / float(max(out.size))
        out = out.resize((max(1, int(out.width * scale)), max(1, int(out.height * scale))), Image.LANCZOS)

    out.save(args.dst)

    if args.checker:
        ow, oh = out.size
        bx = Image.new("RGB", (ow, oh), (255, 255, 255))
        px = bx.load()
        step = 16
        for yy in range(0, oh, step):
            for xx in range(0, ow, step):
                if ((xx // step) + (yy // step)) % 2 == 0:
                    for y2 in range(yy, min(oh, yy + step)):
                        for x2 in range(xx, min(ow, xx + step)):
                            px[x2, y2] = (205, 210, 220)
        bx = Image.alpha_composite(bx.convert("RGBA"), out).convert("RGB")
        bx.save(args.checker)

    cov = (np.asarray(out)[:, :, 3] > 8).mean()
    print("OK %s  %dx%d  不透明占比 %.1f%%" % (args.dst, out.width, out.height, cov * 100))
    return 0


if __name__ == "__main__":
    sys.exit(main())
