using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost;

static class Event
{
    public const int OutOfPeriodRoleIdOffset = 5;

    public static bool IsChristmas = DateTime.Now.Month == 12 && DateTime.Now.Day is 24 or 25;
    public static bool White = DateTime.Now.Month == 3 && DateTime.Now.Day is 14;
    public static bool IsInitialRelease = DateTime.Now.Month == 4 && DateTime.Now.Day is 19;
    public static bool IsHalloween = (DateTime.Now.Month == 10 && DateTime.Now.Day is 31) || (DateTime.Now.Month == 11 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7);
    public static bool GoldenWeek = DateTime.Now.Month == 5 && DateTime.Now.Day is 3 or 4 or 5;
    public static bool April = DateTime.Now.Month == 4 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8;
    public static bool Tanabata = DateTime.Now.Month == 7 && DateTime.Now.Day is > 6 and < 15;
    //public static bool Birthday12 = DateTime.Now.Month == 10 && DateTime.Now.Day is 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26;
    public static bool Birthday12 = true;
    public static bool IsEventDay => IsChristmas || White || IsInitialRelease || IsHalloween || GoldenWeek || April || Birthday12;
    public static bool Special = false;
    public static bool NowRoleEvent => false;
    public static List<string> OptionLoad = new();
    public static bool IsE(this CustomRoles role) => role is CustomRoles.SpeedStar or CustomRoles.Chameleon or CustomRoles.Fortuner;
    public static bool IsEventRole(CustomRoles role) => EventRoles.ContainsKey(role);
    public static int GetRoleId(CustomRoles role, bool isInEventPeriod)
    {
        var roleId = (int)role;
        if (!IsEventRole(role)) return roleId;
        return isInEventPeriod ? roleId : roleId + OutOfPeriodRoleIdOffset;
    }

    public static int GetCurrentRoleId(CustomRoles role, bool useApiData = true)
    {
        if (!IsEventRole(role)) return (int)role;
        return GetRoleId(role, CheckRole(role, useApiData));
    }

    public static int GetConfigId(CustomRoles role, int configId, bool isInEventPeriod)
    {
        if (!IsEventRole(role)) return configId;
        return isInEventPeriod ? configId : configId + OutOfPeriodRoleIdOffset;
    }

    public static int GetCurrentConfigId(CustomRoles role, int configId, bool useApiData = true)
    {
        if (!IsEventRole(role)) return configId;
        return GetConfigId(role, configId, CheckRole(role, useApiData));
    }

    /// <summary>
    /// 期間限定ロールが使用可能かをチェックします<br/>
    /// 通常ロールはtrueを返します
    /// </summary>
    /// <returns>ロールが使用可能ならtrueを返します</returns>
    public static bool CheckRole(CustomRoles role, bool useApiData = true)
    {
        ///イベント役職以外はtrueを返す!!
        if (!EventRoles.TryGetValue(role, out var check)) return true;

        //キャッシュ済みならそっちを使う
        if (cachedEventFlags.TryGetValue((role, useApiData), out bool cachedData))
            return cachedData;

        bool result = check.Invoke();
        if (!useApiData || (VersionInfoManager.version?.Events == null && VersionInfoManager.allversion?.Events == null))
        {
            //apiが使えない状態orフラグがfalseの場合はローカルの状態を使用
            cachedEventFlags[(role, false)] = result;
            return result;
        }

        var events = VersionInfoManager.version?.Events;
        var allverevents = VersionInfoManager.allversion?.Events;

        //全イベント情報をチェック
        if (events != null)
        {
            foreach (var data in events)
            {
                if (result) continue;
                result |= IsEventDataActiveForRole(data, role);
            }
        }
        if (allverevents != null)
        {
            foreach (var data in allverevents)
            {
                if (result) continue;
                result |= IsEventDataActiveForRole(data, role);
            }
        }
        cachedEventFlags[(role, true)] = result;
        return result;
    }

