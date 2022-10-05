using RimWorld;
using UnityEngine;
using Verse;


namespace LWM.DeepStorage
{
    public class CompCachedDeepStorage : CompDeepStorage
    {
        private StorageSettings _storageSetting;

        private const int TickRateQuotient = GenTicks.TickLongInterval / GenTicks.TickRareInterval;

        public Building_Storage StorageBuilding { get; private set; }

        public StorageSettings StorageSettings
        {
            get
            {
                if (_storageSetting == null)
                {
                    _storageSetting = this.StorageBuilding.settings;
                }

                return _storageSetting;
            }
        }

        public CellStorageCollection CellStorages { get; private set; }

        public CompCachedDeepStorage() => this._cached = true;

    #region Overrides of CompDeepStorage

        /// <summary>
        /// Check if <paramref name="thing"/> can be placed at <paramref name="cell"/>
        /// </summary>
        /// <param name="thing"> Thing to check. </param>
        /// <param name="cell"> Target position. </param>
        /// <param name="map"> Map that holds <paramref name="thing"/>. </param>
        /// <returns> Returns <see langword="true"/> if there is room for <paramref name="thing"/> </returns>
        public override bool StackableAt(Thing thing, IntVec3 cell, Map map)
        {
            if (!this.CellStorages.TryGetCellStorage(cell, out var cellStorage))
            {
                return false;
            }

            return StackableAt(thing, map, cellStorage, thing.GetStatValue(StatDefOf.Mass));
        }

        public override bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
        {
            capacity = CapacityToStoreThingAt(thing, map, cell);
            return capacity > 0;
        }

        public override int CapacityToStoreThingAt(Thing thing, Map map, IntVec3 cell, bool calledFromPatch = false){
            if (!this.CellStorages.TryGetCellStorage(cell, out var cellStorage))
            {
                return 0;
            }

            var unitWeight = thing.GetStatValue(StatDefOf.Mass);
            if (!CanStore(thing, map))
            {
                return 0;
            }

            var emptyStack = this.MaxNumberStacks - cellStorage.Count;
            var spareSpaceOnNonFull = cellStorage.SpareSpaceOnNonFull(thing);
            var spareStacks = emptyStack * thing.def.stackLimit + spareSpaceOnNonFull;

            if (this._limitingTotalFactorForCell > 0)
            {
                var spareStacksByWeight = Mathf.FloorToInt((this._limitingTotalFactorForCell - cellStorage.TotalWeight) / unitWeight);
                var spareStacksByMinimum = thing.def.stackLimit * (this.MinNumberStacks - cellStorage.Count);

                return Mathf.Max(Mathf.Min(spareStacks, spareStacksByWeight), spareStacksByMinimum, 0);
            }

            return spareStacks;
        }

        public override void PostExposeData() {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
            {
                base.PostExposeData();
            }

            if (this.CellStorages == null)
            {
                this.CellStorages = new CellStorageCollection(this.parent as Building_Storage, this);
            }

            this.CellStorages.ExposeData();
        }

        public override void Initialize(CompProperties props) {
            base.Initialize(props);

            this.StorageBuilding = (Building_Storage)this.parent;
        }

        #endregion

        private bool StackableAt(Thing thing, Map map, DeepStorageCellStorageModel cellStorage, float unitWeight) {

            if (!CanStore(thing, map))
            {
                return false;
            }

            return cellStorage.CanAccept(thing, unitWeight);
        }

        private bool CanStore(Thing thing, Map map) {
            if (map != this.StorageBuilding.Map)
            {
                return false;
            }

            if (!thing.def.EverStorable(false) || !this.StorageSettings.AllowedToAccept(thing))
            {
                return false;
            }

            // Jewelry box can't store a rocket launcher.
            if (this._limitingFactorForItem > 0f)
            {
                if (thing.GetStatValue(this._stat) > this._limitingFactorForItem)
                {
                    Utils.Warn(Utils.Dbf.CheckCapacity, "  Cannot store because " + _stat + " of "
                                                          + thing.GetStatValue(_stat) + " > limit of " + _limitingFactorForItem);
                    return false;
                }
            }

            return true;
        }

        #region Overrides of ThingComp

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Log.Message($"Initialize cached DS unit");
                this.CellStorages = new CellStorageCollection(this.parent as Building_Storage, this);
            }

            Utils.Mess(Utils.Dbf.Cache, $"TickerType: {this.parent.def.tickerType}");
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            this.CellStorages.Clear();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            this.CellStorages.Clear();
        }

        /// <summary>
        /// CompTickRare is only called when Remainder, which is equal to Find.TickManager.TicksGame % GenTicks.TicksRareInterval,
        /// equals to the index of list to which this comp is added. Given TicksRareInterval = 250 and TicksLongInterval = 2000,
        /// _tickRateQuotient = 2000 / 250 = 8. Whenever this method is called, TicksGame = 250 * Multiplier + Remainder(position in the tick list).
        /// Therefore, SelfCorrection will only be invoked when Multiplier is a multiple of _tickRateQuotient.
        /// </summary>
        public override void CompTickRare() {
            Utils.Mess(Utils.Dbf.Cache,
                $"Quotient: {Find.TickManager.TicksGame / GenTicks.TickRareInterval}, _tickQuotient: {CompCachedDeepStorage.TickRateQuotient}");

            if (Find.TickManager.TicksGame / GenTicks.TickRareInterval % CompCachedDeepStorage.TickRateQuotient != 0)
            {
                return;
            }

            Utils.Mess(Utils.Dbf.Cache, $"Tick for {this.parent} at tick {Find.TickManager.TicksGame}");
            foreach (var cellStorage in this.CellStorages.Storages)
            {
                cellStorage.SelfCorrection();
            }
        }

        #endregion
    }
}
