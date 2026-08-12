using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Neutral;
using UnityEngine;
using static Il2CppSystem.Threading.SemaphoreSlim;
using static UnityEngine.ProBuilder.AutoUnwrapSettings;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Polaris : RoleBase, IImpostor, IUsePhantomButton
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Polaris),
                player => new Polaris(player),
                CustomRoles.Polaris,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                10000,
                SetUpOptionItem,
                "Pol",
                "#ff1919",
                OptionSort: (7, 0),
                introSound: () => GetIntroSound(RoleTypes.Shapeshifter)
            );
        public Polaris(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            KillCooldown = OptionKillCooldown.GetFloat();
            shine = OptionFirstshine.GetInt();
            DecreasingTime = OptionDecreasingTime.GetFloat();
            Reducedperkill = OptionReducedperkill.GetInt();

            DecreasingTimer = null;

            phantomCooldownTimer = 0f;
            prevPhantomCooldownTimer = 0f;
            bombTriggered = false;
            Burnouted = false;
            //tomosibishine = OptionTomosibishine.GetInt();
        }

        private static OptionItem OptionKillCooldown;
        private static float KillCooldown;
        public static OptionItem OptionFirstshine;
        public static OptionItem OptionDecreasingTime;
        private static float DecreasingTime;
        public int shine;
        public float? DecreasingTimer;
        public static OptionItem OptionReducedperkill;
        public int Reducedperkill;
        public static OptionItem OptionBombCooldown;
        public static OptionItem OptionExplosionRadius;
        //public static OptionItem OptionCanUseTomosibi;
        //public static OptionItem OptionTomosibiKillcool;
        //public static OptionItem OptionTomosibishine;
        //public int tomosibishine;
        public bool Burnouted;

        enum OptionName
        {
            PolarisFirstshine,
            PolarisDecreasingTime,
            PolarisReducedperkill,
            PolarisBombCooldown,
            PolarisExplosionRadius,
           // PolarisCanUseTomosibi,
           // PolarisRewindKillCool,
           // PolarisRestoredRadiance,
        }


        public bool CanBeLastImpostor { get; } = false;

        // ホスト側で減らす想定。クライアント表示でも同様に使えます。
        private float phantomCooldownTimer;         // 残り秒数（>=0）
        private float prevPhantomCooldownTimer;     // 前フレームの残り秒数（エッジ検出用）
        private bool bombTriggered;                 // 爆発を一度だけにするフラグ

        /// <summary>
        /// アビリティの表示用クールダウンを設定する（副作用はない）。
        /// </summary>
        public void SetPhantomCooldown(float seconds)
        {
            phantomCooldownTimer = Mathf.Max(0f, seconds);
            prevPhantomCooldownTimer = phantomCooldownTimer;

            // ホストなら表示更新を通知（クライアントHUD反映）
            if (AmongUsClient.Instance.AmHost) UtilsNotifyRoles.NotifyRoles();
        }

        /// <summary>
        /// ファントムクールダウンが 0 に到達した瞬間に呼ばれる（表示更新のみ）。
        /// 実行直後に爆発条件を満たすなら爆発を行う。
        /// </summary>
        private void OnPhantomCooldownFinished()
        {
            UtilsNotifyRoles.NotifyRoles();

            // タイマー終了時に shine 条件を満たしていれば爆発を実行（ホストで呼ばれる想定）
            if (AmongUsClient.Instance.AmHost && !bombTriggered && shine <= 1)
            {
                // 爆発は所有者が生存している場合のみ行う（仕様どおり）
                if (Player.IsAlive())
                {
                    ExecuteExplosion();
                }
            }
        }

        /// <summary>
        /// ホストでのみ実行される爆発処理。ダメージは周囲プレイヤーに与え、
        /// 所有者が生存していれば自殺処理も行う。実行は一度だけ。
        /// </summary>
        private void ExecuteExplosion()
        {
            // 再入防止。既に実行済み/実行中なら何もしない。
            if (bombTriggered) return;
            bombTriggered = true; // 最初に立てる（競合で二重実行されるのを防ぐ）
            // 追加の安全処置：ファントムタイマーを無効化して OnFixedUpdate 側の再発を防ぐ
            phantomCooldownTimer = float.MaxValue;
            prevPhantomCooldownTimer = float.MaxValue;

            try
            {
                var explosionRadius = OptionExplosionRadius.GetFloat();
                var targets = PlayerCatch.AllAlivePlayerControls.ToArray();

                // 対象へダメージ（ホスト側でのみ呼ばれている前提）
                foreach (var target in targets)
                {
                    if (target.PlayerId == Player.PlayerId) continue;
                    if (!Ballooner.IsInExplosionRange(Player, target, explosionRadius)) continue;

                    // ホスト側で対象プレイヤーに対して殺害判定を行う
                    CustomRoleManager.OnCheckMurder(Player, target, target, target, true, false, 2, CustomDeathReason.Bombed);
                }

                // 所有者が生存しているなら自殺（ホストで実行）
                if (Player.IsAlive())
                {
                    // Burnouted を先に立てる（再入抑止）
                    if (!Burnouted)
                    {
                        Burnouted = true;
                        MyState.DeathReason = CustomDeathReason.Burnout;
                        Player.SetRealKiller(Player);
                        // 一度だけ RPC を投げる
                        Player.RpcMurderPlayer(Player);
                    }
                }

                // 勝利判定（全滅など）
                if (!PlayerCatch.AllAlivePlayerControls.Any())
                {
                    CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Impostor, byte.MaxValue);
                }

                // 状態同期と表示更新（1回だけ）
                SendRPC();
                UtilsNotifyRoles.NotifyRoles();
            }
            catch (System.Exception ex)
            {
                // 例外が出ても再入不可のままにしておく（安全優先）。
                Logger.Error($"ExecuteExplosion 例外: {ex}", "Polaris.ExecuteExplosion");
            }
        }

        private static void SetUpOptionItem()
        {
            OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 60f, 2.5f), 22.5f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionFirstshine = IntegerOptionItem.Create(RoleInfo, 11, OptionName.PolarisFirstshine, new(2, 99, 1), 10, false);
            OptionDecreasingTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.PolarisDecreasingTime, new(1f, 50f, 0.5f), 10f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionReducedperkill = IntegerOptionItem.Create(RoleInfo, 14, OptionName.PolarisReducedperkill, new(0, 98, 1), 1, false);
            OptionBombCooldown = FloatOptionItem.Create(RoleInfo, 15, OptionName.PolarisBombCooldown, new(2.5f, 60f, 2.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionExplosionRadius = FloatOptionItem.Create(RoleInfo, 16, OptionName.PolarisExplosionRadius, new(0.5f, 10f, 0.5f), 3f, false)
                .SetValueFormat(OptionFormat.Multiplier);
            /*OptionCanUseTomosibi = BooleanOptionItem.Create(RoleInfo, 17, OptionName.PolarisCanUseTomosibi, true, false);
            OptionTomosibiKillcool = FloatOptionItem.Create(RoleInfo, 18, OptionName.PolarisRewindKillCool, new(1f, 60f, 0.5f), 5f, false, OptionCanUseTomosibi)
                .SetValueFormat(OptionFormat.Seconds);
            OptionTomosibishine = IntegerOptionItem.Create(RoleInfo, 19, OptionName.PolarisRestoredRadiance, new(1, 99, 1), 1, false, OptionCanUseTomosibi);*/
        }

        public override void Add()
        {
            base.Add();
            //PetActionManager.Register(Player.PlayerId, OnPetUsed);
        }
        /*public override void OnDestroy()
        {
            PetActionManager.Unregister(Player.PlayerId);
        }
        private void OnPetUsed()
        {
            Tomosibi();
            SendRPC();
        }*/

        public override void OnSpawn(bool initialState = false)
        {
            base.OnSpawn(initialState);

            // ゲーム開始時点でタイマーを起動（ホストのみ）
            if (AmongUsClient.Instance.AmHost)
            {
                bombTriggered = false;
            }
        }

        public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
        {
            DecreasingTimer = null;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (Player.IsAlive())
            {
                Burnouted = false;
            }
            if (AmongUsClient.Instance.AmHost && !ExileController.Instance)
            {
                SendRPC();
                if (DecreasingTimer == null) //タイマーがない
                {
                    SetPhantomCooldown(OptionBombCooldown.GetFloat());
                    DecreasingTimer = 0f;
                }
                else if (DecreasingTimer >= DecreasingTime)
                {
                    shine = --shine;
                    DecreasingTimer = 0f;
                    SendRPC();
                }
                else
                {
                    DecreasingTimer += Time.fixedDeltaTime;//時間をカウント
                }

                // --- ファントムクールダウンのカウントダウンとエッジ検出（ホストでのみ） ---
                prevPhantomCooldownTimer = phantomCooldownTimer;
                if (phantomCooldownTimer > 0f)
                {
                    phantomCooldownTimer -= Time.fixedDeltaTime;
                    if (phantomCooldownTimer < 0f) phantomCooldownTimer = 0f;
                }

                // エッジ検出：prev > 0 && now == 0
                if (prevPhantomCooldownTimer > 0f && phantomCooldownTimer <= 0f)
                {
                    OnPhantomCooldownFinished();
                }

                // 追加の保険：タイマーが既に0の状態でも条件を満たせば確実に発動する
                if (phantomCooldownTimer <= 0f && !bombTriggered && shine <= 1 && Player.IsAlive())
                {
                    ExecuteExplosion();
                }
            }
            if (shine <= 0)
            {
                if (!Burnouted)
                {
                    Burnouted = true;
                    MyState.DeathReason = CustomDeathReason.Burnout;
                    Player.SetRealKiller(Player);
                    Player.RpcMurderPlayer(Player);
                }
            }
        }
       /* private void Tomosibi()
        {
            // 生存チェック
            if (!Player.IsAlive()) return;

            // オプションから巻き戻し秒数と回復shineを取得
            float rewindSeconds = OptionTomosibiKillcool?.GetFloat() ?? 0f;
            int restoreShine = tomosibishine;

            if (rewindSeconds <= 0f && restoreShine <= 0) return;

            // 辞書登録（未登録なら Init）
            (this as IUsePhantomButton)?.Init(Player);
            IUsePhantomButton.IPPlayerKillCooldown.TryGetValue(Player.PlayerId, out var elapsed);

            // 現在の total（最初の値を固定して使う）
            float originalTotal = Main.AllPlayerKillCooldown.TryGetValue(Player.PlayerId, out var tot) ? tot : KillCooldown;
            // 現在の残り（表示上）
            float currentRemaining = Mathf.Max(0f, originalTotal - elapsed);

            // 新しい残り時間 = 現在の残り + 巻き戻し秒数（期待どおり1回ごとに +rewind）
            float newRemaining = currentRemaining + rewindSeconds;

            // 新しい経過（elapsed を減らすことで残りが増える）
            float newElapsed = Mathf.Max(0f, elapsed - rewindSeconds);

            // newTotal は originalTotal を基準に固定計算（再適用しても変わらない）
            float newTotal = originalTotal + rewindSeconds;

            // 表示用経過を更新（クライアント表示に使われる）
            IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = newElapsed;

            // ホストなら権威ある total を上書き（ただし必ず originalTotal を基準に newTotal を設定）
            if (AmongUsClient.Instance.AmHost)
            {
                Main.AllPlayerKillCooldown[Player.PlayerId] = newTotal;

                // 同期と上書き対策（複数回送る）
                try
                {
                    Player.MarkDirtySettings();
                    Player.SyncSettings();
                }
                catch { }

                _ = new LateTask(() =>
                {
                    if (!Player.IsAlive()) return;
                    Main.AllPlayerKillCooldown[Player.PlayerId] = newTotal;
                    try { Player.MarkDirtySettings(); Player.SyncSettings(); } catch { }
                }, 0.12f, "Polaris.Tomosibi.Sync1", true);

                _ = new LateTask(() =>
                {
                    if (!Player.IsAlive()) return;
                    Main.AllPlayerKillCooldown[Player.PlayerId] = newTotal;
                    try { Player.MarkDirtySettings(); Player.SyncSettings(); } catch { }
                }, 0.45f, "Polaris.Tomosibi.Sync2", true);
            }

            // HUD 表示を即時更新（自分のクライアント）
            float displayRemaining = Mathf.Max(0.005f, newRemaining);
            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == Player.PlayerId)
            {
                try { Player.SetKillTimer(displayRemaining); } catch { }
            }

            // 追加の保険として経過値を短時間再適用（表示安定化）
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = newElapsed;
                if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == Player.PlayerId)
                {
                    try { Player.SetKillTimer(Mathf.Max(0.005f, Main.AllPlayerKillCooldown.TryGetValue(Player.PlayerId, out var t2) ? (t2 - newElapsed) : displayRemaining)); } catch { }
                }
            }, 0.25f, "Polaris.Tomosibi.Reapply", true);

            // shine の回復（固定回復）
            if (restoreShine > 0)
            {
                shine += restoreShine;
            }

            // 同期と表示更新
            SendRPC();
            try { Player.MarkDirtySettings(); } catch { }
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }*/

        private void SendRPC()
        {
            using var sender = CreateSender();
            sender.Writer.Write(shine);
        }

        public override void ReceiveRPC(MessageReader reader)
        {
            shine = reader.ReadInt32();

            UtilsNotifyRoles.NotifyRoles();
        }

        public override string GetProgressText(bool comms = false, bool GameLog = false)
        {
            var color = RoleInfo?.RoleColorCode ?? "#ffffff";
            return $"<{color}>({shine})</color>";
        }

        public float CalculateKillCooldown() => KillCooldown;

        bool IUsePhantomButton.IsresetAfterKill => false;

        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = OptionBombCooldown.GetFloat();
        }

        // ファントムボタンは何もしない（クールダウン表示は最初から始まっている）
        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = false;
            ResetCooldown = false;
            // 何もしない（仕様どおり）
        }

        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            shine = shine - Reducedperkill;
            SendRPC();
        }

        public override void AfterMeetingTasks()
        {
            // ミーティング後は表示用クールの状態をクリア
            phantomCooldownTimer = 0f;
            prevPhantomCooldownTimer = 0f;
            bombTriggered = false;
            if (Player.IsAlive())
            {
                Burnouted = false;
            }
        }
        public override bool OverrideAbilityButton(out string text)
        {
            text = "jibaku_Ability";
            return true;
        }
    }
}