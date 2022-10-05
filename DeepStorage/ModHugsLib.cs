namespace DeepStorage
{
#region
    using HugsLib;
    using HugsLib.Settings;

    using Verse;
#endregion

    internal class LwmHug : ModBase
    {
    #if DEBUG
        private readonly SettingHandle<bool>[] _debugONorOff = new SettingHandle<bool>[Utils._showDebug.Length];
    #endif
        public override string ModIdentifier => "LWM_DeepStorage";
        public override void DefsLoaded()
        {
        #if DEBUG
            Log.Message("LWM.DeepStorage:  DefsLoaded via HugsLib():");

            for (var i = 1; i < Utils._showDebug.Length; i++)
            {
                _debugONorOff[i] = Settings.GetHandle("turnDebugONorOFF" + (Utils.Dbf) i, "Turn ON/OFF debugging: " + (Utils.Dbf) i, "Turn ON/OFF all debugging - this is a lot of trace, and only available on debug builds", false);
            }
            SettingsChanged();
        #endif
            Properties.RemoveAnyMultipleCompProps();
            DeepStorage.Settings.DefsLoaded();
        }
    #if DEBUG
        public override void SettingsChanged()
        {
            Log.Message("LWM's Deep Storage: Debug settings changed");
            UpdateDebug();
        }

        public void UpdateDebug()
        {
            for (var i = 1; i < Utils._showDebug.Length; i++)
            {
                // 0 is always true
                Utils._showDebug[i] = _debugONorOff[i];
            }
        }
    #endif
    }
}
