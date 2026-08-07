using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost;

// ダミーハンター ゲームモード
// タスクバトル(TaskBattle.cs)のゲームモード構成を参考に、
// ・[GameModuleInitializer] Init() で毎試合の状態リセット
// ・オプションは SetupOptionItem() で一括作成(OptionHolder.Load から呼ばれる)
// ・勝敗判定は GameEndPredicate 内部クラス(TaskBattleGameEndPredicate と同じ流儀)
// という形に整理して作り直したもの。
//
// 全プレイヤーは役職 DummyHunterPlayer(ベース RoleTypes.Phantom)になり、
// ファントムボタンのワンクリックで一番近いダミーへワープしてキルする。
// 制限時間内に一番ダミーをキルしたプレイヤーが勝利。
public static class DummyHunter
{
    #region State
    public static bool IsActive = false;
    public static float TimeLeft = 0f;
    public static float ElapsedTime = 0f;

    /// <summary>key: プレイヤーID, value: キル数</summary>
    public static Dictionary<byte, int> KillCounts = new();

    /// <summary>現在マップ上に存在するダミー</summary>
    public static List<HunterDummy> ActiveDummies = new();

    // ホスト用: スポーン中(生成キュー投入済みでまだ ActiveDummies に入っていない)の数。
    // CreateNetObject が非同期のため、これを数えないと OnFixedUpdate が過剰スポーンさせてしまう。
    private static int _pendingSpawns = 0;
    private static float _spawnCooldown = 0f;
    private static float _scoreSyncTimer = 0f;

    // ローカルの矢印表示位置(重複登録を避けるため保持)
    private static Vector3? _arrowPos = null;

    public static bool IsThisMode => Options.CurrentGameMode == CustomGameMode.DummyHunter;

    [Attributes.GameModuleInitializer]
    public static void Init()
    {
        IsActive = false;
        TimeLeft = 0f;
        ElapsedTime = 0f;
        _pendingSpawns = 0;
        _spawnCooldown = 0f;
        _scoreSyncTimer = 0f;
        _arrowPos = null;
        KillCounts = new();
        ActiveDummies = new();
    }
    #endregion

    #region Options
    public static OptionItem OptionTimeLimit;
    public static OptionItem OptionPhantomCooldown;
    public static OptionItem OptionMaxDummyCount;
    public static OptionItem OptionShowArrow;
    public static OptionItem OptionArrowDelay;
    public static OptionItem OptionShowTopPlayer;

