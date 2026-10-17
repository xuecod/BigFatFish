// PetBrain.cs —— 状态机、行为、系统联动、养成与统计（PetForm 的另一半）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FishPet
{
    public sealed partial class PetForm
    {
        // ---------- 情绪锁定（发烧/开心等临时覆盖）----------
        internal PetMood moodLockValue = PetMood.Idle;
        internal float moodLockLeft;

        // ---------- 漫游 ----------
        private float roamWait = 4f;
        private int roamDir = 1;
        private string lastAlarmMinute = "";
        private int lastBatteryPercent = -1;
        private readonly Random rnd = new Random();

        // ---------- 铁盆白饭 ----------
        internal float riceLevel = 1f;      // 盆里剩饭量 0..1
        private float chewCd;
        private float selfServeCd = 180f;   // 自己掏盆的冷却（秒）

        // ==================== 状态机 ====================

        internal void EnterState(PetState st, float duration, bool isMicro)
        {
            state = st;
            stateTime = 0f;
            stateDuration = duration;
            microAction = isMicro;
        }

        internal static PetState StateFor(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Walk: return PetState.Walk;
                case PetMood.Drag: return PetState.Drag;
                case PetMood.Eat: return PetState.Eat;
                case PetMood.EatRice: return PetState.EatRice;
                case PetMood.Sleep: return PetState.Sleep;
                case PetMood.Sleepy: return PetState.Sleepy;
                case PetMood.Focus: return PetState.Focus;
                case PetMood.Relax: return PetState.Relax;
                case PetMood.Dizzy: return PetState.Dizzy;
                case PetMood.Cheer: return PetState.Cheer;
                case PetMood.Surprised: return PetState.Surprised;
                default: return PetState.Idle;
            }
        }

        internal void UpdateBrain(float dt)
        {
            stateTime += dt;
            if (bubbleLeft > 0f) bubbleLeft -= dt;
            if (hudHintLeft > 0f) hudHintLeft -= dt;
            if (moodLockLeft > 0f) moodLockLeft -= dt;

            if (sayCooldown.Count > 0)
            {
                List<string> keys = new List<string>(sayCooldown.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    float v = sayCooldown[keys[i]] - dt;
                    if (v <= 0f) sayCooldown.Remove(keys[i]);
                    else sayCooldown[keys[i]] = v;
                }
            }

            UpdateBlink(dt);
            breathT += dt;

            Screen sc = CurrentScreen();
            Rectangle wa = sc.WorkingArea;
            float groundY = GroundY(sc);
            float feetY = Y + FishBottomOffset();

            switch (state)
            {
                case PetState.Idle:
                    if (cfg.FollowMouse) break;
                    if (cfg.WalkAround && bubbleLeft <= 0f)
                    {
                        roamWait -= dt;
                        if (roamWait <= 0f)
                        {
                            roamWait = 5f + (float)rnd.NextDouble() * 9f;
                            roamDir = rnd.Next(2) == 0 ? -1 : 1;
                            facingLeft = roamDir < 0;
                            EnterState(PetState.Walk, 6f + (float)rnd.NextDouble() * 11f, false);
                        }
                    }
                    break;

                case PetState.Walk:
                    {
                        float spd = (52f + cfg.Intimacy * 0.25f) * Math.Max(0.6f, cfg.Scale);
                        X += roamDir * spd * dt;
                        float leftLimit = wa.Left - canvasW * 0.30f;
                        float rightLimit = wa.Right - canvasW * 0.70f;
                        if (X <= leftLimit || X >= rightLimit)
                        {
                            roamDir = -roamDir;
                            facingLeft = roamDir < 0;
                            X = X <= leftLimit ? leftLimit : rightLimit;
                            SayThrottled("wall", Speech.OnWallHit(), 3f, 25f);
                            PlaySfx("dizzy");
                            EnterState(PetState.Dizzy, 0.9f, false);
                            break;
                        }
                        if (stateDuration > 0f && stateTime >= stateDuration)
                            EnterState(PetState.Idle, -1f, false);
                    }
                    break;

                case PetState.Sink:
                    {
                        VY += 90f * dt;
                        if (VY > 90f * Math.Max(0.6f, cfg.Scale)) VY = 90f * Math.Max(0.6f, cfg.Scale);
                        Y += VY * dt;
                        if (feetY >= groundY)
                        {
                            Y = groundY - FishBottomOffset();
                            VY = 0f;
                            PlaySfx("bubble");
                            EnterState(PetState.Idle, -1f, false);
                        }
                    }
                    break;

                case PetState.Drag:
                    break;

                case PetState.Eat:
                    if (stateTime >= 2.4f)
                    {
                        ForceMood(PetMood.Happy, 1.2f);
                        EnterState(PetState.Idle, -1f, false);
                    }
                    break;

                case PetState.EatRice:
                    {
                        if (!riceFreeze)
                        {
                            riceLevel = 1f - stateTime / 4.5f;
                            if (riceLevel < 0f) riceLevel = 0f;
                        }
                        chewCd -= dt;
                        if (chewCd <= 0f)
                        {
                            chewCd = 0.55f;
                            PlaySfx(riceLevel > 0.02f ? "eat" : "bubble");
                        }
                        if (stateTime >= 5.6f)
                        {
                            ForceMood(PetMood.Happy, 1.5f);
                            Say(Speech.OnRiceDone(), 4.5f);
                            EnterState(PetState.Idle, -1f, false);
                        }
                    }
                    break;

                case PetState.Sleepy:
                    if (stateTime >= 1.6f) EnterState(PetState.Sleep, -1f, false);
                    break;

                case PetState.Sleep:
                    if (stateTime > 4f && rnd.Next(100) < 2) PlaySfx("snore");
                    break;

                case PetState.Dizzy:
                case PetState.Cheer:
                case PetState.Surprised:
                    if (stateDuration > 0f && stateTime >= stateDuration)
                        EnterState(PetState.Idle, -1f, false);
                    break;

                case PetState.Focus:
                case PetState.Relax:
                    break;
            }

            if (cfg.FollowMouse && state != PetState.Drag && !dragging)
            {
                Native.POINT cur;
                if (Native.GetCursorPos(out cur))
                {
                    float targetX = cur.X - canvasW / 2f + 40f * (cur.X < X + canvasW / 2f ? 1f : -1f);
                    float targetY = cur.Y - FishBottomOffset() + fishH * 0.35f;
                    X += (targetX - X) * Math.Min(1f, 2.2f * dt);
                    Y += (targetY - Y) * Math.Min(1f, 2.2f * dt);
                    facingLeft = cur.X < X + canvasW / 2f;
                }
            }

            if (state == PetState.Idle && moodLockLeft <= 0f)
            {
                nextMicro -= dt;
                if (nextMicro <= 0f)
                {
                    nextMicro = 16f + (float)rnd.NextDouble() * 26f;
                    RandomMicroAction();
                }
            }

            uint idleMs = Native.GetIdleMilliseconds();
            if (state == PetState.Idle || state == PetState.Walk)
            {
                if (idleMs > 300000)
                {
                    SayThrottled("away", Speech.OnUserAway(), 4f, 900f);
                    EnterState(PetState.Sleepy, 1.6f, false);
                }
            }
            else if (state == PetState.Sleep && idleMs < 3000)
            {
                Wake();
            }

            if (state != PetState.Drag) ClampToScreen();
        }

        private void UpdateBlink(float dt)
        {
            if (state == PetState.Sleep) { blinkAnim = 1f; return; }
            if (state == PetState.Sleepy) { blinkAnim = 0.7f + 0.2f * (float)Math.Sin(breathT * 1.2); return; }

            if (blinkStage == 0)
            {
                blinkCountdown -= dt;
                if (blinkCountdown <= 0f)
                {
                    blinkStage = 1;
                    blinkAnim = 0f;
                }
            }
            else if (blinkStage == 1)
            {
                blinkAnim += dt / 0.09f;
                if (blinkAnim >= 1f) { blinkAnim = 1f; blinkStage = 2; }
            }
            else
            {
                blinkAnim -= dt / 0.11f;
                if (blinkAnim <= 0f)
                {
                    blinkAnim = 0f;
                    blinkStage = 0;
                    blinkCountdown = 2.2f + (float)rnd.NextDouble() * 4.5f;
                    if (rnd.Next(100) < 18) blinkCountdown = 0.25f;
                }
            }
        }

        private void RandomMicroAction()
        {
            int pick = rnd.Next(100);
            if (pick < 22)
            {
                PlaySfx("bubble");
                ForceMood(PetMood.Surprised, 0.6f);
            }
            else if (pick < 42)
            {
                EnterState(PetState.Cheer, 1.3f, true);
            }
            else if (pick < 56)
            {
                PlaySfx("hiccup");
                ForceMood(PetMood.Surprised, 0.5f);
                blinkStage = 1; blinkAnim = 0f;
            }
            else if (pick < 70)
            {
                EnterState(PetState.Dizzy, 1.1f, true);
            }
            else if (pick < 88)
            {
                SayThrottled("chatter", Speech.OnIdleChatter(), 5f, 200f);
            }
            else if (cfg.Satiety < 70f)
            {
                FeedRice(true);
            }
            else
            {
                EnterState(PetState.Walk, 4f + (float)rnd.NextDouble() * 4f, true);
                roamDir = rnd.Next(2) == 0 ? -1 : 1;
                facingLeft = roamDir < 0;
            }
        }

        private void SayThrottled(string key, string text, float seconds, float cooldown)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (sayCooldown.ContainsKey(key)) return;
            sayCooldown[key] = cooldown;
            Say(text, seconds);
        }

        // ==================== 姿态 ====================

        internal PetPose BuildPose()
        {
            PetPose p = PetPose.Default;
            float t = stateTime;
            float sc = Math.Max(0.5f, cfg.Scale);
            p.Time = (float)clock.Elapsed.TotalSeconds;
            p.FacingLeft = facingLeft;
            p.Accessory = cfg.Worn;
            p.Blink = blinkAnim;
            p.Squash = 1f + 0.035f * (float)Math.Sin(t * 2.3f);
            p.Bob = 2.2f * sc * (float)Math.Sin(t * 1.6f);
            p.Happy = 0.22f + cfg.Intimacy / 220f;
            p.Mood = PetMood.Idle;

            switch (state)
            {
                case PetState.Walk:
                    p.Mood = PetMood.Walk;
                    p.Tilt = (facingLeft ? -4f : 4f) + 2f * (float)Math.Sin(t * 7f);
                    p.Bob = 3.4f * sc * (float)Math.Sin(t * 5.2f);
                    p.Squash = 1f + 0.05f * (float)Math.Sin(t * 5.2f);
                    break;
                case PetState.Drag:
                    p.Mood = PetMood.Drag;
                    p.Squash = 1.09f;
                    p.MouthOpen = 0.55f;
                    p.Tilt = 7f * (float)Math.Sin(t * 9f);
                    break;
                case PetState.Sink:
                    p.Mood = PetMood.Surprised;
                    p.MouthOpen = 0.35f + 0.2f * (float)Math.Sin(t * 5f);
                    p.Tilt = 4f * (float)Math.Sin(t * 6f);
                    break;
                case PetState.Eat:
                    p.Mood = PetMood.Eat;
                    p.MouthOpen = 0.5f + 0.5f * (float)Math.Sin(t * 13f);
                    p.Squash = 1f + 0.07f * (float)Math.Sin(t * 13f);
                    p.Happy = 0.85f;
                    break;
                case PetState.EatRice:
                    p.Mood = PetMood.EatRice;
                    p.Rice = riceLevel;
                    p.MouthOpen = 0.45f + 0.45f * (float)Math.Sin(t * 11f);
                    p.Squash = 1f + 0.05f * (float)Math.Sin(t * 11f);
                    p.Bob = 1.2f * sc * (float)Math.Sin(t * 5.5f);
                    p.Tilt = 3.5f * (float)Math.Sin(t * 2.2f);
                    p.Happy = 0.95f;
                    break;
                case PetState.Sleepy:
                    p.Mood = PetMood.Sleepy;
                    p.Blink = 0.75f;
                    p.Bob = 1.2f * sc * (float)Math.Sin(t * 1.1f);
                    break;
                case PetState.Sleep:
                    p.Mood = PetMood.Sleep;
                    p.Blink = 1f;
                    p.Squash = 1f + 0.06f * (float)Math.Sin(t * 1.1f);
                    p.Bob = 1.5f * sc * (float)Math.Sin(t * 1.1f);
                    break;
                case PetState.Focus:
                    p.Mood = PetMood.Focus;
                    p.Bob = -fishH * 0.03f;
                    p.Happy = 0.35f;
                    break;
                case PetState.Relax:
                    p.Mood = PetMood.Relax;
                    p.Blink = 0.45f;
                    p.Happy = 0.8f;
                    p.Bob = 2.8f * sc * (float)Math.Sin(t * 1.9f);
                    break;
                case PetState.Dizzy:
                    p.Mood = PetMood.Dizzy;
                    p.Tilt = 18f * (float)Math.Sin(t * 13f);
                    break;
                case PetState.Cheer:
                    p.Mood = PetMood.Cheer;
                    p.MouthOpen = 0.8f;
                    p.Bob = -6f * sc * Math.Abs((float)Math.Sin(t * 6f));
                    p.Happy = 0.9f;
                    break;
                case PetState.Surprised:
                    p.Mood = PetMood.Surprised;
                    p.MouthOpen = 0.7f;
                    p.Squash = 1.06f;
                    break;
            }

            // 情绪锁定覆盖；吃饭、被拎起来这些动作优先，别被盖掉
            if (moodLockLeft > 0f && state != PetState.EatRice && state != PetState.Eat && state != PetState.Drag)
            {
                p.Mood = moodLockValue;
                if (moodLockValue == PetMood.Happy) { p.Happy = 1f; p.Blink = 0.3f; }
                else if (moodLockValue == PetMood.Sick) { p.MouthOpen = 0.35f; }
                else if (moodLockValue == PetMood.Love) { p.Happy = 1f; }
            }
            else if (cfg.Satiety < 18f && (state == PetState.Idle || state == PetState.Walk))
            {
                p.Mood = PetMood.Sad;
            }
            return p;
        }

        // ==================== 点击 / 养成动作 ====================

        internal void OnPetClicked()
        {
            if (state == PetState.Focus)
            {
                Say(Speech.OnFocusUrge(), 4f);
                PlaySfx("click");
                return;
            }
            if (IsAsleep()) { Wake(); return; }
            AddIntimacy(0.8f);
            Say(Speech.OnClick(), 3.5f);
            PlaySfx("headpat");
            ForceMood(PetMood.Happy, 1.2f);
        }

        public void Feed()
        {
            Wake();
            if (state == PetState.Eat) return;
            EnterState(PetState.Eat, 2.4f, false);
            PlaySfx("feed");
            cfg.Satiety = Settings.Clamp(cfg.Satiety + 22f, 0f, 100f);
            cfg.FedCount++;
            cfg.LastFeed = DateTime.Now.ToString("o");
            AddIntimacy(1.5f);
            Say(Speech.OnFeed(), 4f);
            if (cfg.FedCount % 5 == 0)
            {
                cfg.Coins += 1;
                Say("连续投喂奖励 1 颗鱼丸！现在有 " + cfg.Coins + " 颗", 4.5f);
                PlaySfx("coin");
            }
            SaveNow();
        }

        /// <summary>投喂白米饭（招牌动作：双手捧铁盆扒饭）</summary>
        public void FeedRice()
        {
            FeedRice(false);
        }

        public void FeedRice(bool selfServe)
        {
            Wake();
            if (state == PetState.EatRice) return;
            EnterState(PetState.EatRice, 5.6f, false);
            riceLevel = 1f;
            chewCd = 0f;
            cfg.Satiety = Settings.Clamp(cfg.Satiety + 30f, 0f, 100f);
            cfg.FedCount++;
            cfg.RiceCount++;
            cfg.LastFeed = DateTime.Now.ToString("o");
            AddIntimacy(2.2f);
            Say(selfServe ? Speech.OnRiceSelfServe() : Speech.OnRice(), 5f);
            PlaySfx("feed");
            if (!selfServe && cfg.RiceCount % 5 == 0)
            {
                cfg.Coins += 1;
                Say("连吃 " + cfg.RiceCount + " 盆白饭，奖励 1 颗鱼丸！现在有 " + cfg.Coins + " 颗", 5f);
                PlaySfx("coin");
            }
            selfServeCd = 240f;
            SaveNow();
        }

        public bool IsAsleep()
        {
            return state == PetState.Sleep || state == PetState.Sleepy;
        }

        public void PutToSleep()
        {
            EnterState(PetState.Sleepy, 1.6f, false);
            Say(Speech.OnSleep(), 4.5f);
            PlaySfx("sleep");
        }

        public void Wake()
        {
            if (!IsAsleep()) return;
            EnterState(PetState.Surprised, 0.9f, false);
            Say(Speech.OnWake(), 3.5f);
            PlaySfx("hiccup");
        }

        private string SleepMenuItemText()
        {
            return IsAsleep() ? "叫醒它" : "让它睡一会儿";
        }

        // ==================== 番茄钟回调 ====================

        public void StartPomodoro()
        {
            OpenPomodoro();
            if (pomodoro != null) pomodoro.StartFocus();
        }

        public void OnFocusStarted()
        {
            Wake();
            EnterState(PetState.Focus, -1f, false);
            Say(Speech.OnFocusStart(), 5f);
            PlaySfx("pomodoro_start");
            if (cfg.Ambient != "none") SetAmbient(cfg.Ambient);
            ForceMood(PetMood.Focus, 3f);
        }

        public void OnFocusEnded()
        {
            EnterState(PetState.Cheer, 2f, false);
            Say(Speech.OnFocusEnd(), 5f);
            PlaySfx("pomodoro_end");
            cfg.PomodoroCount++;
            AddIntimacy(2.5f);
            RewardCoins(1, "完成一个番茄");
            Notify("番茄钟完成", "干得漂亮！休息一下吧～");
            try { Audio.StopAmbient(); } catch { }
            SaveNow();
        }

        public void OnFocusInterrupted()
        {
            EnterState(PetState.Idle, -1f, false);
            Say("没关系，下次再一起专注～", 4.5f);
            try { Audio.StopAmbient(); } catch { }
        }

        public void OnBreakStarted()
        {
            EnterState(PetState.Relax, -1f, false);
            Say(Speech.OnBreakStart(), 5f);
            PlaySfx("notify");
            Notify("休息时间", "起来走两步，看看远方吧～");
        }

        public void OnBreakEnded()
        {
            EnterState(PetState.Idle, -1f, false);
            Say(Speech.OnBreakEnd(), 4.5f);
            PlaySfx("notify");
        }

        // ==================== 系统联动 ====================

        internal void OnStartupChecks()
        {
            try
            {
                Audio.Init(Settings.DataDir);
                Audio.Volume = cfg.Volume / 100f;
                Audio.Muted = cfg.Muted;
            }
            catch { }

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (cfg.LastSeenDate != today)
            {
                if (cfg.LastSeenDate.Length > 0)
                    Notify("大肥鱼", "新的一天！昨天的鱼报已归档～");
                cfg.TodaySlackMinutes = 0;
                cfg.LastSeenDate = today;
            }

            if (cfg.Satiety < 25f)
            {
                Say(Speech.OnHungry(), 6f);
            }
            else if (Monitor.IsAsusTufMachine())
            {
                Say("检测到天选整机，本鱼就是天选之鱼！", 6f);
            }

            try
            {
                System.Diagnostics.Process[] ps = System.Diagnostics.Process.GetProcessesByName("TX Mascot");
                if (ps != null && ps.Length > 0)
                {
                    for (int i = 0; i < ps.Length; i++) ps[i].Dispose();
                    Say("咦，天选姬也在？那我们一起陪着你～", 6f);
                }
            }
            catch { }
        }

        internal void ReactToSystem()
        {
            if (shotMode) return;
            SysSnapshot s = Monitor.Latest;
            if (s == null) return;

            if (s.HasBattery && s.BatteryPercent >= 0)
            {
                if (!s.AcOnline && s.BatteryPercent <= 20)
                {
                    batteryNotifyCd -= 1f;
                    if (batteryNotifyCd <= 0f)
                    {
                        batteryNotifyCd = 300f;
                        Say(Speech.OnLowBattery(s.BatteryPercent), 6f);
                        PlaySfx("lowbattery");
                        if (cfg.NotifyBattery) Notify("电量不足", "只剩 " + s.BatteryPercent + "%，快插电吧");
                        ForceMood(PetMood.Sleepy, 6f);
                    }
                }
                else if (s.AcOnline && s.Charging && !lastCharging)
                {
                    if (cfg.NotifyBattery)
                    {
                        Say(Speech.OnCharging(s.BatteryPercent), 5f);
                        PlaySfx("charge");
                    }
                }
                else if (s.AcOnline && s.BatteryPercent >= 99 && lastBatteryPercent < 99)
                {
                    Say(Speech.OnFullBattery(), 5f);
                    PlaySfx("notify");
                    if (cfg.NotifyBattery) Notify("电量充满", "满电啦，可以拔线自由游动了");
                }
                lastCharging = s.Charging;
                lastBatteryPercent = s.BatteryPercent;
            }

            bool hot = (s.CpuTempC >= 88f) || (s.GpuTempC >= 88f);
            bool heavy = s.CpuLoad >= 95f;
            if (hot || heavy)
            {
                ForceMood(hot ? PetMood.Sick : PetMood.Dizzy, 20f);
                hotNotifyCd -= 1f;
                if (hotNotifyCd <= 0f)
                {
                    hotNotifyCd = 600f;
                    Say(hot ? Speech.OnHot() : Speech.OnHighLoad(), 6f);
                    PlaySfx(hot ? "error" : "surprised");
                    if (cfg.NotifyHotCpu && hot)
                        Notify("大肥鱼提醒", "CPU/GPU 温度偏高（" + ((int)Math.Round(s.CpuTempC)) + "°C），注意散热");
                }
            }

            if (s.MemPercent >= 93f) SayThrottled("mem", Speech.OnMemWarn(), 5f, 900f);
            else if (s.NetDownKBps >= 10240f) SayThrottled("net", Speech.OnNetBusy(), 4f, 900f);
        }

        internal void AccumulateStats(float seconds)
        {
            if (shotMode) return;

            bool visibleAndHere = Visible && Native.GetIdleMilliseconds() < 60000;
            bool fullscreen = Native.IsForegroundFullscreen();
            if (visibleAndHere && !fullscreen)
            {
                cfg.TodaySlackMinutes += seconds / 60.0;
                cfg.TotalSlackMinutes += seconds / 60.0;
            }

            float rate = IsAsleep() ? 0.0012f : 0.0025f;
            cfg.Satiety = Settings.Clamp(cfg.Satiety - rate * seconds, 0f, 100f);

            if (visibleAndHere) AddIntimacySilent(0.0004f * seconds);

            coinAcc += seconds;
            if (coinAcc >= 1200f)
            {
                coinAcc = 0f;
                RewardCoins(1, "陪伴 20 分钟");
                PlaySfx("coin");
            }

            // 饿了：先喊两句，饿狠了就自己掏铁盆
            selfServeCd -= seconds;
            if (cfg.Satiety < 30f && selfServeCd <= 0f && state == PetState.Idle && !IsAsleep() && bubbleLeft <= 0f)
            {
                FeedRice(true);
            }
            if (cfg.Satiety < 25f)
            {
                hungryCd -= seconds;
                if (hungryCd <= 0f)
                {
                    hungryCd = 420f;
                    if (!IsAsleep()) Say(Speech.OnHungry(), 5f);
                }
            }
        }

        private void AddIntimacySilent(float v)
        {
            float before = cfg.Intimacy;
            cfg.Intimacy = Settings.Clamp(cfg.Intimacy + v, 0f, 100f);
            if ((int)(before / 20f) < (int)(cfg.Intimacy / 20f))
            {
                PlaySfx("levelup");
                Notify("大肥鱼", "亲密度提升到 " + ((int)cfg.Intimacy) + "% 啦！");
            }
        }

        internal void TickAlarm()
        {
            if (!cfg.AlarmEnabled) return;
            if (string.IsNullOrEmpty(cfg.AlarmTime)) return;
            string now = DateTime.Now.ToString("HH:mm");
            if (now == cfg.AlarmTime && lastAlarmMinute != now)
            {
                lastAlarmMinute = now;
                Wake();
                EnterState(PetState.Surprised, 2.5f, false);
                Say(Speech.OnAlarm(), 6f);
                PlaySfx("alarm");
                Notify("大肥鱼闹钟", "到点啦！" + (cfg.AlarmDaily ? "" : "（单次闹钟已关闭）"));
                if (!cfg.AlarmDaily)
                {
                    cfg.AlarmEnabled = false;
                    if (alarmForm != null && !alarmForm.IsDisposed) alarmForm.SyncFromConfig();
                }
            }
        }

        internal void UpdateHiddenState()
        {
            if (cfg.HideFullscreen)
            {
                bool fs = Native.IsForegroundFullscreen();
                if (fs && !fullscreenHidden)
                {
                    fullscreenHidden = true;
                    Hide();
                }
                else if (!fs && fullscreenHidden)
                {
                    fullscreenHidden = false;
                    if (!manualHidden)
                    {
                        Show();
                        BringToFrontSafe();
                    }
                }
            }
            else if (fullscreenHidden)
            {
                fullscreenHidden = false;
                if (!manualHidden) Show();
            }
        }
    }
}
