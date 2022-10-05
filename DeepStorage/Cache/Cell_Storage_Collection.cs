namespace DeepStorage.Cache
{
#region
    using RimWorld;

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using Verse;
#endregion

    public class CellStorageCollection : ICollection<Thing>, IExposable
    {
        private readonly Dictionary<IntVec3, DeepStorageCellStorageModel> _cacheCell = new Dictionary<IntVec3, DeepStorageCellStorageModel>();

        private readonly CompCachedDeepStorage _comp;

        private readonly Building_Storage _parent;
        private readonly HashSet<Thing> _thingsInStore = new HashSet<Thing>();

        private StorageSettings _settings;

        private List<DeepStorageCellStorageModel> _storageList;

        public CellStorageCollection() { }

        public CellStorageCollection(Building_Storage storage, CompCachedDeepStorage comp)
        {
            _comp     = comp;
            _parent   = storage;
            _settings = storage.settings;

            if (storage.Spawned) { Init(storage.AllSlotCellsList(), storage.Map); }
        }

        public List<DeepStorageCellStorageModel> Storages => _cacheCell.Values.ToList();

        public int Count => _thingsInStore.Count;

        public bool IsReadOnly => false;

        /// <summary>
        ///     Add <paramref name = "item" /> to cache.
        /// </summary>
        /// <param name = "item"> Thing to add. </param>
        /// <remarks>
        ///     It is called in <see cref = "PatchNotifyReceivedThing" />. <see cref = "Thing.stackCount" /> is sanitized in
        ///     <see cref = "GenSpawn.Spawn" />.
        ///     The number should not go above <see cref = "ThingDef.stackLimit" />. A bizarre situation is a drop pod or a
        ///     collapsed pawn drops right on
        ///     top of the storage, things spilled out are accepted by storage and taking up the last stack space which is reserved
        ///     by a hauling job.
        ///     When the pawn with the hauling job arrives, it will find the storage is full. This situation is handled by a
        ///     Harmony patch to TryPlaceDirect() and GenSpawn.Spawn().
        /// </remarks>
        public void Add(Thing item)
        {
            if (!_cacheCell.TryGetValue(item.Position, out var model)) { return; }

            if (!_settings.AllowedToAccept(item)) { return; }

            if (!_comp.StackableAt(item, item.Position, item.MapHeld)) { return; }

            model.Add(item);
            _thingsInStore.Add(item);
        }

        public void Clear()
        {
            _thingsInStore.Clear();
            _cacheCell.Clear();
        }

        public bool Contains(Thing item) => _thingsInStore.Contains(item);

        public void CopyTo(Thing[] array, int arrayIndex) => _thingsInStore.CopyTo(array, arrayIndex);

        public bool Remove(Thing item)
        {
            if (_thingsInStore.Remove(item) & (_cacheCell.TryGetValue(item.Position, out var model) && model.Remove(item))) { return true; }

            return false;
        }

        public IEnumerator<Thing> GetEnumerator() => _thingsInStore.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _thingsInStore.GetEnumerator();

    #region Implementation of IExposable
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) { _storageList = _cacheCell.Values.ToList(); }

            Scribe_Collections.Look(ref _storageList, nameof(CellStorageCollection._storageList), LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _settings = _parent.settings;

                foreach (var storage in _storageList)
                {
                    storage.Init(storage._cell, _comp);
                    _cacheCell[storage._cell] = storage;
                }
            }
        }
    #endregion

        public bool TryGetCellStorage(IntVec3 position, out DeepStorageCellStorageModel model) => _cacheCell.TryGetValue(position, out model);

        /// <summary>
        ///     It is invoked in the postfix of <see cref = "ListerMergeables.Notify_ThingStackChanged" />
        ///     iff <paramref name = "thing" /> is spawned.
        /// </summary>
        /// <param name = "thing"> Thing to update. </param>
        /// <remarks>
        ///     As of RW1.1, the only call site that meets the criteria to execute this method is TryPlaceDirect() or
        ///     Thing.SplitOff(),
        ///     which we have check capacity to limit the number of items that can be merged, and SplitOff is not of concern here.
        ///     Thus, the overweight possibilities of a storage should be nil.
        /// </remarks>
        public void Update(Thing thing)
        {
            if (_cacheCell.TryGetValue(thing.Position, out var model)) { model.Update(thing); }
        }

        public bool StackableOnNonFull(Thing thing) => _cacheCell.Values.Any(cellStorage => cellStorage.StackableOnNonFull(thing));

        public int SpareSpaceOnNonFull(Thing thing) => _cacheCell.Values.Sum(c => c.SpareSpaceOnNonFull(thing));

        /// <summary>
        ///     Absorb <paramref name = "thing" /> only with Non-full stack in this storage.
        /// </summary>
        /// <param name = "thing"> Thing to absorb by cell storage. </param>
        /// <param name = "loc"> location at which <paramref name = "thing" /> is about to store. </param>
        /// <param name = "placedAction"> Callback for when <paramref name = "thing" /> is fully absorbed or Spawned. </param>
        /// <param name = "resultingThing">
        ///     The thing that, partly of fully, consists of <paramref name = "thing" />.
        ///     Only when return value is <see langword = "true" /> will the <paramref name = "resultingThing" /> be non-null.
        /// </param>
        /// <returns> Returns <see langword = "true" /> only if there is no stack left to place in <paramref name = "thing" />. </returns>
        public bool AbsorbWithNonFull(Thing thing, IntVec3 loc, Action<Thing, int> placedAction, ref Thing resultingThing)
        {
            if (!_cacheCell.TryGetValue(loc, out var cellStorage)) { return false; }

            if (!cellStorage.NonFullThings.TryGetValue(thing, out var nonFullThing)) { return false; }

            var stackCount = thing.stackCount;

            if (nonFullThing.TryAbsorbStack(thing, true))
            {
                placedAction?.Invoke(nonFullThing, thing.stackCount);
                resultingThing = nonFullThing;

                return true;
            }

            if (stackCount != thing.stackCount) { placedAction?.Invoke(thing, stackCount - thing.stackCount); }

            return false;
        }

        private void Init(List<IntVec3> cells, Map map)
        {
            foreach (var cell in cells)
            {
                _cacheCell[cell] = new DeepStorageCellStorageModel(cell, _comp);

                foreach (var thing in cell.GetThingList(map))
                {
                    // There is a chance that when an item with size larger than one is used as input,
                    // and the item is not supposed to be stored to this building, a corpse clips a meat hook, e.g..
                    // Thus, ThingListAt() is not the appropriate method to use here.
                    if (thing.Position != cell) { continue; }

                    if (thing.def.EverStorable(false) && _settings.AllowedToAccept(thing)) { Add(thing); }
                }
            }
        }
    }
}
