using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit; // for OpCodes in Harmony Transpiler
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

using System.Linq;

using static LWM.DeepStorage.Utils.Dbf; // trace utils

namespace LWM.DeepStorage
{
    /*
      Desired sequence of events:
      User right-clicks with pawn selected
      When AddHumanlikeOrders is run,
      // and when AddUndraftedOrders->AddJobGiverWorkOrders is run, // not doing this now
      //    It tended to cause crashing etc and didn't gain much?
        Prefix runs
        Prefix sets flag

        Move All Items Away
        (Get basic default orders?)
        For Each Thing
          Move Thing Back
          Call AHlO/AJGWO - only calling AddHumanlikeOrders right now?
          flag is set so runs normally
          Move Thing Away
        Move Things Back
        Combine menu
        return false
      Postfix runs and catches logic, puts together complete, correct menu option list
      So...look, we do the same thing twice!  Function calls!
    */
    [HarmonyPatch(typeof(RimWorld.FloatMenuMakerMap), "AddHumanlikeOrders")]
    internal static class PatchAddHumanlikeOrders {
        static public bool _runVanillaAHlO=false;                         // should Vanilla(+mods) AddHumanlikeOrders run
        static public bool _blockAHlONonItems=false;                      // if it does run, should TargetsAt() be blocked
        private static List<FloatMenuOption> _listOfOptionsWeFilled=null; // out master list; see below

        // Thing => where it was originally from before it was stolen away to faerie:
        //   Note: this will only contain weird things...like Pawns walkig past.  or
        //   maybe fire.  I'm not really sure how the fire thing works.  Clearly the
        //   thing to do is to test it on a big shelf of artillery shells.
        static public Dictionary<Thing, IntVec3> _thingsInRealmOfFaerie=new Dictionary<Thing,IntVec3>();

        //// Utility dynamic functions to access private fields quickly:
        // set a Thing's positionInt directly (fast):
        static public Action<Thing,IntVec3> _setPosition; // created in Prepare
        // call FloatMenuMakerMap's private AddHumanlikeOrders (fast):
        static public Action<Vector3,Pawn,List<FloatMenuOption>> _aHlO; // created in Prepare
        // get the ThingGrid's actual thingGrid for direct manipulation
        //   (reflection, so slightly slower, but only run once, then cached)
        private static readonly FieldInfo _thingGridThingList = HarmonyLib.AccessTools.Field(typeof(ThingGrid), "thingGrid");
        private static List<Thing>[] _cachedThingGridThingList=null;
        private static Map _mapOfCachedThingGrid=null;
        // List<Thing> that is kept empty but used with thingGrid's ThingListAt for building menu:
        private static List<Thing> _tmpEmpty=new List<Thing>();

        // Use this to prepare some "dynamic functions" we will use
        //   (for faster performance, b/c apparently the reflection
        //   is sloooooow.  From what i hear.)
        private static bool Prepare(Harmony instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null)
            {
                return false;
            }

            /* Build a dynamic method to do:
             * void RePosition(Thing t, IntVec3 pos) {
             *   t.positionInt=pos;  // directly set internal private field
             * }
             *
             * Use this approach for speed.
             */
            var dm = new DynamicMethod("directly set thing's positionInt",
                                        null, // return type void
                                        new Type[] {typeof(Thing), typeof(IntVec3)},
                                        true // skip JIT visibility checks - which is whole point
                                             //   we want to access a private field!
                );
            var il=dm.GetILGenerator();
            // build our function from IL.  Because why not
            il.Emit(OpCodes.Ldarg_0);//put Thing on stack
            il.Emit(OpCodes.Ldarg_1);//put position on stack
            // store field:
            il.Emit(OpCodes.Stfld, typeof(Thing).GetField("positionInt",
                                                          BindingFlags.Instance |
                                                          BindingFlags.GetField |
                                                          BindingFlags.SetField |
                                                          BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret); //don't forget
            // Create the delegate that completes the dynamic method:
            //   (I'm just quoting the MSIL documentation, I don't
            //    actually know what I'm doing)
            PatchAddHumanlikeOrders._setPosition=(Action<Thing,IntVec3>)dm.CreateDelegate(typeof(Action<,>).MakeGenericType(typeof(Thing), typeof(IntVec3)));
            /*****     Now do the same for AHlO - call the private method directly     *****/
            dm=new DynamicMethod("directly call AddHumanlikeOrders",
                                 null, // return type void
                                 new Type[] {typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>)},
                                 true // skip JIT visibility checks
                );
            il=dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                                                       BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            PatchAddHumanlikeOrders._aHlO=(Action<Vector3,Pawn,List<FloatMenuOption>>)dm
                .CreateDelegate(typeof(Action<,,>).MakeGenericType(typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>)));

