using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Verse;

namespace LWM.DeepStorage
{
    /// <summary>
    /// The basic unit of any storage. All input used by methods in this class should be sanitized by
    /// its holding class. Checks to make:
    /// 1. stackCount on thing.
    /// 2. Whether thing and the storage on the same map.
    /// 3. If a thing is accepted by the Storage setting.
    /// </summary>
    public class DeepStorageCellStorageModel : ICollection<Thing>, IExposable
    {
        private CompCachedDeepStorage _comp;

        private int _minNumberStacks;

        private int _maxNumberStacks;

        private float _limitintTotalFactorForCell;

        private List<Thing> _serializationList = new List<Thing>();

        /// <summary>
        /// Indicates if the Add method is re-entry because of a previous call to Add.
        /// </summary>
        private bool _addReEntry;

        public IntVec3 _cell;

        public DeepStorageCellStorageModel() {
        }

        public DeepStorageCellStorageModel(IntVec3 cell, CompCachedDeepStorage comp) => this.Init(cell, comp);

        public Dictionary<ThingDef, Dictionary<Thing, float>> ThingCache { get; private set; }
            = new Dictionary<ThingDef, Dictionary<Thing, float>>();

        /// <summary>
        /// Note: It needs to always keep the non-full things on top of the stack,
        /// because it determines the amount of items to haul in some hauling jobs.
        /// </summary>
        public Dictionary<Thing, Thing> NonFullThings { get; } =
            new Dictionary<Thing, Thing>(StackableThingComparer._instance);

        public float TotalWeight { get; private set; } = 0;

        /// <summary>
        /// Gets the number of stacks on this cell.
        /// </summary>
        public int Count { get; private set; } = 0;

        public bool IsReadOnly => false;

        public void Init(IntVec3 cell, CompCachedDeepStorage comp) {
            this._cell = cell;
            _comp = comp;
            _minNumberStacks = _comp.MinNumberStacks;
            _maxNumberStacks = _comp.MaxNumberStacks;
            _limitintTotalFactorForCell = _comp._limitingTotalFactorForCell;
        }

        public void Add(Thing item) {
            var unitWeight = GetUnitWeight(item);
            Add(item, unitWeight);
        }

        public void Add(Thing item, float unitWeight) {
            var itemWeight = unitWeight * item.stackCount;
            TotalWeight += itemWeight;

            AddToNonFull(item);

            // Could be despawn when it is processed by AddToNonFull().
            if (!item.Spawned)
            {
                return;
            }

            AddToThingCache(item, unitWeight);
            this.Count++;
        }

        public bool TryAdd(Thing item) {
            var unitWeight = item.GetStatValue(StatDefOf.Mass);
            if (_addReEntry || CanAccept(item, unitWeight))
            {
                Add(item, unitWeight);
                _addReEntry = false;
                return true;
            }

            return false;
        }

        public void Clear() {
            ThingCache.Clear();
            NonFullThings.Clear();
            TotalWeight = 0;
            Count = 0;
        }

        public bool Contains(Thing item) {
            if (item is null)
            {
                return false;
            }

            return this.ThingCache.TryGetValue(item.def, out var value)
                   && value.ContainsKey(item);
        }

        public void CopyTo(Thing[] array, int arrayIndex) =>
            ThingCache.Values
                      .SelectMany(pair => pair.Keys)
                      .ToList()
                      .CopyTo(array, arrayIndex);

        public IEnumerator<Thing> GetEnumerator() =>
            ThingCache.Values
                      .SelectMany(pair => pair.Keys)
                      .GetEnumerator();

