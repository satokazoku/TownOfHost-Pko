using TownOfHost.Modules;

namespace TownOfHost
{
    public class PresetOptionItem : OptionItem
    {
        // 必須情報
        public IntegerValueRule Rule;
        public static OptionItem Preset;

        // コンストラクタ
        public PresetOptionItem(int defaultValue, TabGroup tab)
        : base(0, "Preset", defaultValue, tab, true)
        {
            Rule = (0, NumPresets - 1, 1);
        }
        public static PresetOptionItem Create(int defaultValue, TabGroup tab)
        {
            return new PresetOptionItem(defaultValue, tab);
        }

        // Getter
        public override int GetInt() => Rule.GetValueByIndex(CurrentValue);
        public override float GetFloat() => Rule.GetValueByIndex(CurrentValue);
        public override string GetString()//プリセット名決める適菜奴作りたいなぁ。
        {
            var ch = "";
            if (GameModeManager.IsStandardClass())
            {
                var text = UtilsShowOption.GetRoleTypesCount();
                if (text != "")
                    ch += $"<size=70%>{text}</size>";
            }
            return Main.GetPresetName(CurrentValue) + "<size=50%>\n" + (ch == "" ? Translator.GetString($"{Options.CurrentGameMode}") : ch) + "</size>";
        }
        public override int GetValue()
            => Rule.RepeatIndex(base.GetValue());

        // Setter
        public override void SetValue(int value, bool doSync = true)
        {
            base.SetValue(Rule.RepeatIndex(value), doSync);
            SwitchPreset(Rule.RepeatIndex(value), doSync);
        }
        public override void SetValue(int afterValue, bool doSave, bool doSync = true)
        {
            base.SetValue(Rule.RepeatIndex(afterValue), doSave, doSync);
            SwitchPreset(Rule.RepeatIndex(afterValue), doSync);
            VanillaOptionHolder.SetVanillaValue();
        }
    }
}
