#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
make_anim.py —— 用一张静态立绘生成桌宠动画帧（不手绘角色，只做图像变形）

原理：对原图做平滑位移场变形（头顶摆动、整体呼吸、鲸鱼尾摆动），眨眼用"眼皮滑落"采样实现。
输出：assets/sprites/anim/<动作>_NN.png + manifest.txt（帧数、帧率）
用法：python make_anim.py --in assets/sprites/idle.png --outdir assets/sprites/anim
"""
import argparse
import os
import sys
import numpy as np
from PIL import Image

# ---------------- 基础工具 ----------------
def load_rgba(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(np.float32)
    return a  # H,W,4 premultiplied? no -> straight alpha


def sample(img, sx, sy):
    """双线性采样（边界外透明）"""
    h, w = img.shape[:2]
    x0 = np.floor(sx).astype(np.int32)
    y0 = np.floor(sy).astype(np.int32)
    x1 = x0 + 1
    y1 = y0 + 1
    fx = (sx - x0)[..., None]
    fy = (sy - y0)[..., None]
    out = np.zeros((h, w, 4), dtype=np.float32)

    def gather(xi, yi):
        valid = (xi >= 0) & (xi < w) & (yi >= 0) & (yi < h)
        xi2 = np.clip(xi, 0, w - 1)
        yi2 = np.clip(yi, 0, h - 1)
        vals = img[yi2, xi2]
        vals = vals * valid[..., None]
        return vals

    c00 = gather(x0, y0)
    c10 = gather(x1, y0)
    c01 = gather(x0, y1)
    c11 = gather(x1, y1)
    out = (c00 * (1 - fx) * (1 - fy) + c10 * fx * (1 - fy) +
           c01 * (1 - fx) * fy + c11 * fx * fy)
    return out


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def warp(img, dx, dy):
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    return sample(img, xx + dx, yy + dy)


# ---------------- 变形参数 ----------------
def make_frame(img, t, amp_sway=1.0, amp_breath=1.0, amp_tail=1.0, blink=0.0,
               eye_boxes=None, tail_box=None, lean=0.0):
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    yn = yy / max(1.0, h - 1.0)          # 0..1
    xn = xx / max(1.0, w - 1.0)

    # 1) 整体呼吸：以底边为锚的纵向缩放
    s = 0.012 * amp_breath * np.sin(t * 2 * np.pi)
    dy = (yy - (h - 1)) * s

    # 2) 头顶摆动：越靠上位移越大（像脖子带动头部）
    head_w = smoothstep((0.62 - yn) / 0.62) ** 1.4
    dx = amp_sway * 2.6 * head_w * np.sin(t * 2 * np.pi)
    dy += amp_sway * 1.1 * head_w * np.cos(t * 2 * np.pi)

    # 3) 身体左右倾摆（整体轻微）
    dy += lean * (xn - 0.5) * 6.0

    # 4) 鲸鱼尾摆动：只在右下方区域，绕根部旋转
    if tail_box is not None and amp_tail > 0:
        tx0, ty0, tx1, ty1 = tail_box
        mask = np.zeros((h, w), dtype=np.float32)
        mask[ty0:ty1, tx0:tx1] = 1.0
        # 平滑边缘
        mimg = Image.fromarray((mask * 255).astype(np.uint8), mode="L")
        from PIL import ImageFilter
        mimg = mimg.filter(ImageFilter.GaussianBlur(radius=max(3, (tx1 - tx0) // 12)))
        mask = np.asarray(mimg).astype(np.float32) / 255.0
        angle = 0.16 * amp_tail * np.sin(t * 2 * np.pi + 0.6)
        rx, ry = float(tx0) + 6.0, float(ty0) + (ty1 - ty0) * 0.25
        ca, sa = np.cos(angle), np.sin(angle)
        px, py = xx - rx, yy - ry
        wx = ca * px - sa * py + rx
        wy = sa * px + ca * py + ry
        dx = dx * (1 - mask) + (wx - xx) * mask
        dy = dy * (1 - mask) + (wy - yy) * mask

    out = warp(img, dx, dy)

    # 5) 眨眼：眼睛纵向压扁成一条缝，用眼睛下方的脸颊肤色补位（纯图像处理，不画线）
    if blink > 0.001 and eye_boxes:
        out = blink_eyes(out, eye_boxes, blink)
    return np.clip(out, 0, 255)


def blink_eyes(arr, eye_boxes, amount):
    h, w = arr.shape[:2]
    out = arr.copy()
    from PIL import ImageFilter

    for (ex0, ey0, ex1, ey1) in eye_boxes:
        x0 = max(0, int(ex0)); y0 = max(0, int(ey0))
        x1 = min(w, int(ex1)); y1 = min(h, int(ey1))
        ew, eh = x1 - x0, y1 - y0
        if ew < 4 or eh < 4:
            continue

        # 肤色来源：眼睛正下方一条（脸颊），拉伸到眼睛尺寸
        sy0 = min(h - 2, y1 + 4)
        sy1 = min(h - 1, sy0 + max(6, eh // 3))
        strip = out[sy0:sy1, x0:x1]
        if strip.size == 0:
            continue
        skin_img = Image.fromarray(np.clip(strip, 0, 255).astype(np.uint8), "RGBA").resize((ew, eh), Image.BILINEAR)
        skin_img = skin_img.filter(ImageFilter.GaussianBlur(radius=max(1.0, eh * 0.06)))
        base = np.asarray(skin_img).astype(np.float32)

        # 眼睛本身压扁成细缝（保留睫毛/虹膜的暗色，形成"闭眼线"）
        eye = Image.fromarray(np.clip(out[y0:y1, x0:x1], 0, 255).astype(np.uint8), "RGBA")
        k = max(0.10, 1.0 - 0.9 * amount)
        nh = max(2, int(eh * k))
        sq = np.asarray(eye.resize((ew, nh), Image.LANCZOS)).astype(np.float32)

        top = (eh - nh) // 2
        sa = sq[:, :, 3:4] / 255.0
        base[top:top + nh, :, :3] = sq[:, :, :3] * sa + base[top:top + nh, :, :3] * (1 - sa)

        # 软边遮罩把处理后的眼睛区域贴回整帧
        m = np.zeros((h, w), dtype=np.float32)
        m[y0:y1, x0:x1] = 1.0
        mimg = Image.fromarray((m * 255).astype(np.uint8), "L").filter(
            ImageFilter.GaussianBlur(radius=max(1.2, eh * 0.08)))
        mm = (np.asarray(mimg).astype(np.float32) / 255.0)[..., None]

        patch = np.zeros((h, w, 4), dtype=np.float32)
        patch[y0:y1, x0:x1] = base
        out = out * (1 - mm) + patch * mm
    return out


def save(arr, path):
    Image.fromarray(arr.astype(np.uint8), mode="RGBA").save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--outdir", required=True)
    ap.add_argument("--frames", type=int, default=12)
    ap.add_argument("--eye", default="", help="眼睛框 x0,y0,x1,y1;x0,y0,x1,y1（可空）")
    ap.add_argument("--tail", default="", help="尾巴区域 x0,y0,x1,y1（可空）")
    args = ap.parse_args()

    img = load_rgba(args.src)
    h, w = img.shape[:2]
    os.makedirs(args.outdir, exist_ok=True)

    eye_boxes = []
    if args.eye:
        for part in args.eye.split(";"):
            if part.strip():
                eye_boxes.append(tuple(int(float(v)) for v in part.split(",")))
    tail_box = None
    if args.tail:
        tail_box = tuple(int(float(v)) for v in args.tail.split(","))

    # 动作表：名称, 帧数, 帧率, 摆动, 呼吸, 尾巴, 倾摆, 眨眼帧
    # 说明：单张立绘做眨眼会糊（眼睛上方是刘海、下方是阴影），所以默认不眨眼，只做变形动画。
    NB = set()
    plans = [
        ("idle",   args.frames, 10, 1.0, 1.0, 1.0, 0.0, NB),
        ("walk",   args.frames, 12, 1.6, 0.6, 1.4, 1.0, NB),
        ("happy",  10,           12, 1.2, 1.6, 1.2, 0.0, NB),
        ("sleep",  10,           6,  0.25, 1.8, 0.3, 0.0, NB),
        ("drag",   8,            12, 1.8, 0.4, 1.6, 1.6, NB),
        ("sad",    8,            6,  0.4, 0.8, 0.4, 0.0, NB),
        ("focus",  8,            8,  0.3, 0.9, 0.2, 0.0, NB),
        ("relax",  8,            7,  0.6, 1.2, 0.8, 0.0, NB),
        ("sick",   8,            7,  0.4, 0.7, 0.3, 0.0, NB),
        ("cheer",  8,            14, 1.4, 1.5, 1.3, 0.0, NB),
        ("dizzy",  8,            14, 2.0, 0.5, 0.8, 2.2, NB),
        ("surprise", 6,          14, 1.0, 1.8, 0.6, 0.0, NB),
        ("eat",    8,            12, 0.5, 1.2, 0.3, 0.0, NB),
    ]

    manifest = []
    for name, n, fps, sw, br, tl, ln, blink_frames in plans:
        for i in range(n):
            t = i / float(n)
            blink = 0.0
            if i in blink_frames:
                blink = 0.85
            elif (i - 1) in blink_frames or (i + 1) in blink_frames:
                blink = 0.35
            arr = make_frame(img, t, amp_sway=sw, amp_breath=br, amp_tail=tl,
                             blink=blink, eye_boxes=eye_boxes, tail_box=tail_box, lean=ln)
            save(arr, os.path.join(args.outdir, "%s_%02d.png" % (name, i)))
        manifest.append("%s %d %d" % (name, n, fps))
        print("  %-9s %2d 帧 @ %d fps" % (name, n, fps))

    with open(os.path.join(args.outdir, "manifest.txt"), "w", encoding="utf-8") as f:
        f.write("# pose frames fps\n")
        f.write("\n".join(manifest) + "\n")
    print("完成，输出目录：" + args.outdir)


if __name__ == "__main__":
    sys.exit(main())