    private static bool IsEventDataActiveForRole(VersionInfoManager.VersionInfo.EventData data, CustomRoles role)
    {
        if (data?.Period == null) return false;

        var isInEventPeriod = data.Period.IsActive;
        return data.RoleId == GetRoleId(role, isInEventPeriod) && isInEventPeriod;
    }

    public static Dictionary<CustomRoles, Func<bool>> EventRoles = new()
    {
        //{CustomRoles.Altair,() => Tanabata},
        //{CustomRoles.Vega,() => Tanabata},
        //{CustomRoles.Amateras,() => Tanabata},
        //{CustomRoles.SpeedStar , () => Special},
        //{CustomRoles.Chameleon , () => Special},
        //{CustomRoles.Cakeshop , () => NowRoleEvent},

        {CustomRoles.MadPukupuku,() => Birthday12}
    };

    public static Dictionary<(CustomRoles role, bool isApiData), bool> cachedEventFlags = new(EventRoles.Count);
}
















//やぁ。気付いちゃった...?( ᐛ )
//ファイル作っちゃうとばれちゃうからね。
//ｶ ｸ ｼ ﾃ ﾙ ﾅ ﾗ ﾏ ｧ ｺ ｺ ｼﾞ ｬ ﾝ ?
public sealed class SpeedStar : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SpeedStar),
            player => new SpeedStar(player),
            CustomRoles.SpeedStar,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            7200,
            SetUpOptionItem,
            "SS",
            OptionSort: (0, 51),
            from: From.Speyrp
        );
    public SpeedStar(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        allplayerspeed.Clear();
        speed = optSpeed.GetFloat();
        abilitytime = optabilitytime.GetFloat();
        cooldown = optcooldown.GetFloat();
        killcooldown = optkillcooldown.GetFloat();
        IsBoosted = false;
    }
    static OptionItem optabilitytime;
    static OptionItem optcooldown;
    static OptionItem optkillcooldown;
    static OptionItem optSpeed;
    static float speed;
    static float abilitytime;
    static float cooldown;
    static float killcooldown;
    bool IsBoosted;
    bool IUsePhantomButton.IsresetAfterKill => !IsBoosted;
    public float CalculateKillCooldown() => killcooldown;

    Dictionary<byte, float> allplayerspeed = new();
    public override void StartGameTasks()
    {
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            allplayerspeed.TryAdd(pc.PlayerId, Main.AllPlayerSpeed[pc.PlayerId]);
        }
    }
    static void SetUpOptionItem()
    {
        optkillcooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        optcooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        optabilitytime = FloatOptionItem.Create(RoleInfo, 12, "GhostNoiseSenderTime", new(1, 300, 1f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        optSpeed = FloatOptionItem.Create(RoleInfo, 13, "SpeedStarSpeed", new(0, 10, 0.05f), 3f, false).SetValueFormat(OptionFormat.Multiplier);
    }
    [PluginModuleInitializer]
    public static void Load()
    {
        Event.OptionLoad.Add("SpeedStar");
        Event.OptionLoad.Add("Chameleon");
        Event.OptionLoad.Add("Cakeshop");
        Event.OptionLoad.Add("VegaandAltair");
    }
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = cooldown;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        ResetCooldown = false;
        AdjustKillCooldown = true;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            Main.AllPlayerSpeed[pc.PlayerId] = speed;
        }
        IsBoosted = true;
        AURoleOptions.PhantomCooldown = abilitytime;
        Player.RpcResetAbilityCooldown();
        UtilsOption.MarkEveryoneDirtySettings();
        _ = new LateTask(() =>
        {
            if (GameStates.InGame)
            {
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = allplayerspeed[pc.PlayerId];
                }
                _ = new LateTask(() => UtilsOption.MarkEveryoneDirtySettings(), 0.2f, "", true);
                IsBoosted = false;
                AURoleOptions.PhantomCooldown = cooldown;
                Player.RpcResetAbilityCooldown();       
            }
        }, abilitytime, "", true);
    }
    public override void OnStartMeeting()
    {
        if (GameStates.InGame)
        {
            IsBoosted = false;
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                Main.AllPlayerSpeed[pc.PlayerId] = allplayerspeed[pc.PlayerId];
            }
            _ = new LateTask(() => UtilsOption.MarkEveryoneDirtySettings(), 0.2f, "", true);
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        Player.ResetKillCooldown();
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        if (isForHud) return GetString("PhantomButtonLowertext");
        return $"<size=50%>{GetString("PhantomButtonLowertext")}</size>";
    }
    public override string GetAbilityButtonText() => GetString("SpeedStarAbility");
}
public sealed class Chameleon : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Chameleon),
            player => new Chameleon(player),
            CustomRoles.Chameleon,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            50500,
            SetUpOptionItem,
            "Ch",
            "#357a39",
            OptionSort: (0, 50),
            from: From.Speyrp
        );
    public Chameleon(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        NowTeam = CustomRoles.NotAssigned;
        TeamList.Clear();

        var ch = true;
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetCustomRole().IsImpostor()) TeamList.Add(CustomRoles.Impostor);
            if (pc.GetCustomRole().IsCrewmate())
            {
                if (ch)
                {
                    TeamList.Add(CustomRoles.Crewmate);
                    ch = false;
                }
                else
                {
                    ch = true;
                }
            }
        }
        if (CustomRoles.Jackal.IsPresent() || CustomRoles.JackalAlien.IsPresent() || CustomRoles.JackalMafia.IsPresent() || CustomRoles.JackalWolf.IsPresent())
            TeamList.Add(CustomRoles.Jackal);

        if (CustomRoles.Egoist.IsPresent())
            TeamList.Add(CustomRoles.Egoist);
        if (CustomRoles.Remotekiller.IsPresent())
            TeamList.Add(CustomRoles.Remotekiller);
        if (CustomRoles.CountKiller.IsPresent())
            TeamList.Add(CustomRoles.CountKiller);
        if (CustomRoles.DoppelGanger.IsPresent())
            TeamList.Add(CustomRoles.DoppelGanger);
        if (CustomRoles.GrimReaper.IsPresent())
            TeamList.Add(CustomRoles.GrimReaper);
        if (CustomRoles.Fox.IsPresent())
            TeamList.Add(CustomRoles.Fox);
        if (CustomRoles.Arsonist.IsPresent())
            TeamList.Add(CustomRoles.Arsonist);
        if (CustomRoles.StandMaster.IsPresent())
            TeamList.Add(CustomRoles.StandMaster);
        if (CustomRoles.PavlovOwner.IsPresent() || CustomRoles.PavlovDog.IsPresent())
            TeamList.Add(CustomRoles.PavlovOwner);
        if (CustomRoles.Eater.IsPresent())
            TeamList.Add(CustomRoles.Eater);
        if (CustomRoles.Huntman.IsPresent())
            TeamList.Add(CustomRoles.Huntman);
    }
    CustomRoles NowTeam;
    List<CustomRoles> TeamList = new();
    static void SetUpOptionItem() => RoleAddAddons.Create(RoleInfo, 20);
    public override void StartGameTasks() => ChengeTeam();
    void ChengeTeam()
    {
        if (AmongUsClient.Instance.AmHost is false) return;
        var oldteam = NowTeam;
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Jackal or CustomRoles.JackalAlien or CustomRoles.JackalMafia or CustomRoles.JackalWolf))
            TeamList.Remove(CustomRoles.Jackal);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Egoist))
            TeamList.Remove(CustomRoles.Egoist);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Remotekiller))
            TeamList.Remove(CustomRoles.Remotekiller);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.CountKiller))
            TeamList.Remove(CustomRoles.CountKiller);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.DoppelGanger))
            TeamList.Remove(CustomRoles.DoppelGanger);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.GrimReaper))
            TeamList.Remove(CustomRoles.GrimReaper);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Fox))
            TeamList.Remove(CustomRoles.Fox);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Arsonist))
            TeamList.Remove(CustomRoles.Arsonist);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.StandMaster))
            TeamList.Remove(CustomRoles.StandMaster);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.PavlovOwner))
            TeamList.Remove(CustomRoles.PavlovOwner);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Eater))
            TeamList.Remove(CustomRoles.Eater);
        if (!PlayerCatch.AllAlivePlayerControls.Any(p => p.GetCustomRole() is CustomRoles.Huntman))
            TeamList.Remove(CustomRoles.Huntman);

        //リストをシャッフル! → 更にランダム！
        NowTeam = TeamList.OrderBy(x => Guid.NewGuid()).ToArray()[IRandom.Instance.Next(TeamList.Count)];

        Logger.Info($"Now : {NowTeam}", "Chameleon");

        SendRpc();
        if (oldteam != NowTeam) UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }
    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)NowTeam);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        NowTeam = (CustomRoles)reader.ReadPackedInt32();
    }
    public override void OverrideTrueRoleName(ref UnityEngine.Color roleColor, ref string roleText) => roleText = NowTeam == CustomRoles.PavlovOwner ? Translator.GetString($"{CustomRoles.PavlovDog}").Color(UtilsRoleText.GetRoleColor(NowTeam)) + Translator.GetString("Chameleon") : Translator.GetString($"{NowTeam}").Color(UtilsRoleText.GetRoleColor(NowTeam)) + Translator.GetString("Chameleon");
    public override void AfterMeetingTasks() => _ = new LateTask(() => { if (!GameStates.CalledMeeting) ChengeTeam(); }, 5f, "", true);
    public bool CheckWin(ref CustomRoles winnerRole) => ((CustomRoles)CustomWinnerHolder.WinnerTeam == NowTeam) || CustomWinnerHolder.AdditionalWinnerRoles.Contains(NowTeam);
}











































