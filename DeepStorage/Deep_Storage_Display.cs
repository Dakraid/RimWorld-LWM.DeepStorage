using System;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using UnityEngine; // because graphics.


namespace LWM.DeepStorage
{
    /*********************************************
     * Display   
     * 
     * Make giant piles of Deep Storage stuff look tider!
     * 
     * Multiple aspects:
     *
     * 1.  Make items invisisble if the DSU makes them invisible
     *     (patch Regenerate and modify adding items to DeepStorage
     *      to deregister drawing them)
     * 2.  Make it so the last item added to the Deep Storage,
     *     the item on top, shows on top.
     *     Why?
     *     Because the way Unity works, the very first mesh drawn
     *     is "over" later meshes drawn.  As far as I can tell,
     *     the meshes get drawn once, and then get put down
     *     multiple times - once for each object.  So the mesh for
     *     Simple meals will ALWAYS cover the mesh for Fine meals.
     *     Even if in a specific case, it is "Draw()n" later.
     *     Fix
     *     Create a HashSet (in Utils) to keep track of what
     *     items are on top (I think that was fastest).  When an
     *     item is added to DeepStorage, make sure the correct
     *     item is in the HashSet.  When an item gets removed,
     *     take it out of the HashSet and make sure the correct
     *     item IS in there.
     *     Patch Thing's DrawPos getter, so that when an item
     *     is on "top" of the DeepStorage pile, its draw height
     *     (altitude, "y", whatever) is sliiiightly higher than
     *     normal.  Viola!  It gets drawn on top.
     * 3.  Do a pretty - and useful - GUI overlay on DSUs, as
     *     appropriate.
     *     (on adding to DSU, turn off normal GUI overlay;
     *     make a gui overlay for the DSU)
     *
     * Note:  The way the game handles storage, we don't have to
     *     re-register items to be drawn, turn back on their GUI,
     *     or any of that.
     *     Why?
     *     The game *unspawns the old item* and then moves it to
     *     the pawn's inventory.  When the pawn puts it down?
     *     The item re-registers to be drawn on spawn.
     * Note2: When an item is added to DS, we put the DSU at the
     *     end of the thingsListAt.
     *     Vanilla behavior, pre-save:
     *       stuff-in-storage (at end of list)
     *       Shelf
     *     Vanilla behavior, post-save:
     *       Shelf (at end of list)
     *       stuff-in-storage
     *     It is better to ensure a DSU is at the end of the
     *     list than risk DSU being in the middle of the list
     *     after a save and reload.
     * 
     *********************************************/

    /**********************************
     * SectionLayer_Things's Regnerate()
     *
     * Graphics with MapMeshOnly (for example, stone chunks, slag, ...?)
     * are drawn by the Things SectionLayer itself.  We patch the Regenerate
     * function so that any items in a DSU are invisible.
     *
     * Specifically, we use Harmony Transpiler to change the thingGrid.ThingsListAt 
     * to our own list, that doesn't include any things in storage.
     *
     * TODO?  Only do the transpilation if there are actually any DSUs that
     * store MapMeshOnly items?
     */

    [HarmonyPatch(typeof(SectionLayer_Things), "Regenerate")]
    public static class PatchDisplaySectionLayerThingsRegenerate {
        // We change thingGrid.ThingsListAt(c) to DisplayThingList(map, c):
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var flag=false;
            var code = new List<CodeInstruction>(instructions);
            var i = 0;
            var lookingForThisFieldCall = HarmonyLib.AccessTools.Field(typeof(Verse.Map), "thingGrid");
            for (;i<code.Count;i++) {
                if (code[i].opcode != OpCodes.Ldfld || 
                    (System.Reflection.FieldInfo)code[i].operand != lookingForThisFieldCall) {
                    yield return code[i];
                    continue;
                }
                flag=true;
                // found middle of List<Thing> list = base.Map.thingGrid.ThingsListAt(c);
                // We are at the original instruction .thingGrid
                //   and we have the Map on the stack
                i++;  // go past thingGrid instruction
                // Need the location c on the stack, but that's what happens next in original code - loading c
                yield return code[i];
                i++; // now past c
                // Next code instruction is to call ThingsListAt.
                i++; // We want our own list
                yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools.Method(
                                                     "LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:ThingListToDisplay"));
                break; // that's all we need to change!
            }
            for (;i<code.Count; i++) {
                yield return code[i];
            }
            if (!flag)
            {
                Log.Error("LWM Deep Storage: Haromony Patch for SectionLayer display failed.  This is display-only, the game is still playable.");
            }