            return true;
            Utils.Warn(RightClickMenu, "Loading AddHumanlikeOrders menu code: "
                       +Settings._useDeepStorageRightClickLogic);




            return Settings._useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
            if (!Settings._useDeepStorageRightClickLogic)
            {
                return true;
            }
            //                return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);

            // if expicitly told to run vanilla, run vanilla.
            //   (note vanilla may have any number of mods attached)
            if (PatchAddHumanlikeOrders._runVanillaAHlO) {
                Utils.Warn(RightClickMenu, "-------Running Vanilla AddHumanlikeOrders"+pawn+"-------");
                return true;
            }
            // if not in storage, don't worry about it:
            var clickCell=IntVec3.FromVector3(clickPos);
            if (!Utils.GetDeepStorageOnCell(clickCell, pawn.Map, out _)) {
                Utils.Warn(RightClickMenu, "-----Running Vanilla AddHumanlikeOrders"+pawn+" - not in storage-----");
                return true;
            }
            Utils.Warn(RightClickMenu, "-----Running Custom AddHumanlikeOrders for "+pawn+"-----");
            // We will fill listOfOptionsWeFilled - this lets us properly handle other mods'
            //   right click modifications (we will use listOfOptionsWeFilled in our postfix
            //   to provide the actual result). In the meantime, we use listOfOptions as our
            //   list - we will move everything over at the end.
            // Why?  Because we have to move over our final result in the Postfix
            var listOfOptions=new List<FloatMenuOption>();
            // Prepare Faerie to accept pawns/etc.
            PatchAddHumanlikeOrders._thingsInRealmOfFaerie.Clear();

            // get menu with no items.  This will include commands such as "clean dirt."  I think.
            // and dealing with fire.  I think.  And pawns.  Definitely dealing with pawns.

            /*var index = pawn.Map.cellIndices.CellToIndex(cpos.ToIntVec3());
var listArray = (List<Thing>[]) thingListTG.GetValue(pawn.Map.thingGrid);
var origList = listArray[index];

listArray[index] = new List<Thing> {thingList[i]};
rows[i] = new DSGUI_ListItem(pawn, thingList[i], cpos, boxHeight);
listArray[index] = origList;
*/
            //   Clear thingsList
            if (PatchAddHumanlikeOrders._mapOfCachedThingGrid!=pawn.Map) {
                PatchAddHumanlikeOrders._mapOfCachedThingGrid                              = pawn.Map;
                PatchAddHumanlikeOrders._cachedThingGridThingList = (List<Thing>[]) PatchAddHumanlikeOrders._thingGridThingList.GetValue(pawn.Map.thingGrid);
            }
            var index=pawn.Map.cellIndices.CellToIndex(clickCell);
            var origList=PatchAddHumanlikeOrders._cachedThingGridThingList[index];
            PatchAddHumanlikeOrders._cachedThingGridThingList[index]=PatchAddHumanlikeOrders._tmpEmpty;

