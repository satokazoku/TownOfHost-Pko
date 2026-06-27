//投票によりこの役職は一度封印。
//今後調整等をして復活する場合もあります。

/*using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

public sealed class Suicider : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Suicider),
            player => new Suicider(player),
            CustomRoles.Suicider,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Neutral,
            54800,
            SetupOptionItem,
            "sc",
            "#696969",
            (5, 5),
            true,
            countType: CountTypes.OutOfGame,
            from: From.SuperNewRoles
        );

    public Suicider(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        InitialTimer = OptionInitialTimer.GetFloat();
        TaskTimeBonus = OptionTaskTimeBonus.GetFloat();
        FallChance = OptionFallChance.GetFloat() / 100f;
        HasWon = false;
        timer = InitialTimer;
        hasExploded = false;
        LastCooltime = -1;
    }

    static OptionItem OptionInitialTimer; static float InitialTimer;
    static OptionItem OptionTaskTimeBonus; static float TaskTimeBonus;
    static OptionItem OptionFallChance; static float FallChance;
    static OverrideTasksData Tasks;

    enum OptionName
    {
        SuiciderInitialTimer,
        SuiciderTaskTimeBonus,
        SuiciderFallChance,
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 8, defo: 1);
        OptionInitialTimer = FloatOptionItem.Create(RoleInfo, 10, OptionName.SuiciderInitialTimer,
            new(10f, 300f, 5f), 60f, false).SetValueFormat(OptionFormat.Seconds);
        OptionTaskTimeBonus = FloatOptionItem.Create(RoleInfo, 11, OptionName.SuiciderTaskTimeBonus,
            new(0f, 60f, 1f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionFallChance = FloatOptionItem.Create(RoleInfo, 12, OptionName.SuiciderFallChance,
            new(0f, 100f, 5f), 50f, false).SetValueFormat(OptionFormat.Percent);
        Tasks = OverrideTasksData.Create(RoleInfo, 20);
    }

    float timer;
    bool hasExploded;
    bool HasWon;
    int LastCooltime;

    public override void Add()
    {
        timer = InitialTimer;
        hasExploded = false;
        HasWon = false;
        LastCooltime = -1;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Mathf.Max(timer, 0.1f);
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override void OnSpawn(bool initialState = false)
    {
        AURoleOptions.EngineerCooldown = Mathf.Max(timer, 0.1f);
        Player.RpcResetAbilityCooldown(Sync: true);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!Player.IsAlive()) return true;

        timer += TaskTimeBonus;
        if (timer > InitialTimer) timer = InitialTimer;

        if (MyTaskState.IsTaskFinished && !HasWon)
            HasWon = true;

        SyncCooldown();
        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;
        if (!Player.IsAlive() || hasExploded) return;
        if (MyTaskState.IsTaskFinished) return;

        timer -= Time.fixedDeltaTime;
        if (timer < 0f) timer = 0f;

        var now = Mathf.FloorToInt(timer);
        if (now != LastCooltime)
        {
            LastCooltime = now;
            SyncCooldown();
        }

        if (timer <= 0f)
            Explode();
    }

    void SyncCooldown()
    {
        Player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            if (Player.IsAlive())
                Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "Suicider.SyncCD", true);
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        bool isFall = IRandom.Instance.Next(0, 100) < (int)(FallChance * 100);
        PlayerState.GetByPlayerId(Player.PlayerId).DeathReason =
            isFall ? CustomDeathReason.Fall : CustomDeathReason.Suicide;

        Player.RpcMurderPlayerV2(Player);

        UtilsGameLog.AddGameLog("Suicider",
            $"{UtilsName.GetPlayerColor(Player)}が自爆した ({(isFall ? "転落死" : "自殺")})");
    }

    public override void OnStartMeeting()
    {
        LastCooltime = -1;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        LastCooltime = -1;
        SyncCooldown();
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (HasWon && Player.IsAlive())
        {
            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Suicider, Player.PlayerId, true))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
                CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            }
            return;
        }

        if (Player.IsAlive() && CustomWinnerHolder.WinnerTeam != CustomWinner.Suicider)
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (MyTaskState.IsTaskFinished)
            return $"<color={RoleInfo.RoleColorCode}>(完了)</color>";

        int sec = Mathf.CeilToInt(timer);
        string color = timer > InitialTimer * 0.5f ? RoleInfo.RoleColorCode : "#ff4444";
        return $"<color={color}>({sec}s)</color>";
    }
}
*/