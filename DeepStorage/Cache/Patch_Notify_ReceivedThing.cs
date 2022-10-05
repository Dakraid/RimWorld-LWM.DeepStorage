namespace DeepStorage.Cache
{
#region
    using HarmonyLib;

    using RimWorld;

    using Verse;
#endregion

    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_ReceivedThing))]
    public class PatchNotifyReceivedThing
    {
        public static void Postfix(Building_Storage instance, Thing newItem)
        {
            if (instance.TryGetComp<CompCachedDeepStorage>() is CompCachedDeepStorage comp)
            {
                Utils.Mess(Utils.Dbf.Cache, $"Place {newItem.LabelCap} in {instance.LabelCapNoCount}");
                comp.CellStorages.Add(newItem);
            }
        }
    }
}