            yield break;
        }

        static public List<Thing> ThingListToDisplay(Map map, IntVec3 loc) {
            CompDeepStorage cds;
            ThingWithComps building;
            var slotGroup=loc.GetSlotGroup(map);

            if (slotGroup==null || (building=(slotGroup.parent as ThingWithComps))==null ||
                (cds=(slotGroup.parent as ThingWithComps).TryGetComp<CompDeepStorage>())==null||
                cds.ShowContents)
            {
                return map.thingGrid.ThingsListAt(loc);
            }
            // only return non-storable things to be drawn:
            return map.thingGrid.ThingsListAt(loc).FindAll(t=>!t.def.EverStorable(false));
        }
    } // end Patch SectionLayer_Things Regenerate()

    /*****************************************
     * Making non-mesh things invisible:
     * 
     * We add checks to: Notify_ReceivedThing and to SpawnSetup,
     * and remove items from various lists:
     *   Drawable list
     *   tooltip Giver List
     *   GUI Overlay list
     * (These settings gathered from DeSpawn() and from other modders')
     *
     * Note that we don't have to do much to make them visible again!
     * When an item leaves Deep Storage, it deSpawns, and an identical one
     * is created.
     * If the DSU itself despawns, however, we need to make sure everything
     * is visible!
     */

    /* NOTE: an item can be added to Deep Storage in two ways:
     *  2.  Something puts it there (Notify_ReceivedThing called)
     *  1.  The game loads.
     *  Both need to be addressed.
     */
    
    // Make non-mesh things invisible when loaded in Deep Storage
    // Making item on top display on top: loaded items on "top" need to go into the HashSet
    // Gui Overlay: loaded items' overlays should not display
    // Put DeepStorage at the end of the ThingsList for proper display post-save
    [HarmonyPatch(typeof(Building_Storage), "SpawnSetup")]
    public static class PatchDisplaySpawnSetup {
        public static void Postfix(Building_Storage instance, Map map) {
            CompDeepStorage cds;
            if ((cds = instance.GetComp<CompDeepStorage>()) == null)
            {
                return;
            }

            foreach (var cell in instance.AllSlotCells()) {
                var list = map.thingGrid.ThingsListAt(cell);
                var alreadyFoundItemOnTop=false;
                for (var i=list.Count-1; i>=0; i--) {
                    var thing=list[i];
                    if (!thing.Spawned || !thing.def.EverStorable(false))
                    {
                        continue; // don't make people walking past be invisible...
                    }

                    if (cds.CdsProps._overlayType != GuiOverlayType.Normal || !cds.ShowContents) {
                        // Remove gui overlay - this includes number of stackabe item, quality, etc
                        map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(thing);
                    }
                    if (thing.def.drawerType != DrawerType.MapMeshOnly) {
                        if (!alreadyFoundItemOnTop) {
                            Utils._topThingInDeepStorage.Add(thing);
                        }
                        if (!cds.ShowContents) {
                            map.dynamicDrawManager.DeRegisterDrawable(thing);
                        }
                    }
                    alreadyFoundItemOnTop=true;  // it's true now, one way or another!

                    if (!cds.ShowContents) {
                        map.tooltipGiverList.Notify_ThingDespawned(thing); // should this go with guioverlays?
                    }
                    // Don't need to thing.DirtyMapMesh(map); because of course it's dirty on spawn setup ;p
                } // end cell
                // Now put the DSU at the top of the ThingsList here:
                list.Remove(instance);
                list.Add(instance);
            }
        }
    }
    
    // Make non-mesh things invisible: they have to be de-registered on being added to a DSU
    // Making item on top display on top: added items need to go into the HashSet
    // Gui Overlay: added items' overlays should not display
    // Put DeepStorage at the end of the ThingsList for consistant display:
    [HarmonyPatch(typeof(Building_Storage),"Notify_ReceivedThing")]
    public static class PatchDisplayNotifyReceivedThing {
        public static void Postfix(Building_Storage instance,Thing newItem) {
            CompDeepStorage cds;
            if ((cds = instance.TryGetComp<CompDeepStorage>()) == null)
            {
                return;
            }

            /****************** Put DSU at top of list *******************/
            /*  See note 2 at top of file re: display                    */
            var list = newItem.Map.thingGrid.ThingsListAt(newItem.Position);
            list.Remove(instance);
            list.Add(instance);

            /****************** Set display for items correctly *******************/
            /*** Clean up old "what was on top" ***/
            foreach (var t in list) {
                Utils._topThingInDeepStorage.Remove(t);
            }
            
            /*** Complex meshes have a few rules for DeepStorage ***/
            if (newItem.def.drawerType != DrawerType.MapMeshOnly) {
                //  If they are on top, they should be drawn on top:
                if (cds.ShowContents)
                {
                    Utils._topThingInDeepStorage.Add(newItem);
                }
                else // If we are not showing contents, don't draw them:
                {
                    instance.Map.dynamicDrawManager.DeRegisterDrawable(newItem);
                }
            }

            /*** Gui overlay - remove if the DSU draws it, or if the item is invisible ***/
            if (cds.CdsProps._overlayType != GuiOverlayType.Normal || !cds.ShowContents) {
                // Remove gui overlay - this includes number of stackabe item, quality, etc
                instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Remove(newItem);
            }

            if (!cds.ShowContents)
            {
                return; // anything after is for invisible items
            }

            /*** tool tip, dirt mesh, etc ***/
            instance.Map.tooltipGiverList.Notify_ThingDespawned(newItem); // should this go with guioverlays?
            newItem.DirtyMapMesh(newItem.Map); // for items with a map mesh; probably unnecessary?
            // Note: not removing linker here b/c I don't think it's applicable to DS?
        }
    }

    /*** Removing an item from DeepStorage necessitates re-calculating which item is "on top" ***/
    
    // Son. Of. A. Biscuit.  This does not work:
    //   Notify_LostThing is an empty declaration, and it seems to be optimized out of existance,
    //   so Harmony cannot attach to it.  The game crashes - with no warning - when the patched
    //   method gets called.
    #if false
    [HarmonyPatch(typeof(RimWorld.Building_Storage), "Notify_LostThing")]
    public static class PatchDisplay_Notify_LostThing {
        static void Postfix(){
            return; // crashes instantly when Notify_LostThing is called ;_;
        }

        static void Postfix_I_Would_Like_To_Use(Building_Storage __instance, Thing newItem) {
            Utils.TopThingInDeepStorage.Remove(newItem);
            if (__instance.TryGetComp<CompDeepStorage>() == null) return;
            List<Thing> list = newItem.Map.thingGrid.ThingsListAt(newItem.Position);
            for (int i=list.Count-1; i>0; i--) {
                if (!list[i].def.EverStorable(false)) continue;
                Utils.TopThingInDeepStorage.Add(list[i]);
                return;
            }
        }
    }
    #endif

    /* Item Removed from Deep Storage: reprise: */
    /* We have to make a general check in DeSpawn to see if it was in DeepStorage before it disappears 
     * If so, make sure display is correct */
    [HarmonyPatch(typeof(Verse.Thing), "DeSpawn")]
    internal static class CleanupForDeepStorageThingAtDeSpawn {
        private static void Prefix(Thing instance) {
            // I wish I could just do this:
            // Utils.TopThingInDeepStorage.Remove(__instance);
            // But, because I cannot patch Notify_LostThing, I have to do its work here:  >:/
            if (!Utils._topThingInDeepStorage.Remove(instance))
            {
                return;
            }

            if (instance.Position == IntVec3.Invalid)
            {
                return; // ???
            }
            // So it was at one point in Deep Storage.  Is it still?
            CompDeepStorage cds;
            if ((cds=((instance.Position.GetSlotGroup(instance.Map)?.parent) as ThingWithComps)?.
                 TryGetComp<CompDeepStorage>())==null)
            {
                return;
            }

            // Figure out what is on top now:
            if (!cds.ShowContents)
            {
                return;
            }
            var list = instance.Map.thingGrid.ThingsListAtFast(instance.Position);
            for (var i=list.Count-1; i>=0; i--) {
                if (!list[i].def.EverStorable(false))
                {
                    continue;
                }

                if (list[i] == instance)
                {
                    continue;
                }
                Utils._topThingInDeepStorage.Add(list[i]);
                return;
            }
        }
    }

    /*************** Deep Storage DeSpawns (destroyed, minified, etc) *****************/
    [HarmonyPatch(typeof(Verse.Building), "DeSpawn")]
    public static class PatchBuildingDeSpawnForBuildingStorage {
        [HarmonyPriority(Priority.First)] // MUST execute, cannot be postfix,
                                          // as some elements already null (e.g., map)
        public static void Prefix(Building instance) {
            CompDeepStorage cds;
            if ((cds = instance.GetComp<CompDeepStorage>())==null)
            {
                return;
            }
            var map=instance.Map;
            if (map == null) {
                Log.Error("DeepStorage despawning: Map is null; some assets may not display properly: "
                          +instance.ToString()); return;
            }
            ISlotGroupParent dsu = instance as Building_Storage;
            foreach (var cell in dsu.AllSlotCells()) {
                var list = map.thingGrid.ThingsListAt(cell);
                Thing t;
                for (var i=0; i<list.Count;i++) {
                    t=list[i];
                    Utils._topThingInDeepStorage.Remove(t); // just take them all, to be safe
                    if (t==null) { Log.Warning("DeepStorage despawning: tried to clean up null object"); continue;}
                    if (!t.Spawned || !t.def.EverStorable(false))
                    {
                        continue;
                    }

                    if (t.def.drawerType != DrawerType.MapMeshOnly)
                    {   // should be safe to register even if already registered
                        map.dynamicDrawManager.RegisterDrawable(t);
                    }
                    // from the ListerThings code:
                    if (ThingRequestGroup.HasGUIOverlay.Includes(t.def)) {
                        if (!map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Contains(t)) {
                            map.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay).Add(t);
                        }
                    }
                    // Just to make sure it's not in the tooltip list twice:
                    //    Is this ineffecient?  Yes.
                    //    It also means that if anything changes whether cds.showContents, nothing breaks
                    //    Also, this only happens rarely, so ineffecient is okay.
                    map.tooltipGiverList.Notify_ThingDespawned(t);
                    map.tooltipGiverList.Notify_ThingSpawned(t);
                } // end list of things at                
            } // end foreach cell of despawning DSU
        }  //end postfix
    } // end patch for when DSU despawns

    /* The workhouse solving #2 (from top of file)
     * The magic to make what-is-on-top get displayed "above" everything else: */
    /* (thank you DuckDuckGo for providing this approach, and thak you to everyone
     *  who helped people who had similar which-mesh-is-on-top problems)
     */
    [HarmonyPatch(typeof(Verse.Thing),"get_DrawPos")]
    internal static class EnsureTopItemInDsuDrawsCorrectly {
        private static void Postfix(Thing instance, ref Vector3 result) {
            if (Utils._topThingInDeepStorage.Contains(instance)) {
                result.y+=0.05f; // The default "altitudes" are around .45 apart, so .05 should be about right.
                                   //             "altitudes" here are "terrain," "buildings," etc.
            }
        }
    }

    
    /**************** GUI Overlay *****************/
    [HarmonyPatch(typeof(Thing),"DrawGUIOverlay")]
    internal static class AddDsuGUIOverlay {
        private static bool Prefix(Thing instance) {
            if (Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest)
            {
                return true; // maybe someone changes this? Who knows.
            }
            var dsu = instance as Building_Storage;
            if (dsu == null)
            {
                return true;
            }
            var cds = dsu.GetComp<CompDeepStorage>();
            if (cds == null)
            {
                return true;
            }

            if (cds.CdsProps._overlayType == LWM.DeepStorage.GuiOverlayType.Normal)
            {
                return true;
            }

            if (cds.CdsProps._overlayType == GuiOverlayType.None)
            {
                return false;
            }

            List<Thing> things;
            String s;
            if (cds.CdsProps._overlayType == GuiOverlayType.CountOfAllStacks) {
                // maybe Armor Racks, Clothing Racks, def Weapon Lockers etc...
                things = new List<Thing>();
                foreach (var c in dsu.AllSlotCellsList()) {
                    things.AddRange(instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false)));
                }

                if (things.Count ==0) {
                    if (cds.CdsProps._showContents)
                    {
                        return false; // If it's empty, player will see!
                    }
                    s="LWM_DS_Empty".Translate();
                } else if (things.Count ==1)
                {
                    s = 1.ToStringCached(); // Why not s="1";?  You never know, someone may be playing in...
                }
                else if (AllSameType(things))
                {
                    s = "x"+things.Count.ToStringCached();
                }
                else
                {
                    s = "[ "+things.Count.ToStringCached()+" ]";
                }
                GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(instance,0f),s,GenMapUI.DefaultThingLabelColor);
                return false;
            }

            if (cds.CdsProps._overlayType == GuiOverlayType.CountOfStacksPerCell) {
                // maybe Armor Racks, Clothing Racks?
                foreach (var c in dsu.AllSlotCellsList()) {
                    things=instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false));
                    if (things.Count ==0) {
                        if (cds.CdsProps._showContents)
                        {
                            continue; // if it's empty, player will see!
                        }
                        s="LWM_DS_Empty".Translate();
                    } else if (things.Count ==1)
                    {
                        s = 1.ToStringCached(); // ..a language that doesn't use arabic numerals?
                    }
                    else if (AllSameType(things))
                    {
                        s = "x"+things.Count.ToStringCached();
                    }
                    else
                    {
                        s = "[ "+things.Count.ToStringCached()+" ]";
                    }
                    var l2=GenMapUI.LabelDrawPosFor(c);
