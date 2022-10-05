using HarmonyLib;
using RimWorld;
using Verse;

namespace LWM.DeepStorage
{
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Notify_LostThing))]
    public class PatchNotifyLostThing
    {
        public static void Postfix(Building_Storage instance, Thing newItem)
        {
            if (instance.TryGetComp<CompCachedDeepStorage>() is CompCachedDeepStorage comp)
            {
                Utils.Mess(Utils.Dbf.Cache, $"Removing {newItem.LabelCap} from {instance.LabelCapNoCount}");
                comp.CellStorages.Remove(newItem);
            }
        }
    }
}
