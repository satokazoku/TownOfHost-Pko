using System;
using AmongUs.GameOptions;

namespace TownOfHost.Modules
{
    public class NormalGameOptionsSender : GameOptionsSender
    {
        public override IGameOptions BasedGameOptions =>
            GameOptionsManager.Instance.CurrentGameOptions;
        public override bool IsDirty
        {
            get
            {
                try
                {
                    if (!GameManager.Instance) return false;
                    if (GameManager.Instance.LogicComponents == null) return false;
                    if (_logicOptions == null || !GameManager.Instance?.LogicComponents?.Contains(_logicOptions) == true)
                    {
                        foreach (var glc in GameManager.Instance?.LogicComponents)
                            if (glc.TryCast<LogicOptions>(out var lo))
                                _logicOptions = lo;
                    }
                    return _logicOptions != null && (_logicOptions?.IsDirty ?? false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"{ex}", "NomalGameOptionsSender");
                    return false;
                }
            }
            protected set
            {
                if (_logicOptions != null)
                    _logicOptions.ClearDirtyFlag();
            }
        }
        private LogicOptions _logicOptions;

        public override IGameOptions BuildGameOptions()
            => BasedGameOptions;
    }
}