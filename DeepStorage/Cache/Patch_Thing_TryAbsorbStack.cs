namespace DeepStorage.Cache
{
#region
    using HarmonyLib;

    using Verse;
#endregion

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public class PatchThingTryAbsorbStack
    {
        // Originally, TryAbsorbStack() doesn't call the notify method for "other".
        // This fixes an issue when a non-full thing in cell storage tries to absorb another thing
        // in storage when Update() is called on the cell storage.
        public static void Postfix(Thing other)
        {
            if (other.Spawned) { other.Map.listerMergeables.Notify_ThingStackChanged(other); }
        }
    }
}
