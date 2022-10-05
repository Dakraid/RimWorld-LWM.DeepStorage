namespace DeepStorage.Cache
{
#region
    using HarmonyLib;

    using RimWorld;

    using Verse;
#endregion

    /// <summary>
    ///     Handles the split off and stack merge of spawned things on map.
    /// </summary>
    [HarmonyPatch(typeof(ListerMergeables), nameof(ListerMergeables.Notify_ThingStackChanged))]
    public class PatchListerMergeablesNotifyThingStackChanged
    {
        public static void Postfix(Thing t)
        {
            if (t.GetSlotGroup() is SlotGroup slotGroup && slotGroup.IsCachedDeepStorage(out var comp)) { comp.CellStorages.Update(t); }
        }
    }
}