        public bool Remove(Thing item) {
            if (item is null)
            {
                return false;
            }

            if (!ThingCache.TryGetValue(item.def, out var things))
            {
                return false;
            }

            if (!things.TryGetValue(item, out var weight))
            {
                return false;
            }

            things.Remove(item);
            this.TotalWeight -= weight;
            this.Count--;

            RemoveFromNonFull(item);

            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Update items in storage.
        /// </summary>
        /// <param name="item"> The updated item. </param>
        /// <summary>
        ///     There could be many reason why the stackCount of an item changes in storage,
        /// e.g., decay, partially taken out for construction of projects. Therefore, it is necessary
        /// to update the NonFullThings here.
        /// </summary>
        public void Update(Thing item) {
            // Do not update things that are not already in storage.
            if (!this.ThingCache.TryGetValue(item.def, out var things))
            {
                return;
            }

            if (!things.ContainsKey(item))
            {
                return;
            }

            var newWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            var oldWeight = things[item];
            var deltaWeight = newWeight - oldWeight;

            TotalWeight += deltaWeight;
            things[item] = newWeight;

            if (this.NonFullThings.TryGetValue(item, out var nonFullThing))
            {
                if (item.stackCount == item.def.stackLimit)
                {
                    if (nonFullThing == item) {
                        NonFullThings.Remove(item);
                    }
                }
                else
                {
                    // Possible states:
                    // 1. Item is the same as the one stored in NonFullThings, no action required.
                    // 2. Item is not the same as the one in NonFullThings.

                    // State 2
                    if (nonFullThing != item)
                    {
                        // TryAbsorbStack() will trigger a re-entry to this method for nonFullThing.
                        // The other branches in this method will take care of the re-entry.
                        if (!nonFullThing.TryAbsorbStack(item, true))
                        {
                            NonFullThings[item] = item;
                        }
                    }
                }
            }
            else
            {
                if (item.stackCount != item.def.stackLimit)
                {
                    this.NonFullThings[item] = item;
                }
            }
        }

        public int SpareSpaceOnNonFull(Thing thing) {
            if (NonFullThings.TryGetValue(thing, out var value))
            {
                return thing.def.stackLimit - value.stackCount;
            }

            return 0;
        }

        public bool StackableOnNonFull(Thing thing) => SpareSpaceOnNonFull(thing) >= thing.stackCount;

        public bool CanAccept(Thing thing, float unitWeight) {
            var thingWeight = thing.stackCount * unitWeight;
            var thingStacks = Mathf.CeilToInt((float) thing.stackCount / thing.def.stackLimit);
            var stacksStoredHere = this.Count;

            var gTMinStack = stacksStoredHere + thingStacks > _minNumberStacks;
            var gTMaxStack = stacksStoredHere + thingStacks > _maxNumberStacks;
            var gTCellFactor = _limitintTotalFactorForCell > 0f &&
                               (thingWeight + this.TotalWeight - _limitintTotalFactorForCell) > 0.01f;

            if (!gTMinStack || (!gTCellFactor && !gTMaxStack))
            {
                return true;
            }

            return StackableOnNonFull(thing);
        }

        #region Implementation of IExposable

        public void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _serializationList = ThingCache.Values.SelectMany(d => d.Keys).ToList();
            }

