// Speech.cs —— 台词库（鱼味 + 天选机联动）
using System;
using System.Collections.Generic;
using System.Text;

namespace FishPet
{
    public static class Speech
    {
        private static readonly Random _rnd = new Random();

        private static readonly Dictionary<string, string[]> Lines = Build();

        private static Dictionary<string, string[]> Build()
        {
            Dictionary<string, string[]> d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            d["greeting"] = new string[] {
                "扑通！大肥鱼上线啦～",
                "叮！你的专属摸鱼搭子已就位",
                "我来了我来了，今天也要好好摸鱼哦",
                "水面有点吵，是谁在敲键盘呀？",
                "天选之鱼报道！本鱼只吃白饭，不干活",
                "咕噜咕噜……先让我看看今天的电量和温度"
            };
            d["click"] = new string[] {
                "哎呀，别戳肚子啦，痒！",
                "咕噜？你叫我？",
                "再摸一下就不客气了哦～",
                "嘿嘿，是来陪我玩的吗？",
                "本鱼正在认真发呆，勿扰",
                "摸头就摸头，别揪鳍呀"
            };
            d["headpat"] = new string[] {
                "唔……舒服，继续继续～",
                "今天也是被摸头的一天",
                "亲密度 +1，本鱼记下了",
                "咕噜咕噜（开心得冒泡）"
            };
            d["dragstart"] = new string[] {
                "哇——放我下来！",
                "我在飞！我真的在飞！",
                "轻点轻点，我可是很重的鱼",
                "水面在下面，别松手啊喂"
            };
            d["drop"] = new string[] {
                "咚！屁股着地……",
                "呼，还是地面踏实",
                "下次提前说一声好不好",
                "换个地方继续发呆，挺好"
            };
            d["wall"] = new string[] {
                "哐！谁把屏幕边界放这儿的",
                "撞到啦，转三圈冷静一下",
                "本鱼只是路过，真的",
                "呼……这墙有点硬"
            };
            d["feed"] = new string[] {
                "小鱼干！是小鱼干！",
                "谢谢投喂，本鱼胃口很好",
                "咔嚓咔嚓……好吃",
                "再来一份我也不介意的"
            };
            d["rice"] = new string[] {
                "铁盆！我的铁盆呢——",
                "白饭最香了，别的都不要",
                "本鱼只吃白饭，谢谢",
                "扒饭扒饭，别跟我说话",
                "这一盆……我能吃完",
                "干饭啦！盆给我端稳了"
            };
            d["rice_self"] = new string[] {
                "没人投喂，本鱼自己扒一盆",
                "饿了，铁盆伺候",
                "白饭时间到，谁也别抢",
                "咕噜……先干为敬（一盆）"
            };
            d["rice_done"] = new string[] {
                "嗝～一盆下肚，满足了",
                "盆底都舔干净了，一点不浪费",
                "再来一盆也行的（小声）",
                "吃饱了，可以继续吃白饭了"
            };
            d["rice_empty"] = new string[] {
                "盆怎么是空的……本鱼不同意",
                "没饭了？那我先睡一会儿",
                "白饭呢？本鱼要白饭！"
            };
            d["hungry"] = new string[] {
                "肚子……有点空空的",
                "喂，本鱼快饿成鱼干了",
                "有吃的吗？什么都行，白饭也行",
                "咕噜（这是饿的声音，不是困）"
            };
            d["sleep"] = new string[] {
                "眼皮好重……先睡一小会儿……",
                "呼……呼……（吹出一个大泡泡）",
                "夜深了，本鱼要沉底休息了",
                "别吵，梦里有一整片海"
            };
            d["wake"] = new string[] {
                "唔……谁把我叫醒了？",
                "（泡泡啪地破了）我醒了！",
                "早上好，今天也一起摸鱼吧"
            };
            d["idle"] = new string[] {
                "摸鱼一时爽，一直摸鱼一直爽",
                "本鱼今天的工作量：0，成果：快乐",
                "你说，鱼会不会也上班呢",
                "刚数了一下，我这身肉都是白饭变的",
                "别看了，我没有在偷懒，我在思考",
                "水面上的光好好看，可惜我上不去",
                "如果努力有用，还要天赋干什么——摸鱼吧",
                "今天的我，比昨天更圆了一点",
                "你打字好快，我在旁边看着都累",
                "要不要一起来个番茄钟？我监督你"
            };
            d["procrastinate"] = new string[] {
                "先去接杯水吧，顺便摸五分钟鱼",
                "这活儿看着好难，我们不如先休息",
                "先别急，拖延是效率的孪生兄弟",
                "本鱼批准你，休息十分钟"
            };
            d["focus_start"] = new string[] {
                "叮！专注模式启动，加油！",
                "番茄钟开始，我陪你一起专心",
                "这段时间不许摸我，专心干活",
                "交出你的注意力，25 分钟就好"
            };
            d["focus_end"] = new string[] {
                "恭喜你，又收获一个番茄！",
                "干得漂亮！休息一下吧",
                "专注完成，本鱼给你鼓掌（用鳍）"
            };
            d["focus_urge"] = new string[] {
                "还不能休息哦，继续加油，我陪你",
                "别分心呀，剩下的很快就做完了",
                "坚持一下下，本鱼在旁边看着你呢"
            };
            d["break_start"] = new string[] {
                "休息时间到！起来走两步",
                "眼睛需要休息一下，看看远方吧",
                "放松片刻，待会儿再战！"
            };
            d["break_end"] = new string[] {
                "休息结束，我们来下一个番茄",
                "起来啦，新的一轮开始啦",
                "坐久了记得起身走一走呦"
            };
            d["hot"] = new string[] {
                "好烫！谁在烤鱼？",
                "温度有点高，风扇在拼命转了",
                "本鱼快被煮熟了，开个性能模式降降温？",
                "这温度……要不要歇一会儿再跑"
            };
            d["cool"] = new string[] {
                "温度很舒服，本鱼状态良好",
                "凉凉的，适合继续干活"
            };
            d["highload"] = new string[] {
                "CPU 忙成这样，是在跑什么好东西？",
                "风扇都吼起来了，本鱼听着有点紧张",
                "负载很高哦，注意别把机器累着"
            };
            d["lowbattery"] = new string[] {
                "电量不多了，本鱼有点心慌",
                "快插电吧，我不想在最后 1% 睡着",
                "低电量警告！再玩下去本鱼要断电了"
            };
            d["charging"] = new string[] {
                "充电中……能量涌入！",
                "插上电啦，本鱼又有精神了",
                "充能！充能！满电本鱼最可爱"
            };
            d["fullbattery"] = new string[] {
                "电量满格！本鱼满血复活",
                "充满啦，可以拔线自由游动了"
            };
            d["memwarn"] = new string[] {
                "内存快满了，本鱼游不动了",
                "开太多东西啦，关几个吧"
            };
            d["netbusy"] = new string[] {
                "网速飞快，是在下什么好东西？",
                "有人在偷偷下载吧，本鱼看到了"
            };
            d["idle_user"] = new string[] {
                "人呢？本鱼自己玩一会儿",
                "（自己吐了个泡泡，看着它飘上去）",
                "你不在的时候，我就数水波"
            };
            d["night"] = new string[] {
                "这么晚还不睡？本鱼陪你，但别太晚",
                "深夜的鱼最清醒，也最困",
                "熬夜的话，明天记得早点摸鱼补回来"
            };
            d["gacha_rare"] = new string[] {
                "哇！稀有的！本鱼运气爆棚",
                "金色！是金色的扭蛋！",
                "手气不错嘛，快戴上看看"
            };
            d["gacha_common"] = new string[] {
                "叮～又收获一件装扮",
                "看看扭到了什么呀～",
                "收进背包啦，随时能换"
            };
            d["gacha_dup"] = new string[] {
                "这个已经有了……换成鱼丸给你吧",
                "重复啦，本鱼把鱼丸退给你"
            };
            d["nocoin"] = new string[] {
                "鱼丸不够啦，先去专注一会儿赚鱼丸吧",
                "扭蛋机空了……本鱼的鱼丸呢",
                "没鱼丸了，摸摸我也可以的"
            };
            d["alarm"] = new string[] {
                "叮铃铃！到点啦！",
                "闹钟响啦，别装没听见",
                "该做正事啦，本鱼提醒过你了"
            };
            d["levelup"] = new string[] {
                "亲密度升级！本鱼越来越喜欢你了",
                "我们又熟了一点，嘿嘿"
            };
            d["sick"] = new string[] {
                "本鱼有点发烧……让我缓一缓",
                "温度太高了，我冒热气了"
            };
            d["tosspon"] = new string[] {
                "工作嘛，差不多得了",
                "要不……明天再做？",
                "本鱼建议：先摸一小时鱼"
            };
            d["menu_hint"] = new string[] {
                "右键点我，有好多功能哦",
                "双击试试？有惊喜（也许）"
            };
            d["morning"] = new string[] {
                "早上好！今天的白饭准备好了吗",
                "早呀，先看看电量，再决定摸多久"
            };
            return d;
        }

