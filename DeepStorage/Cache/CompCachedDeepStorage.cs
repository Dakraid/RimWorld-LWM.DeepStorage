namespace DeepStorage.Cache
{
#region
    using RimWorld;

    using UnityEngine;

    using Verse;
#endregion

    public class CompCachedDeepStorage : CompDeepStorage
    {
        private const int TickRateQuotient = GenTicks.TickLongInterval / GenTicks.TickRareInterval;
        private StorageSettings _storageSetting;

        public CompCachedDeepStorage() => _cached = true;

        public Building_Storage StorageBuilding { get; private set; }

        public StorageSettings StorageSettings
        {
            get
            {
                if (_storageSetting == null) { _storageSetting = StorageBuilding.settings; }

                return _storageSetting;
            }
        }

        public CellStorageCollection CellStorages { get; private set; }

        private bool StackableAt(Thing thing, Map map, DeepStorageCellStorageModel cellStorage, float unitWeight)
        {
            if (!CanStore(thing, map)) { return false; }

            return cellStorage.CanAccept(thing, unitWeight);
        }

        private bool CanStore(Thing thing, Map map)
        {
            if (map != StorageBuilding.Map) { return false; }

            if (!thing.def.EverStorable(false) || !StorageSettings.AllowedToAccept(thing)) { return false; }

            // Jewelry box can't store a rocket launcher.
            if (_limitingFactorForItem > 0f)
            {
                if (thing.GetStatValue(_stat) > _limitingFactorForItem)
                {
                    Utils.Warn(Utils.Dbf.CheckCapacity, "  Cannot store because " + _stat + " of " + thing.GetStatValue(_stat) + " > limit of " + _limitingFactorForItem);

                    return false;
                }
            }

            return true;
        }

    #region Overrides of CompDeepStorage
        /// <summary>
        ///     Check if <paramref name = "thing" /> can be placed at <paramref name = "cell" />
        /// </summary>
        /// <param name = "thing"> Thing to check. </param>
        /// <param name = "cell"> Target position. </param>
        /// <param name = "map"> Map that holds <paramref name = "thing" />. </param>
        /// <returns> Returns <see langword = "true" /> if there is room for <paramref name = "thing" /> </returns>
        public override bool StackableAt(Thing thing, IntVec3 cell, Map map)
        {
            if (!CellStorages.TryGetCellStorage(cell, out var cellStorage)) { return false; }

            return StackableAt(thing, map, cellStorage, thing.GetStatValue(StatDefOf.Mass));
        }

        public override bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
        {
            capacity = CapacityToStoreThingAt(thing, map, cell);

            return capacity > 0;
        }

        public override int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell, bool calledFromPatch = false)
        {
            if (!CellStorages.TryGetCellStorage(cell, out var cellStorage)) { return 0; }

            var unitWeight = thing.GetStatValue(StatDefOf.Mass);

            if (!CanStore(thing, map)) { return 0; }

            var emptyStack          = MaxNumberStacks - cellStorage.Count;
            var spareSpaceOnNonFull = cellStorage.SpareSpaceOnNonFull(thing);
            var spareStacks         = emptyStack * thing.def.stackLimit + spareSpaceOnNonFull;

            if (_limitingTotalFactorForCell > 0)
            {
                var spareStacksByWeight  = Mathf.FloorToInt((_limitingTotalFactorForCell - cellStorage.TotalWeight) / unitWeight);
                var spareStacksByMinimum = thing.def.stackLimit * (MinNumberStacks - cellStorage.Count);

                return Mathf.Max(Mathf.Min(spareStacks, spareStacksByWeight), spareStacksByMinimum, 0);
            }

            return spareStacks;
        }

        public override void PostExposeData()
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars) { base.PostExposeData(); }

            if (CellStorages == null) { CellStorages = new CellStorageCollection(parent as Building_Storage, this); }

            CellStorages.ExposeData();
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);

            StorageBuilding = (Building_Storage) parent;
        }
    #endregion

    #region Overrides of ThingComp
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                Log.Message("Initialize cached DS unit");
                CellStorages = new CellStorageCollection(parent as Building_Storage, this);
            }

            Utils.Mess(Utils.Dbf.Cache, $"TickerType: {parent.def.tickerType}");
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            CellStorages.Clear();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            CellStorages.Clear();
        }

        /// <summary>
        ///     CompTickRare is only called when Remainder, which is equal to Find.TickManager.TicksGame %
        ///     GenTicks.TicksRareInterval,
        ///     equals to the index of list to which this comp is added. Given TicksRareInterval = 250 and TicksLongInterval =
        ///     2000,
        ///     _tickRateQuotient = 2000 / 250 = 8. Whenever this method is called, TicksGame = 250 * Multiplier +
        ///     Remainder(position in the tick list).
        ///     Therefore, SelfCorrection will only be invoked when Multiplier is a multiple of _tickRateQuotient.
        /// </summary>
        public override void CompTickRare()
        {
            Utils.Mess(Utils.Dbf.Cache, $"Quotient: {Find.TickManager.TicksGame / GenTicks.TickRareInterval}, _tickQuotient: {CompCachedDeepStorage.TickRateQuotient}");

            if (Find.TickManager.TicksGame / GenTicks.TickRareInterval % CompCachedDeepStorage.TickRateQuotient != 0) { return; }

            Utils.Mess(Utils.Dbf.Cache, $"Tick for {parent} at tick {Find.TickManager.TicksGame}");

            foreach (var cellStorage in CellStorages.Storages) { cellStorage.SelfCorrection(); }
        }
    #endregion
    }
}
