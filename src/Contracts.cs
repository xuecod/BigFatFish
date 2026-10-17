// Contracts.cs —— 大肥鱼桌宠：共享类型契约
// 目标框架：.NET Framework 4.x，C# 5 语法（由 Framework64\v4.0.30319\csc.exe 编译）
using System;
using System.Drawing;

namespace FishPet
{
    /// <summary>桌宠情绪/动作状态</summary>
    public enum PetMood
    {
        Idle = 0,        // 发呆呼吸
        Blink = 1,       // 眨眼
        Happy = 2,       // 开心（被摸头）
        Surprised = 3,   // 惊吓
        Sleepy = 4,      // 犯困
        Sleep = 5,       // 睡着了
        Eat = 6,         // 吃东西
        Drag = 7,        // 被拎起来
        Walk = 8,        // 游动/走路
        Focus = 9,       // 专注（番茄钟）
        Relax = 10,      // 休息（番茄钟休息）
        Sad = 11,        // 委屈
        Love = 12,       // 亲密度满/喜欢
        Dizzy = 13,      // 撞墙转晕
        Sick = 14,       // 发烧（CPU 温度过高）
        Cheer = 15,      // 加油打气
        EatRice = 16     // 双手捧着铁盆扒白米饭（本鱼招牌动作）
    }

    /// <summary>可穿戴/可收集的装扮</summary>
    public enum Accessory
    {
        None = 0,
        StrawHat = 1,      // 草帽
        Sunglasses = 2,    // 墨镜
        BowTie = 3,        // 蝴蝶结
        Scarf = 4,         // 围巾
        Crown = 5,         // 王冠
        SantaHat = 6,      // 圣诞帽
        DivingMask = 7,    // 潜水镜
        BambooCopter = 8,  // 竹蜻蜓
        Headphones = 9,    // 耳机
        RubberDuck = 10,   // 小黄鸭
        Flower = 11,       // 头花
        WizardHat = 12     // 巫师帽
    }

    /// <summary>一帧的绘制姿态。Art.DrawFish 完全由它决定画面。</summary>
    public struct PetPose
    {
        public PetMood Mood;
        public float Time;        // 秒，用于周期性动作
        public float Blink;       // 0=睁眼, 1=闭眼
        public float Squash;      // 纵向缩放，1.0 为正常（<1 压扁，>1 拉长）
        public float MouthOpen;   // 0..1 张嘴
        public float Tilt;        // 旋转角度（度）
        public float Bob;         // 垂直偏移（像素，正数向下）
        public bool FacingLeft;   // true = 朝左
        public Accessory Accessory;
        public float Happy;       // 0..1，影响腮红与嘴角
        public float Rice;        // 0..1，铁盆里剩下的白饭量（EatRice 用；1=满满一盆，0=扒光了）

        public static PetPose Default
        {
            get
            {
                PetPose p = new PetPose();
                p.Mood = PetMood.Idle;
                p.Time = 0f;
                p.Blink = 0f;
                p.Squash = 1f;
                p.MouthOpen = 0f;
                p.Tilt = 0f;
                p.Bob = 0f;
                p.FacingLeft = false;
                p.Accessory = Accessory.None;
                p.Happy = 0.3f;
                p.Rice = 1f;
                return p;
            }
        }
    }

    /// <summary>全局配色（大肥鱼 = 蓝色圆滚滚胖鱼，参照抖音/DeepSeek 二创形象）</summary>
    public static class Palette
    {
        public static readonly Color BodyDeep = Color.FromArgb(255, 40, 79, 168);   // 背部深蓝
        public static readonly Color BodyMid = Color.FromArgb(255, 77, 124, 232);   // 主体蓝
        public static readonly Color BodyLight = Color.FromArgb(255, 133, 175, 250);// 高光蓝
        public static readonly Color Belly = Color.FromArgb(255, 238, 245, 255);    // 肚皮白
        public static readonly Color BellyShade = Color.FromArgb(255, 205, 224, 250);
        public static readonly Color Outline = Color.FromArgb(255, 26, 52, 112);    // 描边深蓝
        public static readonly Color Fin = Color.FromArgb(255, 108, 156, 244);      // 鳍
        public static readonly Color EyeWhite = Color.FromArgb(255, 255, 255, 255);
        public static readonly Color Pupil = Color.FromArgb(255, 22, 42, 74);
        public static readonly Color Blush = Color.FromArgb(255, 255, 150, 168);
        public static readonly Color Lips = Color.FromArgb(255, 255, 142, 163);
        public static readonly Color Bubble = Color.FromArgb(150, 220, 240, 255);
        public static readonly Color BubbleEdge = Color.FromArgb(200, 255, 255, 255);
        public static readonly Color Accent = Color.FromArgb(255, 77, 107, 254);    // DeepSeek 蓝

        // 面板/气泡用色
        public static readonly Color PanelBg = Color.FromArgb(238, 22, 32, 56);
        public static readonly Color PanelEdge = Color.FromArgb(200, 90, 140, 240);
        public static readonly Color PanelText = Color.FromArgb(232, 241, 255);
        public static readonly Color PanelSub = Color.FromArgb(150, 175, 215);
        public static readonly Color BubbleBack = Color.FromArgb(242, 255, 255, 255);
        public static readonly Color BubbleText = Color.FromArgb(28, 46, 82);
        public static readonly Color BarGreen = Color.FromArgb(92, 214, 168);
        public static readonly Color BarYellow = Color.FromArgb(250, 205, 96);
        public static readonly Color BarRed = Color.FromArgb(250, 110, 120);
    }
}
