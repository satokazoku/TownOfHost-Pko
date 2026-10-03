using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Predator : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Predator),
                player => new Predator(player),
                CustomRoles.Predator,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Impostor,
                127500,
                SetupOptionItem,
                "Pdr",
                OptionSort: (7, 14)
            );
        public Predator(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            CurrentKillCool = KillCoolDown.GetFloat();
        }
        static OptionItem KillCoolDown;
        static OptionItem ZoukaCool;
        static OptionItem GensyoCool;
        static OptionItem Zouka;
        static OptionItem Gensyo;
        float CurrentKillCool;
        public static readonly CustomRoleTypes[] PickTypes =
        {
            CustomRoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
        };
        private static void SetupOptionItem()
        {
            var PickRoleTypesString = PickTypes.Select(x => x.ToString()).ToArray();

            KillCoolDown = FloatOptionItem.Create(RoleInfo, 10, "KillCooldown", OptionBaseCoolTime, 35f, false).SetValueFormat(OptionFormat.Seconds);

            Gensyo = StringOptionItem.Create(RoleInfo, 11, "PredatorGensyo", PickRoleTypesString, 0, false);
            Zouka = StringOptionItem.Create(RoleInfo, 12, "PredatorZouka", PickRoleTypesString, 1, false);

            GensyoCool = FloatOptionItem.Create(RoleInfo, 13, "PredatorGensyoCool", OptionBaseCoolTime, 10f, false).SetValueFormat(OptionFormat.Seconds);
            ZoukaCool = FloatOptionItem.Create(RoleInfo, 14, "PredatorZoukaCool", OptionBaseCoolTime, 10f, false).SetValueFormat(OptionFormat.Seconds);
        }
        public float CalculateKillCooldown() => CurrentKillCool;
        public override void AfterMeetingTasks()
        {
            CurrentKillCool = KillCoolDown.GetFloat();
        }
        private static CustomRoleTypes ZoukaType => PickTypes[Zouka.GetValue()];
        private static CustomRoleTypes GensyoType => PickTypes[Gensyo.GetValue()];

        public void OnMurderPlayerAsKiller(MurderInfo info)
        {
            var (killer, target) = info.AttemptTuple;
            var baseCool = KillCoolDown.GetFloat();

            // Madmateはクルー扱いにする
            bool isCrewLike = target.Is(CustomRoleTypes.Crewmate) || target.Is(CustomRoleTypes.Madmate);
            bool Match(CustomRoleTypes type) =>
                target.Is(type) || (type == CustomRoleTypes.Crewmate && target.Is(CustomRoleTypes.Madmate));

            CurrentKillCool = baseCool;
            if (Match(ZoukaType))
            {
                CurrentKillCool = baseCool + ZoukaCool.GetFloat();
            }
            else if (Match(GensyoType))
            {
                CurrentKillCool = Mathf.Max(0f, baseCool - GensyoCool.GetFloat());
            }

            Main.AllPlayerKillCooldown[killer.PlayerId] = CurrentKillCool;
            killer.SyncSettings();
            killer.SetKillCooldown();
        }
    }
}