    public static void SetupOptionItem()
    {
        ObjectOptionitem.Create(1_000_210, "DummyHunter", true, null, TabGroup.MainSettings)
            .SetOptionName(() => GetString("DummyHunter"))
            .SetColorcode("#e0b0ff")
            .SetTag(CustomOptionTags.DummyHunter);

        OptionTimeLimit = FloatOptionItem.Create(210000, "DummyHunterTimeLimit", new(30f, 600f, 10f), 120f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetColorcode("#e0b0ff")
            .SetHeader(true);

        OptionPhantomCooldown = FloatOptionItem.Create(210001, "DummyHunterPhantomCooldown", new(0f, 60f, 0.5f), 3f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetColorcode("#e0b0ff");

        OptionMaxDummyCount = IntegerOptionItem.Create(210002, "DummyHunterMaxDummyCount", new(1, 50, 1), 8, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Pieces)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetColorcode("#e0b0ff");

        OptionShowArrow = BooleanOptionItem.Create(210003, "DummyHunterShowArrow", true, TabGroup.MainSettings, false)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetColorcode("#e0b0ff");

        OptionArrowDelay = FloatOptionItem.Create(210004, "DummyHunterArrowDelay", new(0f, 300f, 5f), 30f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetParent(OptionShowArrow);

        OptionShowTopPlayer = BooleanOptionItem.Create(210005, "DummyHunterShowTopPlayer", true, TabGroup.MainSettings, false)
            .SetTag(CustomOptionTags.DummyHunter)
            .SetColorcode("#e0b0ff");
    }
    #endregion

    #region GameFlow
    public static void OnGameStart()
    {
        IsActive = true;
        TimeLeft = OptionTimeLimit != null ? OptionTimeLimit.GetFloat() : 120f;
        ElapsedTime = 0f;
        _pendingSpawns = 0;
        _spawnCooldown = 0f;
        _scoreSyncTimer = 0f;
        _arrowPos = null;
        ActiveDummies.Clear();

        KillCounts.Clear();
        foreach (var pc in PlayerCatch.AllPlayerControls)
            KillCounts[pc.PlayerId] = 0;

        if (!AmongUsClient.Instance.AmHost) return;

        // イントロ演出が終わってから順次スポーン(いきなり大量生成すると崩れやすいため間隔を空ける)
        SpawnInitialDummies(baseDelay: 3f);
    }

    // 会議明けにダミーを配置し直す
    public static void AfterMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsActive) return;

        ActiveDummies.Clear();
        _pendingSpawns = 0;
        SpawnInitialDummies(baseDelay: 0.5f);
    }

    private static void SpawnInitialDummies(float baseDelay)
    {
        int max = GetMaxDummyCount();
        for (int i = 0; i < max; i++)
        {
            int index = i;
            _pendingSpawns++;
            _ = new LateTask(() =>
            {
                if (!IsActive || !AmongUsClient.Instance.AmHost)
                {
                    if (_pendingSpawns > 0) _pendingSpawns--;
                    return;
                }
                SpawnDummy(fromQueue: true);
            }, baseDelay + index * 0.15f, $"DummyHunter.Spawn{index}", true);
        }
    }

    // 会議開始時: 全ダミーを消す
    public static void OnMeeting()
    {
        foreach (var dummy in ActiveDummies.ToArray())
            dummy?.Despawn();
        ActiveDummies.Clear();
        _pendingSpawns = 0;
        RemoveLocalArrow();
    }

    public static void OnGameEnd()
    {
        IsActive = false;
        foreach (var dummy in ActiveDummies.ToArray())
            dummy?.Despawn();
        ActiveDummies.Clear();
        KillCounts.Clear();
        _pendingSpawns = 0;
        RemoveLocalArrow();
    }
    #endregion

    #region FixedUpdate
    public static void OnFixedUpdate()
    {
        if (!IsThisMode || !IsActive) return;
        if (!GameStates.InGame || GameStates.IsMeeting) return;

        float dt = Time.fixedDeltaTime;
        ElapsedTime += dt;
        TimeLeft -= dt;

        // 表示更新(全クライアント)
        UpdateArrow();
        UpdateUI();

        if (!AmongUsClient.Instance.AmHost) return;

        // スコアの定期同期
        _scoreSyncTimer += dt;
        if (_scoreSyncTimer >= 3f)
        {
            _scoreSyncTimer = 0f;
            foreach (var kv in KillCounts)
                RpcSyncScore(kv.Key, kv.Value);
        }

        // 不足分を少しずつ補充(過剰生成を防ぐため間隔を空け、生成中の数も勘定する)
        _spawnCooldown -= dt;
        int total = ActiveDummies.Count + _pendingSpawns;
        if (total < GetMaxDummyCount() && _spawnCooldown <= 0f)
        {
            _spawnCooldown = 0.5f;
            SpawnDummy(fromQueue: false);
        }

        // 制限時間終了
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            // 勝敗確定は GameEndPredicate が拾う。ここでは何もしない。
        }
    }
    #endregion