        private static string Pick(string key)
        {
            string[] arr;
            if (!Lines.TryGetValue(key, out arr) || arr == null || arr.Length == 0) return "";
            return arr[_rnd.Next(arr.Length)];
        }

        public static string Category(string key) { return Pick(key); }

        public static string Greeting()
        {
            int h = DateTime.Now.Hour;
            if (h >= 5 && h < 11) return Pick("morning");
            if (h >= 23 || h < 5) return Pick("night");
            return Pick("greeting");
        }

        public static string OnClick() { return Pick("click"); }
        public static string OnHeadPat() { return Pick("headpat"); }
        public static string OnDragStart() { return Pick("dragstart"); }
        public static string OnDrop() { return Pick("drop"); }
        public static string OnWallHit() { return Pick("wall"); }
        public static string OnFeed() { return Pick("feed"); }
        public static string OnRice() { return Pick("rice"); }
        public static string OnRiceSelfServe() { return Pick("rice_self"); }
        public static string OnRiceDone() { return Pick("rice_done"); }
        public static string OnRiceEmpty() { return Pick("rice_empty"); }
        public static string OnHungry() { return Pick("hungry"); }
        public static string OnSleep() { return Pick("sleep"); }
        public static string OnWake() { return Pick("wake"); }

        public static string OnIdleChatter()
        {
            if (DateTime.Now.Hour >= 23 || DateTime.Now.Hour < 5)
            {
                if (_rnd.Next(100) < 35) return Pick("night");
            }
            if (_rnd.Next(100) < 12) return Pick("tosspon");
            if (_rnd.Next(100) < 10) return Pick("procrastinate");
            return Pick("idle");
        }