//                    l2.x+=cds.x;
//                    l2.y+=cds.y;
                    l2.y+=10f;
                    GenMapUI.DrawThingLabel(l2,s,GenMapUI.DefaultThingLabelColor);
                }
                return false;
            }
            if (cds.CdsProps._overlayType == GuiOverlayType.SumOfAllItems) {
                // probably food baskets, skips, etc...
                things=new List<Thing>();
                foreach (var c in dsu.slotGroup.CellsList){
                    things.AddRange(instance.Map.thingGrid.ThingsListAtFast(c)
                                    .FindAll(t=>t.def.EverStorable(false)));
                }

                if (things.Count ==0) {
                    if (cds.CdsProps._showContents)
                    {
                        return false; // if it's empty, player will see
                    }
                    s="LWM_DS_Empty".Translate();
                } else {
                    var count=things[0].stackCount;
                    var allTheSame=true;
                    for (var i=1; i<things.Count; i++) {
                        if (things[i].def != things[0].def)
                        {
                            allTheSame = false;
                        }
                        count+=things[i].stackCount;
                    }
                    if (allTheSame)
                    {
                        s = count.ToStringCached();
                    }
                    else
                    {
                        s = "[ "+count.ToStringCached()+" ]";
                    }
                }
                GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(instance,0f),s,GenMapUI.DefaultThingLabelColor);
                return false;
            }
            if (cds.CdsProps._overlayType == GuiOverlayType.SumOfItemsPerCell) {
                // Big Shelves
                var anyItems=false;
                foreach (var c in dsu.AllSlotCellsList()) {
                    var itemsWithStackSizeOne=false;
                    things=instance.Map.thingGrid.ThingsListAtFast(c).FindAll(t=>t.def.EverStorable(false));
                    if (things.Count > 0) {
                        anyItems=true;
                        var count=0;
                        for (var i=0; i<things.Count; i++) {
                            // Break logic if there is anything with a stackLimit of 1
                            //   show instead the count of stacks:
                            if (itemsWithStackSizeOne || things[i].def.stackLimit==1) {
                                itemsWithStackSizeOne=true;
                                if (things.Count ==1)
                                {
                                    s = 1.ToStringCached(); // ..a language that doesn't use arabic numerals?
                                }
                                else if (AllSameType(things))
                                {
                                    s = "x"+things.Count.ToStringCached();
                                }
                                else
                                {
                                    s = "[ "+things.Count.ToStringCached()+" ]";
                                }
                                var l=GenMapUI.LabelDrawPosFor(c);
                                l.y+=10f;
                                GenMapUI.DrawThingLabel(l,s,GenMapUI.DefaultThingLabelColor);
                                goto WhyDoesCSharpNotHaveBreakTwo;
                            } else {
                                count+=things[i].stackCount;
                            }
                        } // end list of things.
                        if (AllSameType(things))
                        {
                            s = count.ToStringCached();
                        }
                        else
                        {
                            s = "[ "+count.ToStringCached()+" ]";
                        }
                        var l2=GenMapUI.LabelDrawPosFor(c);
                        l2.y+=10f;
                        GenMapUI.DrawThingLabel(l2,s,GenMapUI.DefaultThingLabelColor);
                    } // if count > 0
                    WhyDoesCSharpNotHaveBreakTwo:;
                } // foreach cell
                if (!anyItems && !cds.CdsProps._showContents) { // there are no items, but no way to see that.
                    s="LWM_DS_Empty".Translate();
                    GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(instance,0f),s,GenMapUI.DefaultThingLabelColor);
                }
                return false;
            }
            Log.Warning("LWM DeepStorage: could not find GuiOverlayType of "+cds.CdsProps._overlayType);
            return true;
        }
        private static bool AllSameType(List<Thing> l) {
            if (l.Count < 2)
            {
                return true;
            }

            for (var i=1; i<l.Count;i++) {
                if (l[i].def != l[0].def)
                {
                    return false;
                }
            }
            return true;
        }
    }


}
