using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

// ダミーハンター用の役職。
// ダミーハンターモードでは全プレイヤーがこの役職(ベース RoleTypes.Phantom)になり、
// ファントムボタンのワンクリックで一番近いダミーへワープしてキルする。
//
// モード本体のロジックは DummyHunter(Modules/GameMode/DummyHunter.cs)に委譲する。
public sealed class DummyHunterPlayer : RoleBase, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(DummyHunterPlayer),
            player => new DummyHunterPlayer(player),
            CustomRoles.DummyHunterPlayer,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            210100,
            SetupOptionItem,
            "dhp",
            "#e0b0ff",
            from: From.TownOfHost_Pko
        );

    public DummyHunterPlayer(PlayerControl player)
        : base(RoleInfo, player)
    {
        // 他プレイヤーの名前の右にもキル数を表示するために登録
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    // このモードのオプションは DummyHunter.SetupOptionItem 側で作成するため、
    // 役職側では何も作らない(通常のロール抽選オプションを出さない)。
    static void SetupOptionItem()
    {
    }

    // ファントム置き換え役職として扱う(ワンクリックボタンを出す)。
    bool IUsePhantomButton.IsPhantomRole => true;
    // キルはダミーに対してなので、キル後のクールリセット処理は使わない。
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        // ファントムCDはオプション値を使う
        AURoleOptions.PhantomCooldown = DummyHunter.OptionPhantomCooldown != null
            ? DummyHunter.OptionPhantomCooldown.GetFloat()
            : 3f;
    }

    // ファントムワンクリック時の処理(ホストのみ)。
    // 一番近いダミーへワープしてキルする(キルワープ再現)。実処理は DummyHunter に委譲。
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        // キルクール調整はしない(このモードにキルボタンは無い)。
        AdjustKillCooldown = false;
        // ファントム戻し処理は行う(ボタンを再表示するため)。
        ResetCooldown = true;

        if (!AmongUsClient.Instance.AmHost) return;
        DummyHunter.OnPhantomClick(Player);
    }

    // 自分の名前の右に付ける表示：キル数 + 矢印(一番近いダミー)
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        // 自分の名前にのみキル数と矢印を出す
        if (!Is(seer) || !Is(seen)) return "";

        string mark = DummyHunter.GetScoreMark(Player.PlayerId);

        // 矢印(一番近いダミーの方向)
        if (DummyHunter.IsActive && DummyHunter.OptionShowArrow != null && DummyHunter.OptionShowArrow.GetBool()
            && DummyHunter.OptionArrowDelay != null && DummyHunter.ElapsedTime >= DummyHunter.OptionArrowDelay.GetFloat())
        {
            var arrowPos = DummyHunter.GetClosestDummyPosition(seer);
            if (arrowPos.HasValue)
            {
                string arrow = GetArrow.GetArrows(seer, arrowPos.Value);
                if (!string.IsNullOrEmpty(arrow))
                    mark += $"<color=#00ccff>{arrow}</color>";
            }
        }
        return mark;
    }

    // 他プレイヤーの名前の右にもキル数を出す(自分視点で他人のスコアを見せる)。
    // ※ 同じ処理を各 DummyHunterPlayer が登録するため、seer==Player の時だけ描画して重複を防ぐ。
    public string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (!Is(seer)) return "";      // 自分視点のときだけ処理(重複防止)
        if (Is(seen)) return "";       // 自分の名前は GetMark 側で処理済み
        return DummyHunter.GetScoreMark(seen.PlayerId);
    }
}