    #region Dummy management
    public static void SpawnDummy(bool fromQueue)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            if (fromQueue && _pendingSpawns > 0) _pendingSpawns--;
            return;
        }

        // 生成中のカウントを戻す(この呼び出しで1体作るため)
        if (fromQueue && _pendingSpawns > 0) _pendingSpawns--;

        if (ActiveDummies.Count + _pendingSpawns >= GetMaxDummyCount()) return;

        var dummy = new HunterDummy(GetRandomMapPosition());
        ActiveDummies.Add(dummy);
    }

    public static void OnDummyKilled(PlayerControl killer, HunterDummy dummy)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (killer != null)
        {
            if (!KillCounts.ContainsKey(killer.PlayerId)) KillCounts[killer.PlayerId] = 0;
            KillCounts[killer.PlayerId]++;
            RpcSyncScore(killer.PlayerId, KillCounts[killer.PlayerId]);
        }

        ActiveDummies.Remove(dummy);
        dummy.Despawn();

        // 少し遅らせて再スポーン(OnFixedUpdate の補充と二重にならないよう pending でカウント)
        _pendingSpawns++;
        _ = new LateTask(() =>
        {
            if (!IsActive || !AmongUsClient.Instance.AmHost)
            {
                if (_pendingSpawns > 0) _pendingSpawns--;
                return;
            }
            SpawnDummy(fromQueue: true);
        }, 0.3f, "DummyHunter.Respawn", true);

        Utils.AllPlayerKillFlash();

        _ = new LateTask(() =>
        {
            if (!IsActive) return;
            try { UtilsNotifyRoles.NotifyRoles(NoCache: true); } catch { }
        }, 0.3f, "DummyHunter.NotifyScore", true);
    }

    // ファントムワンクリック時(ホストのみ)。一番近いダミーへワープしてキル。
    public static void OnPhantomClick(PlayerControl killer)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (killer == null || !killer.IsAlive()) return;
        if (!IsActive || GameStates.IsMeeting) return;

        var pos = killer.GetTruePosition();
        var target = ActiveDummies
            .Where(d => d?.PlayerControl != null)
            .OrderBy(d => Vector2.Distance(pos, d.Position))
            .FirstOrDefault();

        if (target != null)
        {
            killer.RpcSnapToForced(target.Position);
            target.OnKilled(killer);
        }

        // ファントムクールを設定値に合わせて戻す
        _ = new LateTask(() =>
        {
            if (killer == null || !killer.IsAlive()) return;
            AURoleOptions.PhantomCooldown = OptionPhantomCooldown != null ? OptionPhantomCooldown.GetFloat() : 3f;
            killer.RpcResetAbilityCooldown();
        }, 0.1f, "DummyHunter.PhantomCD", true);
    }

    private static int GetMaxDummyCount()
        => OptionMaxDummyCount != null ? OptionMaxDummyCount.GetInt() : 8;

    public static Vector2 GetRandomMapPosition()
    {
        var rng = IRandom.Instance;
        int mapId = Main.NormalOptions?.MapId ?? 0;
        return mapId switch
        {
            0 => new Vector2(rng.Next(-25, 20), rng.Next(-10, 5)),
            1 => new Vector2(rng.Next(-5, 20), rng.Next(-5, 15)),
            2 => new Vector2(rng.Next(-20, 25), rng.Next(-25, 5)),
            3 => new Vector2(rng.Next(-20, 30), rng.Next(-15, 15)),
            4 => new Vector2(rng.Next(-20, 20), rng.Next(-15, 10)),
            _ => new Vector2(rng.Next(-20, 20), rng.Next(-10, 10)),
        };
    }
    #endregion

    #region Score & Marks
    public static void RpcSyncScore(byte playerId, int score)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        KillCounts[playerId] = score;

        if (PlayerControl.LocalPlayer == null) return;
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncDummyHunterScore, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(score);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveSyncScore(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        int score = reader.ReadInt32();
        KillCounts[playerId] = score;
    }

    // 名前の右に付けるキル数マーク(+トップには王冠)
    public static string GetScoreMark(byte playerId)
    {
        if (!IsThisMode) return "";
        int score = KillCounts.TryGetValue(playerId, out var s) ? s : 0;
        string mark = $"<color=#e0b0ff>[{score}]</color>";

        if (OptionShowTopPlayer != null && OptionShowTopPlayer.GetBool())
        {
            byte topId = byte.MaxValue; int top = -1;
            foreach (var kv in KillCounts)
                if (kv.Value > top) { top = kv.Value; topId = kv.Key; }
            if (top > 0 && playerId == topId)
                mark += "<color=#ffd700>♛</color>";
        }
        return mark;
    }
    #endregion

    #region UI & Arrow
    private static void UpdateUI()
    {
        if (HudManager.Instance == null || PlayerControl.LocalPlayer == null) return;

        var lower = HudManagerPatch.LowerInfoText;
        if (lower == null) return;

        int myScore = KillCounts.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var s) ? s : 0;

        string text = $"<size=140%><color=#e0b0ff>【{GetString("DummyHunter")}】</color></size>\n";
        text += $"<color=#ff5555>{GetString("DummyHunterTimeLeftText")}: {Mathf.CeilToInt(Mathf.Max(0f, TimeLeft))}s</color>  ";
        text += $"<color=#55ff55>{GetString("DummyHunterMyKillText")}: {myScore}</color>";

        if (OptionShowTopPlayer != null && OptionShowTopPlayer.GetBool())
        {
            byte topId = byte.MaxValue; int top = -1;
            foreach (var kv in KillCounts)
                if (kv.Value > top) { top = kv.Value; topId = kv.Key; }
            var topPc = PlayerCatch.GetPlayerById(topId);
            if (topPc != null && top > 0)
                text += $"\n<color=#ffd700>{GetString("DummyHunterTopText")}: {topPc.GetRealName()} ({top})</color>";
        }

        lower.enabled = true;
        lower.text = text;
    }

    // DummyHunterPlayer.GetMark から呼ばれる。矢印用の座標を返す。
    public static Vector3? GetClosestDummyPosition(PlayerControl seer)
    {
        if (seer == null) return null;
        if (seer.PlayerId == (PlayerControl.LocalPlayer?.PlayerId ?? byte.MaxValue))
            return _arrowPos;
        return null;
    }

    private static void RemoveLocalArrow()
    {
        var me = PlayerControl.LocalPlayer;
        if (me != null && _arrowPos.HasValue)
            GetArrow.Remove(me.PlayerId, _arrowPos.Value);
        _arrowPos = null;
    }

    private static void UpdateArrow()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null) return;

        bool showArrow = OptionShowArrow != null && OptionShowArrow.GetBool();
        float delay = OptionArrowDelay != null ? OptionArrowDelay.GetFloat() : 0f;

        if (!showArrow || ElapsedTime < delay || ActiveDummies.Count == 0)
        {
            RemoveLocalArrow();
            return;
        }

        var myPos = me.GetTruePosition();
        var closest = ActiveDummies
            .Where(d => d?.PlayerControl != null)
            .OrderBy(d => Vector2.Distance(myPos, d.Position))
            .FirstOrDefault();
        if (closest == null)
        {
            RemoveLocalArrow();
            return;
        }

        Vector3 newPos = closest.Position;
        if (!_arrowPos.HasValue || Vector2.Distance((Vector2)_arrowPos.Value, closest.Position) > 0.01f)
        {
            if (_arrowPos.HasValue)
                GetArrow.Remove(me.PlayerId, _arrowPos.Value);
            GetArrow.Add(me.PlayerId, newPos);
            _arrowPos = newPos;
        }
    }
    #endregion

    #region GameEndPredicate
    // タスクバトルの TaskBattleGameEndPredicate と同じ流儀。
    // 制限時間が0になったら、一番キルした人を勝者にして終了する。
    public class DummyHunterGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;

            if (!IsActive) return false;
            if (TimeLeft > 0f) return false;

            // 時間切れ: 勝者確定
            IsActive = false;

            byte winnerId = byte.MaxValue;
            int best = -1;
            foreach (var kv in KillCounts)
            {
                if (kv.Value > best)
                {
                    best = kv.Value;
                    winnerId = kv.Key;
                }
            }

            reason = GameOverReason.ImpostorsByKill;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
            CustomWinnerHolder.WinnerIds.Clear();
            if (winnerId != byte.MaxValue && best > 0)
                CustomWinnerHolder.WinnerIds.Add(winnerId);
            else
                // 誰も1体もキルしていなければ全員生存者を勝者にしておく(引き分け扱い)
                foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

            return true;
        }
    }
    #endregion
}

