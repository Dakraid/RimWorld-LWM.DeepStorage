namespace DeepStorage.Cache
{
#region
    using System.Linq;

    using Verse;
#endregion

    public class CacheComponent : GameComponent
    {
        public CacheComponent(Game game) { }

        private static void ReplaceWithOriginalComp() =>
            DefDatabase<ThingDef>.AllDefs.ToList().ForEach(
                def =>
                {
                    var compProperties = def.comps.FirstOrDefault(comp => comp.compClass == typeof(CompCachedDeepStorage));

                    if (compProperties != null) { compProperties.compClass = typeof(CompDeepStorage); }
                }
            );

        public static void ReplaceWithCachedComp() =>
            DefDatabase<ThingDef>.AllDefs.ToList().ForEach(
                def =>
                {
                    var compProperties = def.comps.FirstOrDefault(comp => comp.compClass == typeof(CompDeepStorage));

                    if (compProperties != null)
                    {
                        compProperties.compClass = typeof(CompCachedDeepStorage);
                        def.tickerType           = TickerType.Rare;
                    }
                }
            );

    #region Overrides of GameComponent
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) { CacheComponent.ReplaceWithOriginalComp(); }
            else if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs) { CacheComponent.ReplaceWithCachedComp(); }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            CacheComponent.ReplaceWithCachedComp();
        }
    #endregion
    }
}