            Scribe_Collections.Look(ref _serializationList, false, nameof(_serializationList), LookMode.Reference);
            Scribe_Values.Look(ref _cell, nameof(DeepStorageCellStorageModel._cell));

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PostLoadInit(_serializationList);
            }
        }

        #endregion


        private static float GetUnitWeight(Thing thing) => thing.GetStatValue(StatDefOf.Mass);

        private void PostLoadInit(List<Thing> things) {
            this.ThingCache = new Dictionary<ThingDef, Dictionary<Thing, float>>();
            foreach (var thing in things)
            {
                if (thing.stackCount != thing.def.stackLimit)
                {
                    // Trying to find the item in NonFullThings.
                    if (!this.NonFullThings.TryGetValue(thing, out var nonFullThing))
                    {
                        // If not present in the cache, add it.
                        this.NonFullThings[thing] = thing;
                    }
                    else
                    {
                        try
                        {
                            if (!nonFullThing.TryAbsorbStack(thing, true))
                            {
                                // If nonFullThing can't absorb thing entirely, which suggests nonFulling reaches its stackLimit,
                                // assign thing to NonFullThings cache.
                                this.NonFullThings[thing] = thing;
                            }
                            else
                            {
                                // After merging, if it reaches stackLimit, remove it from cache.
                                if (nonFullThing.stackCount == thing.def.stackLimit)
                                {
                                    this.NonFullThings.Remove(nonFullThing);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Warning($"Naughty item {thing} refuses to play with a little white mouse. Please report this warning" +
                                        $"to LWM.\n{e}");
                        }
                    }
                }

                // thing got absorbed.
                if (thing.Destroyed)
                {
                    continue;
                }

                float weight;
                if (this.ThingCache.TryGetValue(thing.def, out var value))
                {
                    weight = value[thing] = thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
                }
                else
                {
                    var newCache = new Dictionary<Thing, float>()
                    {
                        [thing] = weight = thing.GetStatValue(StatDefOf.Mass) * thing.stackCount,
                    };

                    this.ThingCache[thing.def] = newCache;
                }


                this.TotalWeight += weight;
                this.Count++;
            }
        }

        /// <summary>
        /// Remove item from NonFull cache.
        /// </summary>
        /// <param name="item"> Item to remove. </param>
        /// <remarks> If the add and update operation runs correctly, there will always be only one or zero non-full stack per kind. </remarks>
        private void RemoveFromNonFull(Thing item) {
            if (NonFullThings.TryGetValue(item, out var nonFullThings)
                && nonFullThings == item) {
                NonFullThings.Remove(item);
            }
        }

        /// <summary>
        /// Add <paramref name="item"/> to the NonFull cache if qualified.
        /// </summary>
        /// <param name="item"> Item to add. </param>
        private void AddToNonFull(Thing item) {
            // Possible states:
            // 1. Item stack count equals to stack limit.
            // 2. Item not in cache.
            // 3. Item in cache.

            var def = item.def;
            var defStackLimit = def.stackLimit;

            // State 1
            if (item.stackCount == defStackLimit)
            {
                return;
            }

            // State 2
            if (!NonFullThings.TryGetValue(item, out var value))
            {
                NonFullThings[item] = item;
                return;
            }

            // State 3
            // Update() method will take care of the change related to value.
            // Note: Steps are taken to clean up the spawn setup for item if TryAbsorbStack() returns true,
            // because item is DeSpawned when in the process of spawning.
            // Detail can check in the Harmony patch to GenSpawn.Spawn() and Thing.SpawnSetup().
            if (!value.TryAbsorbStack(item, true))
            {
                this.NonFullThings[item] = item;
            }
        }

        private void AddToThingCache(Thing thing, float unitWeight) {
            if (ThingCache.TryGetValue(thing.def, out var things))
            {
                things[thing] = thing.stackCount * unitWeight;
            }
            else
            {
                var newCache = new Dictionary<Thing, float>
                {
                    [thing] = thing.stackCount * unitWeight
                };

                ThingCache[thing.def] = newCache;
            }
        }

        public void SelfCorrection() {
            var outOfSync = false;

            // A hash set that keeps track of non-full things.
            var nonFullThings = new HashSet<Thing>(StackableThingComparer._instance);

            // Loop through cache.
            foreach (var defThings in this.ThingCache) {
                var def = defThings.Key;
                var sameThings = defThings.Value;

                // Check sub-category
                foreach (var thing in sameThings.Select(thingWeight => thingWeight.Key)) {
                    if (thing.stackCount == def.stackLimit)
                    {
                        continue;
                    }

                    if (nonFullThings.Contains(thing)) {
                        // There are two things whose stackCount is not equal to stackLimit
                        outOfSync = true;
                        Utils.Warn(Utils.Dbf.Cache,
                            $"{_comp.parent} is out of sync. The culprits are {thing} x {thing.stackCount} and" +
                            $"{(nonFullThings.TryGetValue(thing, out var value) ? value : null)} x {value.stackCount}");
                        break;
                    }

                    nonFullThings.Add(thing);
                }

                if (outOfSync) {
                    break;
                }
            }

            if (!outOfSync)
            {
                return;
            }

            this.TotalWeight = this.Count = 0;
            this.NonFullThings.Clear();

            foreach (var pair in this.ThingCache) {
                var sameThings = pair.Value;
                foreach (var thing in sameThings.Keys.ToList())
                {
                    this.TotalWeight += sameThings[thing] = thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
                    this.Count++;
                    this.AddToNonFull(thing);
                }
            }

            this.PrintStates();
        }
    }
}