            PatchAddHumanlikeOrders._runVanillaAHlO=true;
            Utils.Mess(RightClickMenu, "Get menu with no items: invoke vanilla:");
//            List<Thing> thingList=clickCell.GetThingList(pawn.Map);
//            List<Thing> tmpList=new List<Thing>(thingList);
//            thingList.Clear();
            PatchAddHumanlikeOrders._aHlO(clickPos,pawn,listOfOptions);
            /* var origParams=new object[] {clickPos, pawn, listOfOptions};
            AHlO.Invoke(null, origParams);*/
            PatchAddHumanlikeOrders._runVanillaAHlO=false;
            // return things
            PatchAddHumanlikeOrders._cachedThingGridThingList[index]=origList;
            Utils.Mess(RightClickMenu, "  ->"+String.Join("; ", listOfOptions));
//            thingList.AddRange(tmpList);
//            ReturnThingsFromFaerie();
            // get sorted list of all things in dsu
            var allThings=clickCell.GetSlotGroup(pawn.Map).HeldThings.OrderBy(t=>t.LabelCap).ToList();
            // Multilpe Options here, depending on size of allThings.
            //   6 or fewer => should just display each set of options
            //   7-12       => show entry for each, with options
            //   LOTS       => open f***ing window w/ search options, etc.
            // for each thing(mod label) in storage, add menu entry
            // ...for now, just print them all:
            for (var i=0; i<allThings.Count; i++) {
                var t=allThings[i];
/*                int howManyOfThis=1;
                while (i<allThings.Count-1 && t.Label==allThings[i+1].Label) {
                    howManyOfThis++;
                    i++;
                }
                string label;
                if (howManyOfThis==1)
                    label=t.Label;
                else
                    label=howManyOfThis.ToString()+" x "+t.Label;//TODO: translate
                //TODO: what if no option for this item????
                */
                GeneratePawnOptionsFor(pawn, t, listOfOptions);
//                listOfOptions.Add(MakeMenuOptionFor(pawn, t, howManyOfThis));
            }
            PatchAddHumanlikeOrders._listOfOptionsWeFilled=listOfOptions;
            #if DEBUG
            if (Utils._showDebug[(int)RightClickMenu])
            {
                for (var ijk=0; ijk<1000; ijk++)
                {
                    Log.Message("pausing log...");
                }
            }
        #endif
            return false;
            //return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);
        }
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void Postfix(List<FloatMenuOption> opts) {
            if (!Settings._useDeepStorageRightClickLogic) {
                PatchFloatMenuMakerMap.Postfix(opts);
                return;
            }
            if (PatchAddHumanlikeOrders._listOfOptionsWeFilled!=null) {
                opts.Clear();
                opts.AddRange(PatchAddHumanlikeOrders._listOfOptionsWeFilled);
                PatchAddHumanlikeOrders._listOfOptionsWeFilled=null;
            }
            return;
        }

        private static FloatMenuOption MakeMenuOptionFor(Pawn p, Thing t, int count) {
            // Phase one: simple menu of menus
            Utils.Mess(RightClickMenu, "  Creating Menu Option for "+count+" "+t);
            var label=(count>1?count.ToString()+" x "+t.Label:t.Label);
            return new FloatMenuOption(label, delegate() {
                    #if DEBUG
                    if (Utils._showDebug[(int)RightClickMenu])
                    {
                        Log.ResetMessageCount();
                    }
                #endif
                    Utils.Warn(RightClickMenu, "Opening menu for "+p+" using "+t);
                    var menu=GetFloatMenuFor(p,t);
                    if (menu!=null)
                    {
                        Find.WindowStack.Add(menu);
                    }
                #if DEBUG
                    if (Utils._showDebug[(int)RightClickMenu])
                    {
                        for (var ijk=0; ijk<1000; ijk++)
                        {
                            Log.Message("pausing log");
                        }
                    }
                #endif
                },
                t.def);
        }

        private static FloatMenu GetFloatMenuFor(Pawn p, Thing t) {
            var fmo=new List<FloatMenuOption>();
            GeneratePawnOptionsFor(p, t, new List<FloatMenuOption>());
            return new FloatMenu(fmo);
        }
