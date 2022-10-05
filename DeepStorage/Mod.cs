using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
    public class DeepStorageMod : Mod {
        private Settings _settings;

        public DeepStorageMod(ModContentPack content) : base(content) => this._settings=GetSettings<Settings>();

        public override string SettingsCategory() => "LWM's Deep Storage"; // todo: translate?

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);
    }

}