// マップ上に出現するダミー本体。CustomNetObject を継承し IKillableDummy を実装。
public sealed class HunterDummy : CustomNetObject, IKillableDummy
{
    private static readonly string[] SkinIds =
    {
        "skin_Astronaut", "skin_BlackSuit", "skin_CaptainA", "skin_Hazmat",
        "skin_Military", "skin_Police", "skin_Science", "skin_SuitB",
        "skin_Winter", "",
    };
    private static readonly string[] HatIds =
    {
        "hat_PaperHat", "hat_Fedora", "hat_TopHat", "hat_Antenna", "hat_Crown",
        "hat_FloppyHat", "hat_Captain", "hat_Goggles", "hat_HardHat", "hat_Beanie", "",
    };
    private static readonly string[] VisorIds =
    {
        "visor_Visor", "visor_CoolVisor", "visor_GreenVisor", "visor_HalfVisor", "",
    };

    private readonly int _colorId;
    private readonly string _skinId;
    private readonly string _hatId;
    private readonly string _visorId;
    private readonly Vector2 _spawnPos;

    public HunterDummy(Vector2 position)
    {
        var rng = IRandom.Instance;
        _colorId = rng.Next(0, 18);
        _skinId = SkinIds[rng.Next(0, SkinIds.Length)];
        _hatId = HatIds[rng.Next(0, HatIds.Length)];
        _visorId = VisorIds[rng.Next(0, VisorIds.Length)];
        _spawnPos = position;
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        SetAppearance(_colorId, _skinId, _hatId, "", _visorId);
        SetName("Dummy");
        SnapToPosition(_spawnPos);
    }

    public void OnKilled(PlayerControl killer)
    {
        Logger.Info($"Dummy killed by {killer?.Data?.GetLogPlayerName()}", "HunterDummy");
        DummyHunter.OnDummyKilled(killer, this);
    }

    public override void OnMeeting()
    {
        Despawn();
    }
}