//        public static List<FloatMenuOption> floatingList=new List<FloatMenuOption>();
private static void GeneratePawnOptionsFor(Pawn p, Thing t, List<FloatMenuOption> opts) {
            Utils.Mess(RightClickMenu, "Generating Options for "+p+" using "+t);
//            if (!t.Spawned) return null;
            if (!t.Spawned)
            {
                return;
            }
            // if not cached, build
            var map=p.Map;
            /*** Step 1: Clear ThingsList ***/
            var index=map.cellIndices.CellToIndex(t.Position);
            if (PatchAddHumanlikeOrders._mapOfCachedThingGrid!=map) {
                PatchAddHumanlikeOrders._mapOfCachedThingGrid                              = map;
                PatchAddHumanlikeOrders._cachedThingGridThingList = (List<Thing>[]) PatchAddHumanlikeOrders._thingGridThingList.GetValue(map.thingGrid);
            }
            var origList=PatchAddHumanlikeOrders._cachedThingGridThingList[index];
            PatchAddHumanlikeOrders._cachedThingGridThingList[index]=PatchAddHumanlikeOrders._tmpEmpty;
            // Add the thing we are interested in to the new thinglist:
            PatchAddHumanlikeOrders._tmpEmpty.Add(t); // (remember to empty afterwards)

/*            IntVec3 c=t.Position;
            var list=t.Map.thingGrid.ThingsListAtFast(c);
            foreach (Thing bye in list) {
                if (bye==t) continue;
                SendToFaerie(bye);
            }
            // get rid of non-item things (e.g., pawns?)
            */

            //XXX:
            // We are doing UI stuff, which often uses the click-position
            //    instead of the map position, so that's annoying and may
            //    be risky, but I'm not sure there's much choice. We also
            //    grab everything via TargetingParameters, which includes
            //    all the Things on the ground as well as pawns, fire, &c
            var clickPos=t.Position.ToVector3();
            var peverything=new TargetingParameters();
            peverything.canTargetBuildings=true; // be thorough here
            peverything.canTargetItems=false; // We already moved the thingsList
            peverything.canTargetFires=true; // not that pawns SHOULD be able to grab things from burning lockers...
            peverything.canTargetPawns=true;
            peverything.canTargetSelf=true; // are we sure?

            foreach (LocalTargetInfo anotherT in GenUI.ThingsUnderMouse(clickPos,
                                                                        0.8f /*see TargetsAt*/, peverything)) {
                var tmpT=anotherT.Thing;
                if (tmpT!=null && tmpT!=t) {
//                    Utils.Mess(RightClickMenu, "  moving away "+tmpT);
//                    SendToFaerie(tmpT);
                }
            }
            /*var index = pawn.Map.cellIndices.CellToIndex(cpos.ToIntVec3());
var listArray = (List<Thing>[]) thingListTG.GetValue(pawn.Map.thingGrid);
var origList = listArray[index];

listArray[index] = new List<Thing> {thingList[i]};
rows[i] = new DSGUI_ListItem(pawn, thingList[i], cpos, boxHeight);
listArray[index] = origList;
*/
            // get orders for Thing t!
//            List<Thing> thingList=t.Position.GetThingList(t.Map);
//            List<Thing> tmpList=new List<Thing>(thingList);
//            thingList.Clear();
//            thingList.Add(t);
            PatchAddHumanlikeOrders._runVanillaAHlO    = true;
            PatchAddHumanlikeOrders._blockAHlONonItems = true;
//            floatingList.Clear();
            Utils.Mess(RightClickMenu, "  running vanilla (but target-blocked) AHlO");
            PatchAddHumanlikeOrders._aHlO(clickPos,p,opts);
            PatchAddHumanlikeOrders._blockAHlONonItems=false;
//            AHlO(clickPos,p,floatingList);
//            AHlO.Invoke(null, new object[] { clickPos, p, floatingList}); //todo
            PatchAddHumanlikeOrders._runVanillaAHlO=false;
            // Restore Things:
            Utils.Mess(RightClickMenu, "  returning things");
            ReturnThingsFromFaerie();
            // Restor ThingList:
            PatchAddHumanlikeOrders._cachedThingGridThingList[index]=origList;
            PatchAddHumanlikeOrders._tmpEmpty.Clear(); // clean up after ourselves!
//            thingList.Clear();
//            thingList.AddRange(tmpList);
//      Log.Warning("List size: "+floatingList.Count);
//            return floatingList;
//            Log.Warning("List size: "+opts.Count);
        }
        [HarmonyPriority(Priority.Last)]
        public static void PostfixXxx(List<FloatMenuOption> opts) => PatchFloatMenuMakerMap.Postfix(opts);

        public static void SendToFaerie(Thing t) {
            PatchAddHumanlikeOrders._thingsInRealmOfFaerie[t]=t.Position;
            PatchAddHumanlikeOrders._setPosition(t, IntVec3.Invalid);
        }
        public static void ReturnThingsFromFaerie() {
            foreach (var kvp in PatchAddHumanlikeOrders._thingsInRealmOfFaerie) {
                //kvp.Key.Position=kvp.Value;
                PatchAddHumanlikeOrders._setPosition(kvp.Key, kvp.Value);
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var genUITargetsAt = HarmonyLib.AccessTools.Method(typeof(Verse.GenUI), "TargetsAt");
            var ourTargetsAt = HarmonyLib.AccessTools.Method(typeof(PatchAddHumanlikeOrders), "OurTargetsAt");
            foreach (var c in instructions) {
                if (c.opcode==OpCodes.Call && (MethodInfo)c.operand == genUITargetsAt)
                {
                    yield return new CodeInstruction(OpCodes.Call, ourTargetsAt);
                }
                else
                {
                    yield return c;
                }
            }
        }
        public static IEnumerable<LocalTargetInfo> OurTargetsAt(Vector3 clickPos,
                                                                TargetingParameters clickParams, 
                                                                bool thingsOnly = false,
                                                                ITargetingSource source = null) {
            if (PatchAddHumanlikeOrders._blockAHlONonItems)
            {
                return Enumerable.Empty<LocalTargetInfo>(); //yield break;
            }

            return GenUI.TargetsAt(clickPos, clickParams, thingsOnly, source);
        }
        // Allow directly setting Position of things.  And setting it back.
/*        public static void SetPosition(Thing t, IntVec3 p) {
            fieldPosition.SetValue(t, p);
        }*/
        /****************** Black Magic ***************/
        // Allow calling AddHumanlikeOrders
        private static MethodInfo _aHlOOld = typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);
        // Allow calling AddJobGiverWorkOrders
        static public MethodInfo _ajgwo = typeof(FloatMenuMakerMap).GetMethod("AddJobGiverWorkOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);

        // Allow directly setting Position of things.  And setting it back.
        private static FieldInfo _fieldPosition=typeof(Thing).GetField("positionInt",
                                                              BindingFlags.Instance |
                                                              BindingFlags.GetField |
                                                              BindingFlags.SetField |
                                                              BindingFlags.NonPublic);


    }
    #if false
    static class Patch_AddHumanlikeOrders_Orig {
        static bool Prepare(Harmony instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
            Utils.Warn(RightClickMenu, "Loading AddHumanlikeOrders menu code: "
                       +Settings.useDeepStorageRightClickLogic);
            return Settings.useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
            return Patch_FloatMenuMakerMap.Prefix(clickPos,IntVec3.Invalid,pawn,opts,false,false);
        }
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FloatMenuOption> opts) {
            Patch_FloatMenuMakerMap.Postfix(opts);
        }
    }
    #endif
    #if false
