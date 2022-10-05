﻿

// for OpCodes in Harmony Transpiler

namespace DeepStorage
{
#region
    using HarmonyLib;

    using Verse;
#endregion

    //***********************************************************//
    //                    Saving and Loading
    //
    // Saving:
    //   Problem: stone chunks and slag are saved "compressed," 'cause
    //   they are all basically the same.  But...if there are more than
    //   one in a cell, then they shouldn't be compressed.
    //
    //   We cannot simply check for a DSU - there might just be a giant
    //   pile on the ground (maybe DSU got moved/destroyed).  (Alternate
    //   option: make sure anything compressible explodes all over on
    //   DSU despawning.)
    //
    //
    // Loading:
    //   Problem: when loading, the game will try to load all the things
    //    in the Deep Storage unit and then will realize it cannot place
    //    them because there is already something there (see Spawn() in
    //    Deep_Storage_PutInto.cs)  So things could end up exploded all
    //    over the place.  Not pretty.
    //   Why?  Because items get loaded AFTER buildings.  Well, crap.
    //   Bonus problem:  Maybe there's a stack of objects on the ground
    //    because a DSU got destroyed; we want to keep those intact, too.
    //
    //   Solution:
    //    Spawn() does know if it's respawningAfterLoad.  When we
    //    patch it in Deep_Storage_PutInto.cs, we use that as a
    //    check to avoid the problem.  No additional patching
    //    required here.
    //
    //**********************************************************//
    [HarmonyPatch(typeof(CompressibilityDeciderUtility), "IsSaveCompressible")]
    internal class PatchIsSaveCompressible
    {
        private static void Postfix(ref bool result, Thing t)
        {
            if (result == false) { return; }

            if (!t.Spawned) { return; }
            var ots   = t.Map.thingGrid.ThingsListAt(t.Position);
            var items = 0;

            foreach (var ot in ots)
            {
                if (ot.def.EverHaulable) { items++; }
            }

            if (items > 1) { result = false; }
        }
    }
}