        public static string OnFocusStart() { return Pick("focus_start"); }
        public static string OnFocusEnd() { return Pick("focus_end"); }
        public static string OnFocusUrge() { return Pick("focus_urge"); }
        public static string OnBreakStart() { return Pick("break_start"); }
        public static string OnBreakEnd() { return Pick("break_end"); }
        public static string OnHot() { return Pick("hot"); }
        public static string OnHighLoad() { return Pick("highload"); }
        public static string OnMemWarn() { return Pick("memwarn"); }
        public static string OnNetBusy() { return Pick("netbusy"); }
        public static string OnUserAway() { return Pick("idle_user"); }
        public static string OnAlarm() { return Pick("alarm"); }
        public static string OnLevelUp() { return Pick("levelup"); }
        public static string OnNoCoin() { return Pick("nocoin"); }

        public static string OnGacha(bool rare, bool duplicate)
        {
            if (duplicate) return Pick("gacha_dup");
            return rare ? Pick("gacha_rare") : Pick("gacha_common");
        }

        public static string OnLowBattery(int pct)
        {
            return "电量只剩 " + pct + "% 了。" + Pick("lowbattery");
        }

        public static string OnCharging(int pct)
        {
            return Pick("charging") + "（当前 " + pct + "%）";
        }

        public static string OnFullBattery() { return Pick("fullbattery"); }

