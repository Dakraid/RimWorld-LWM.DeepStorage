namespace DeepStorage.Cache
{
#region
    using RimWorld;

    using System.Diagnostics;
    using System.Text;

    using Verse;
#endregion

    public static class CacheUtils
    {
        public static bool IsCachedDeepStorage(this SlotGroup slotGroup, out CompCachedDeepStorage comp)
        {
            if (slotGroup?.parent is Building_Storage building)
            {
                comp = building.TryGetComp<CompCachedDeepStorage>();

                return comp != null;
            }

            comp = null;

            return false;
        }

        [Conditional("DEBUG")]
        public static void PrintStates(this DeepStorageCellStorageModel cellStorage)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Storage at {cellStorage._cell} has:");
            stringBuilder.AppendLine($"Stack: {cellStorage.Count}");
            stringBuilder.AppendLine($"TotalWeight: {cellStorage.TotalWeight}");
            stringBuilder.AppendLine("NonFullThings:");

            foreach (var nonFullThing in cellStorage.NonFullThings) { stringBuilder.AppendLine($"{nonFullThing.Value}: {nonFullThing.Value.stackCount}"); }

            Log.Warning($"{stringBuilder}");
        }
    }
}
