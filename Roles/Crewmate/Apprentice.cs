using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Apprentice : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Apprentice),
            player => new Apprentice(player),
            CustomRoles.Apprentice,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            361000,
            SetupOptionItem,
            "ap",
            "#c8a46e",
            (2, 1),
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );

    public Apprentice(PlayerControl player)
        : base(RoleInfo, player)
    {
        masterPlayerId = byte.MaxValue;
        masterOriginalRole = CustomRoles.NotAssigned;
        hasInherited = false;
        isTaskComplete = false;
    }

    static OptionItem OptionCanVent;
    static OptionItem OptionVentCooldown;
    static OptionItem OptionVentMaxTime;
    static OverrideTasksData Tasks;

    enum OptionName
    {
        ApprenticeCanVent,
        ApprenticeVentCooldown,
        ApprenticeVentMaxTime,
    }

    static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, OptionName.ApprenticeCanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.ApprenticeVentCooldown,
            new(0f, 180f, 0.5f), 15f, false, OptionCanVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionVentMaxTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.ApprenticeVentMaxTime,
            new(0.5f, 180f, 0.5f), 5f, false, OptionCanVent)
            .SetZeroNotation(OptionZeroNotation.Infinity)
            .SetValueFormat(OptionFormat.Seconds);
        Tasks = OverrideTasksData.Create(RoleInfo, 20);
    }

    // ★ 親方情報
    byte masterPlayerId;
    CustomRoles masterOriginalRole;

    // ★ 継承条件フラグ
    bool hasInherited;
    bool isTaskComplete;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (OptionCanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = OptionVentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = OptionVentMaxTime.GetFloat();
        }
    }

    public override bool CanClickUseVentButton => OptionCanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => OptionCanVent.GetBool();

    public override void Add()
    {
        hasInherited = false;
        isTaskComplete = false;
        masterPlayerId = byte.MaxValue;
        masterOriginalRole = CustomRoles.NotAssigned;

        // ★ 弟子入り：ゲーム開始時にクルー陣営からランダムに親方を決める（自分以外）
        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.AmHost) return;

            var crewCandidates = PlayerCatch.AllPlayerControls
                .Where(pc => pc.PlayerId != Player.PlayerId
                          && pc.GetCustomRole().GetCustomRoleTypes() == CustomRoleTypes.Crewmate
                          && pc.IsAlive())
                .ToArray();

            if (crewCandidates.Length == 0) return;

            var master = crewCandidates[IRandom.Instance.Next(crewCandidates.Length)];
            masterPlayerId = master.PlayerId;
            masterOriginalRole = master.GetCustomRole();

            Logger.Info($"[Apprentice] {Player.Data.GetLogPlayerName()} の親方: {master.Data.GetLogPlayerName()} ({masterOriginalRole})", "Apprentice");
            SendRpc();
        }, 3f, "Apprentice.AssignMaster", true);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (hasInherited) return true;

        // ★ タスク完了条件チェック
        if (MyTaskState.IsTaskFinished)
        {
            isTaskComplete = true;
            SendRpc();
            TryInherit();
        }
        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (hasInherited) return;
        if (masterPlayerId == byte.MaxValue) return;
        if (!GameStates.IsInTask) return;

        // ★ 親方が死亡しているかチェック
        var master = PlayerCatch.GetPlayerById(masterPlayerId);
        if (master != null && !master.IsAlive())
            TryInherit();
    }

    void TryInherit()
    {
        if (hasInherited) return;
        if (!isTaskComplete) return;

        var master = PlayerCatch.GetPlayerById(masterPlayerId);
        // 親方が死亡していれば継承
        if (master != null && master.IsAlive()) return;

        hasInherited = true;

        // ★ 親方の元の役職（サイドキックなどで変わる前のクルー役）を継承
        var inheritRole = masterOriginalRole;

        // ★ 念のため継承役職がクルー陣営かチェック
        if (inheritRole == CustomRoles.NotAssigned
            || inheritRole.GetCustomRoleTypes() != CustomRoleTypes.Crewmate)
        {
            // フォールバック: 通常クルー
            inheritRole = CustomRoles.Crewmate;
        }

        Logger.Info($"[Apprentice] {Player.Data.GetLogPlayerName()} が {inheritRole} を継承！", "Apprentice");
        UtilsGameLog.AddGameLog("Apprentice",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsRoleText.GetRoleName(inheritRole)} を継承しました");

        if (!Utils.RoleSendList.Contains(Player.PlayerId))
            Utils.RoleSendList.Add(Player.PlayerId);

        Player.RpcSetCustomRole(inheritRole, log: null);
        UtilsOption.MarkEveryoneDirtySettings();
        UtilsNotifyRoles.NotifyRoles();

        SendRpc();
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(masterPlayerId);
        sender.Writer.Write((int)masterOriginalRole);
        sender.Writer.Write(hasInherited);
        sender.Writer.Write(isTaskComplete);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        masterPlayerId = reader.ReadByte();
        masterOriginalRole = (CustomRoles)reader.ReadInt32();
        hasInherited = reader.ReadBoolean();
        isTaskComplete = reader.ReadBoolean();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (hasInherited) return "";

        // ★ 条件達成状況をアイコンで表示（親方が誰かは表示しない）
        string taskIcon = isTaskComplete ? "<color=#00ff88>①✓</color>" : "<color=#888888>①</color>";
        // 親方死亡条件は外から見えないので常にグレー
        string masterIcon = "<color=#888888>②</color>";
        return $"<color={RoleInfo.RoleColorCode}>{taskIcon}{masterIcon}</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";
        if (hasInherited) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!isTaskComplete)
            return $"{size}<color={color}>①タスクを完了させよう</color>";
        return $"{size}<color={color}>①完了！②親方の死を待て…</color>";
    }
}