        public static string Sick() { return Pick("sick"); }
        public static string MenuHint() { return Pick("menu_hint"); }

        /// <summary>根据系统状态挑一句最相关的关心话（没必要时返回空）</summary>
        public static string ContextualLine(SysSnapshot s)
        {
            if (s == null) return "";
            if (s.HasBattery && s.BatteryPercent >= 0)
            {
                if (!s.AcOnline && s.BatteryPercent <= 20) return OnLowBattery(s.BatteryPercent);
                if (s.AcOnline && s.BatteryPercent >= 99) return OnFullBattery();
            }
            if (s.CpuTempC >= 88f) return OnHot();
            if (s.CpuLoad >= 88f) return OnHighLoad();
            if (s.MemPercent >= 92f) return OnMemWarn();
            if (s.NetDownKBps > 8192f) return OnNetBusy();
            return "";
        }

        /// <summary>今日鱼报文案</summary>
        public static string DailyReport(SysSnapshot s, Settings cfg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【大肥鱼今日鱼报】");
            sb.AppendLine("摸鱼时长：" + FormatMinutes(cfg.TodaySlackMinutes) + "（累计 " + FormatMinutes(cfg.TotalSlackMinutes) + "）");
            sb.AppendLine("番茄收获：" + cfg.PomodoroCount + " 个    投喂次数：" + cfg.FedCount + "    白饭盆数：" + cfg.RiceCount);
            sb.AppendLine("饱食度：" + ((int)cfg.Satiety) + "%    亲密度：" + ((int)cfg.Intimacy) + "%    鱼丸：" + cfg.Coins);
            if (s != null)
            {
                string memText = (s.MemTotalGB > 0f)
                    ? "内存：" + Fmt(s.MemPercent) + "%(" + s.MemUsedGB.ToString("0.0") + "/" + s.MemTotalGB.ToString("0.0") + "GB)"
                    : "内存：采样中";
                sb.AppendLine("CPU：" + Fmt(s.CpuLoad) + "%    " + memText);
                if (s.GpuUsage >= 0) sb.AppendLine("GPU：" + Fmt(s.GpuUsage) + "%  " + Fmt(s.GpuTempC) + "°C  风扇 " + Fmt(s.GpuFanPercent) + "%");
                if (s.CpuTempC > 0) sb.AppendLine("CPU 温度：" + Fmt(s.CpuTempC) + "°C");
                if (s.HasBattery) sb.AppendLine("电量：" + s.BatteryPercent + "%" + (s.AcOnline ? "（充电中）" : "（电池）"));
                if (s.UptimeHours >= 0) sb.AppendLine("开机时长：" + s.UptimeHours.ToString("0.0") + " 小时");
            }
            sb.AppendLine("本鱼今日评价：" + Verdict(cfg));
            return sb.ToString();
        }

        private static string Fmt(float v)
        {
            if (v < 0) return "--";
            return ((int)Math.Round(v)).ToString();
        }

        private static string FormatMinutes(double minutes)
        {
            if (minutes < 60) return ((int)minutes) + " 分钟";
            return ((int)(minutes / 60)) + " 小时 " + ((int)(minutes % 60)) + " 分钟";
        }

        private static string Verdict(Settings cfg)
        {
            if (cfg.TodaySlackMinutes > 180) return "摸得有点狠，本鱼佩服";
            if (cfg.PomodoroCount >= 4) return "专注力惊人，本鱼给你鞠躬";
            if (cfg.Satiety < 30) return "饿着肚子摸鱼，效率不高哦";
            if (cfg.Intimacy >= 80) return "我们已经是老搭档了";
            return "平稳的一天，继续保持";
        }
    }
}