//よく見つけたね...(
public sealed class MadPukupuku : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadPukupuku),
            player => new MadPukupuku(player),
            CustomRoles.MadPukupuku,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            22900,
            SetupOptionItem,
            "mpk",
            OptionSort: (2, 2),
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            from: From.TownOfHost_Pko
        );
    public MadPukupuku(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
    }
    static OptionItem OptionCanVent;
    static OptionItem OptionRevengeImpostor;
    static OptionItem OptionNotify;

    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    public HashSet<byte> VotedPlayerId = new();
    bool IsExild = false;
    enum Op
    {
        MadPukuPukuCanRevengeImpostor,
        MadPukuPukuNotify
    }

    public static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        OptionRevengeImpostor = BooleanOptionItem.Create(RoleInfo, 11, Op.MadPukuPukuCanRevengeImpostor, false, false);
        OptionNotify = BooleanOptionItem.Create(RoleInfo, 12, Op.MadPukuPukuNotify, false, false);
        RoleAddAddons.Create(RoleInfo, 20);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
    public override void Add()
    {
        //テスト用
        _ = new LateTask(() =>
        {
            VotedPlayerId.Add(Player.PlayerId);

            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                VotedPlayerId.Add(p.PlayerId);
            }
        }, 2f, "MPK_Voted_Test", true);
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (Player.PlayerId != exiled.PlayerId) return;
        IsExild = true;
        if (!OptionNotify.GetBool()) return;
        foreach (var p in VotedPlayerId)
        {
            Utils.SendMessage(GetString("MadPukuPukuNotify"), p);
        }
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished && !Player.IsAlive() && IsExild)
        {
            foreach (var p in VotedPlayerId)
            {
                var pc = PlayerCatch.GetPlayerById(p);
                if ((!pc.Is(CustomRoleTypes.Impostor)) || OptionRevengeImpostor.GetBool())
                {
                    if (pc.PlayerId != Player.PlayerId)
                    {
                        PlayerState.GetByPlayerId(pc.PlayerId).DeathReason = CustomDeathReason.Poisoned;
                        pc.RpcMurderPlayerV2(pc);
                        VotedPlayerId.Remove(p);
                    }
                }
            }
        }
        return true;
    }
}
