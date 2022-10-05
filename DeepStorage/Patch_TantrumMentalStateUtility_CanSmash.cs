﻿

/*************
 * Limit damage a pawn can do while having a tantrum:
 *   Only allow pawn to check "top" item in Deep Storage
 *   Only allow any item half the time (because dealing
 *     with tightly packed storage is a pain)
 *   Mostly disallow any item in a "safe" storage unit
 *     (e.g., the Safe) - only 1/10 chance
 *************/
namespace DeepStorage
{
#region
    using HarmonyLib;

    using JetBrains.Annotations;

    using RimWorld;

    using Verse;
    using Verse.AI;
#endregion

    [UsedImplicitly]
    [HarmonyPatch(typeof(TantrumMentalStateUtility), "CanSmash")]
    internal static class PatchTantrumMentalStateUtilityCanSmash
    {
        [UsedImplicitly]
        [HarmonyPostfix]
        private static void AfterCanSmash(Pawn pawn, Thing thing, ref bool result)
        {
            if (result && thing.TryGetComp<CompDeepStorage>() == null)
            {
                // It's in deep storage
                if (thing.Position.GetSlotGroup(pawn.Map)?.parent is ThingWithComps thingWithComps
                    && // null is not TwC
                    thingWithComps.GetComp<CompDeepStorage>() is CompDeepStorage cds)
                {
                    // if it's not on top and easily accessible to angry colonist,
                    if (!Utils._topThingInDeepStorage.Contains(thing)
                        // (or only 50% chance of picking something in storage anyway),
                        || thing.thingIDNumber % 2 == 1)
                    {
                        // don't break it:
                        result = false;

                        return;
                    }

                    if (cds.CdsProps._isSecure && !(thing.thingIDNumber % 10 == 0))
                    {
                        // subject to review?
                        result = false;
                    }
                }
                // otherwise, it's a valid target to get smashed
            }
        }
    }
}