//    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders")]
    static class Patch_AddJobGiverWorkOrders {
        static bool Prepare(HarmonyInstance instance) {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null) return false;
            Utils.Warn(RightClickMenu, "Loading AddJobGiverWorkOrders menu code: "
                       +Settings.useDeepStorageRightClickLogic);
            return Settings.useDeepStorageRightClickLogic;
        }
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(IntVec3 clickCell, Pawn pawn, List<FloatMenuOption> opts, bool drafted) {
            try {
                Log.Message("About to try....");
//                Patch_FloatMenuMakerMap.AJGWO.Invoke(null, new object[] {clickCell, pawn, opts, drafted});
            }
            catch (Exception e) {
                Log.Error("well, THAT failed: "+e);
                return true;
            }

            return true;
            return false;
            return Patch_FloatMenuMakerMap.Prefix(Vector3.zero,clickCell,pawn,opts,true,drafted);
        }
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FloatMenuOption> opts) {
            Log.Message("Skipping...");
            return;
            Patch_FloatMenuMakerMap.Postfix(opts);
        }
    }
    #endif
        internal static class PatchFloatMenuMakerMap {
            private static bool _runningPatchLogic=false;
            private static List<FloatMenuOption> _realList=new List<FloatMenuOption>();
            private static int _failsafe=0;
            private static Vector3 _clickPos;
        public static bool Prepare() {
            if (ModLister.GetActiveModWithIdentifier("netrve.dsgui")!=null)
            {
                return false;
            }

            return true;
        }

        // We have to run as Prefix, because we need to intercept the incoming List.
        public static bool Prefix(Vector3 clickPosition, IntVec3 c, Pawn pawn, List<FloatMenuOption> opts,
                                  bool runningAjgwo, bool drafted /*only if runningAJGWO*/) {
            Utils.Mess(RightClickMenu,""+(runningAjgwo?"AddJobGiverWorkOrders":"AddHumanlikeOrders")+
                       " called.  Currently "
                       +(PatchFloatMenuMakerMap._runningPatchLogic?"":" not ")+"running special Patch Logic");
            if (runningAjgwo)
            {
                return true;
            }
            if (PatchFloatMenuMakerMap._failsafe++>500)
            {
                PatchFloatMenuMakerMap._runningPatchLogic = false;
            }

            if (PatchFloatMenuMakerMap._runningPatchLogic)
            {
                return true;
            }

            // Only give nice tidy menu if items are actually in Deep Storage: otherwise, they
            //   are a jumbled mess on the floor, and pawns can only interact with what's on
            //   top until they've cleaned up the mess.
            // I *could* do better and throw away all items below, but whatev's this is good enuf.
            if (!runningAjgwo) {
                PatchFloatMenuMakerMap._clickPos=clickPosition;
                c = IntVec3.FromVector3(PatchFloatMenuMakerMap._clickPos);
            }
            if (((c.GetSlotGroup(pawn.Map)?.parent)as ThingWithComps)?.AllComps
                .FirstOrDefault(x=>x is IHoldMultipleThings.IHoldMultipleThings)==null) {
                Utils.Warn(RightClickMenu, "Location "+c+" is not in any DSU; continuing.");
                return true; // out of luck, so sorry!
                // Note: also need to handle this case in Postfix!
            }
            PatchFloatMenuMakerMap._failsafe=0;

            Utils.Err(RightClickMenu, "Testing Location "+c);

            PatchFloatMenuMakerMap._runningPatchLogic=true;

            // TODO: get default set of menus and tidy them away somehow?  This seems to be unnecessary so far.
            /************* Move all things away **************/
            // ThingsListAt:
            var workingThingList=c.GetThingList(pawn.Map);
            var origThingList=new List<Thing>(workingThingList);
            workingThingList.Clear();
            // ...other ...things.
            var origPositions=new Dictionary<Thing, IntVec3>();
            var peverything=new TargetingParameters();
            peverything.canTargetBuildings=false; //already got it
            peverything.canTargetItems=false; //already got it
            peverything.canTargetFires=true; //??
            peverything.canTargetPawns=true;
            peverything.canTargetSelf=true;
            foreach (var localTargetInfo in GenUI.TargetsAt(PatchFloatMenuMakerMap._clickPos, peverything)) {
                if (localTargetInfo.Thing==null) {
                    Log.Warning("LWM.DeepStorage: got null target but should only have things?");
                    continue;
                }
                origPositions.Add(localTargetInfo.Thing, localTargetInfo.Thing.Position);
                Utils.Warn(RightClickMenu, "Adding position information for LocalTargetInfo "
                           +localTargetInfo.Thing);
                SetPosition(localTargetInfo.Thing, IntVec3.Invalid);
            }
            /*****************  Do magic ****************/
            object[] origParams;
            if (runningAjgwo) {
                origParams=new object[] { c, pawn, opts, drafted };
            } else {
                origParams=new object[] { PatchFloatMenuMakerMap._clickPos, pawn, opts };
            }
            foreach (var k in origPositions) {
                SetPosition(k.Key, k.Value);
                Utils.Mess(RightClickMenu, "  Doing Menu for Target "+k.Key);
                if (runningAjgwo)
                {
                    PatchFloatMenuMakerMap._ajgwo.Invoke(null, origParams);
                }
                else
                {
                    PatchFloatMenuMakerMap._aHlO.Invoke(null, origParams);
                }
                //showOpts(opts);
                SetPosition(k.Key, IntVec3.Invalid);
            }
            foreach (var t in origThingList) {
                workingThingList.Add(t);
                Utils.Mess(RightClickMenu, "  Doing Menu for Item "+t);
                PatchFloatMenuMakerMap._aHlO.Invoke(null, origParams);
                //showOpts(opts);
                workingThingList.Remove(t);
            }

            /************ Cleanup: Put everything back! ***********/
            workingThingList.Clear();
            foreach (var t in origThingList) {
                workingThingList.Add(t);
            }
            foreach (var t in origPositions) {
                SetPosition(t.Key, t.Value);
            }
            PatchFloatMenuMakerMap._runningPatchLogic=false;
            PatchFloatMenuMakerMap._realList.Clear();
            foreach (var m in opts) {
                PatchFloatMenuMakerMap._realList.Add(m); // got to store it in case anything adjusts it in a different Postfix
            }
            return false;
        } // end Prefix
        public static void Postfix(List<FloatMenuOption> opts) {
            if (PatchFloatMenuMakerMap._runningPatchLogic)
            {
                return;
            }

            if (PatchFloatMenuMakerMap._realList.Count == 0)
            {
                return; // incidentally breaks out of logic here in case not in a DSU
            }
            opts.Clear();
            foreach (var m in PatchFloatMenuMakerMap._realList)
            {
                opts.Add(m);
            }
            PatchFloatMenuMakerMap._realList.Clear();
            Utils.Mess(RightClickMenu, "Final Menu:\n    "+String.Join("\n    ",
                                                   opts.Select(o=>o.Label).ToArray()));
            //showOpts(opts);
        }

        /******************* Utility Functions *******************/

        /*private static void showOpts(List<FloatMenuOption> opts) { //Unused
            System.Text.StringBuilder s=new System.Text.StringBuilder(500);
            foreach (var m in opts) {
                s.Append("     ");
                s.Append(m.Label);
                s.Append("\n");
            }
            Log.Message(s.ToString());
        }*/
        // Allow directly setting Position of things.  And setting it back.
        public static void SetPosition(Thing t, IntVec3 p) => PatchFloatMenuMakerMap._fieldPosition.SetValue(t, p);

        /****************** Black Magic ***************/
        // Allow calling AddHumanlikeOrders
        private static MethodInfo _aHlO = typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);
        // Allow calling AddJobGiverWorkOrders
        static public MethodInfo _ajgwo = typeof(FloatMenuMakerMap).GetMethod("AddJobGiverWorkOrders",
                                               BindingFlags.Static | BindingFlags.NonPublic);

        // Allow directly setting Position of things.  And setting it back.
        private static FieldInfo _fieldPosition=typeof(Thing).GetField("positionInt",
                                                              BindingFlags.Instance |
                                                              BindingFlags.GetField |
                                                              BindingFlags.SetField |
                                                              BindingFlags.NonPublic);
    } // end patch class
}
