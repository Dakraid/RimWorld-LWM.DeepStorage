namespace DeepStorage
{
#region
    using UnityEngine;

    using Verse;
#endregion

    public class DeepStorageMod : Mod
    {
        private Settings _settings;

        public DeepStorageMod(ModContentPack content) : base(content) => _settings = GetSettings<Settings>();

        public override string SettingsCategory() => "LWM's Deep Storage"; // todo: translate?

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);
    }
}
