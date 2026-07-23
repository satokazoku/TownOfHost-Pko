using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost;

public static class DummyHunter
{
    public static OptionItem OptionTimeLimit;
    public static OptionItem OptionPhantomCooldown;
    public static OptionItem OptionMaxDummyCount;
    public static OptionItem OptionShowArrow;
    public static OptionItem OptionArrowDelay;
    public static OptionItem OptionShowTopPlayer;

    public static bool IsActive = false;
    public static float TimeLeft = 0f;
    public static float ElapsedTime = 0f;
    public static Dictionary<byte, int> KillCounts = new();
    public static List<HunterDummy> ActiveDummies = new();

    private static float _syncTimer = 0f;

    public static bool IsThisMode => Options.CurrentGameMode == CustomGameMode.DummyHunter;

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

    public static void OnGameStart()
    {
        IsActive = true;
        TimeLeft = OptionTimeLimit.GetFloat();
        ElapsedTime = 0f;
        _syncTimer = 0f;
        ActiveDummies.Clear();
        KillCounts.Clear();
        foreach (var pc in PlayerCatch.AllPlayerControls)
            KillCounts[pc.PlayerId] = 0;

        if (!AmongUsClient.Instance.AmHost) return;

        int max = OptionMaxDummyCount.GetInt();
        for (int i = 0; i < max; i++)
        {
            int index = i;
            _ = new LateTask(() =>
            {
                if (!IsActive) return;
                SpawnDummy();
            }, 0.5f + index * 0.1f, $"DummyHunter.Spawn{index}", true);
        }
    }

    public static void OnPhantomClick(PlayerControl killer)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (killer == null || !killer.IsAlive()) return;

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

        _ = new LateTask(() =>
        {
            if (killer == null || !killer.IsAlive()) return;
            AURoleOptions.PhantomCooldown = OptionPhantomCooldown.GetFloat();
            killer.RpcResetAbilityCooldown();
        }, 0.1f, "DummyHunter.PhantomCD", true);
    }

    public static void SpawnDummy()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (ActiveDummies.Count >= OptionMaxDummyCount.GetInt()) return;
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

        _ = new LateTask(() =>
        {
            if (!IsActive) return;
            SpawnDummy();
        }, 0.2f, "DummyHunter.Respawn", true);

        Utils.AllPlayerKillFlash();
    }

    public static void OnFixedUpdate()
    {
        if (!IsThisMode || !IsActive) return;
        if (!GameStates.InGame || GameStates.IsMeeting) return;

        ElapsedTime += Time.fixedDeltaTime;
        TimeLeft -= Time.fixedDeltaTime;

        UpdateUI();

        if (!AmongUsClient.Instance.AmHost) return;

        _syncTimer += Time.fixedDeltaTime;
        if (_syncTimer >= 3f)
        {
            _syncTimer = 0f;
            foreach (var kv in KillCounts)
                RpcSyncScore(kv.Key, kv.Value);
        }

        if (ActiveDummies.Count < OptionMaxDummyCount.GetInt())
            SpawnDummy();

        if (TimeLeft <= 0f)
        {
            IsActive = false;
            EndGame();
        }
    }

    private static void EndGame()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameManager.Instance == null) return;

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

        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
        if (winnerId != byte.MaxValue)
            CustomWinnerHolder.WinnerIds.Add(winnerId);

        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
    }

    public static void OnMeeting()
    {
        foreach (var dummy in ActiveDummies.ToArray())
            dummy.Despawn();
        ActiveDummies.Clear();
    }

    public static void AfterMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsActive) return;
        int max = OptionMaxDummyCount.GetInt();
        for (int i = 0; i < max; i++)
        {
            int index = i;
            _ = new LateTask(() =>
            {
                if (!IsActive) return;
                SpawnDummy();
            }, 0.5f + index * 0.1f, $"DummyHunter.ReSpawn{index}", true);
        }
    }

    public static void OnGameEnd()
    {
        IsActive = false;
        foreach (var dummy in ActiveDummies.ToArray())
            dummy?.Despawn();
        ActiveDummies.Clear();
        KillCounts.Clear();
    }

    private static void UpdateUI()
    {
        if (HudManager.Instance == null || PlayerControl.LocalPlayer == null) return;

        var lower = HudManagerPatch.LowerInfoText;
        if (lower == null) return;

        int myScore = KillCounts.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var s) ? s : 0;

        string text = $"<size=140%><color=#e0b0ff>【{GetString("DummyHunter")}】</color></size>\n";
        text += $"<color=#ff5555>{GetString("DummyHunterTimeLeftText")}: {Mathf.CeilToInt(Mathf.Max(0f, TimeLeft))}s</color>  ";
        text += $"<color=#55ff55>{GetString("DummyHunterMyKillText")}: {myScore}</color>";

        if (OptionShowTopPlayer.GetBool())
        {
            byte topId = byte.MaxValue; int top = -1;
            foreach (var kv in KillCounts)
                if (kv.Value > top) { top = kv.Value; topId = kv.Key; }
            var topPc = PlayerCatch.GetPlayerById(topId);
            if (topPc != null && top > 0)
                text += $"\n<color=#ffd700>{GetString("DummyHunterTopText")}: {topPc.GetRealName()} ({top})</color>";
        }

        if (OptionShowArrow.GetBool() && ElapsedTime >= OptionArrowDelay.GetFloat() && ActiveDummies.Count > 0)
        {
            var myPos = PlayerControl.LocalPlayer.GetTruePosition();
            var closest = ActiveDummies.OrderBy(d => Vector2.Distance(myPos, d.Position)).FirstOrDefault();
            if (closest != null)
            {
                float dist = Vector2.Distance(myPos, closest.Position);
                string arrow = GetArrowDirection(myPos, closest.Position);
                text += $"\n<color=#00ccff>{arrow} {dist:F0}m</color>";
            }
        }

        lower.enabled = true;
        lower.text = text;
    }

    public static void RpcSyncScore(byte playerId, int score)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        KillCounts[playerId] = score;
        UtilsNotifyRoles.NotifyRoles();

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
        UtilsNotifyRoles.NotifyRoles();
    }

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

    private static string GetArrowDirection(Vector2 from, Vector2 to)
    {
        var dir = (Vector3)to - (Vector3)from;
        dir.z = 0;
        if (dir.magnitude < 2) return "・";
        var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180f + 22.5f;
        int index = ((int)(angle / 45f)) % 8;
        string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
        return arrows[index];
    }

    public class DummyHunterGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorDisconnect;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            return false;
        }
    }
}

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
