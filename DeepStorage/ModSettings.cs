//for Path() in trace

namespace DeepStorage
{
#region
    using RimWorld;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using UnityEngine;

    using Verse;
#endregion

    public class Settings : ModSettings
    {
        // Architect Menu:
        // The defName for the DesignationCategoryDef the mod items are in by default:
        //TODO: make this a tutorial, provide link.
        // To use this code in another mod, change this const string, and then add to the
        // file ModName/Languages/English(etc)/Keyed/settings(or whatever).xml:
        // <[const string]_ArchitectMenuSettings>Location on Architect Menu:</...>
        // Copy and paste the rest of anything that says "Architect Menu"
        // Change the list of new mod items in the final place "Architect Menu" tells you to
        private const string ArchitectMenuDefaultDesigCatDef = "LWM_DS_Storage";
        private const float ScrollBarWidthMargin = 18f;
        public static bool _robotsCanUse;
        public static bool _storingTakesTime = true;
        public static float _storingGlobalScale = 1f;
        public static bool _storingTimeConsidersStackSize = true;
        public static StoragePriority _defaultStoragePriority = StoragePriority.Important;
        public static bool _useEjectButton = true; // I think users will want it, alho I will prolly not

        public static bool _useDeepStorageRightClickLogic;

        // Turning this off removes conflicts with some other storage mods (at least I hope so):
        //   (RimFactory? I think?)
        public static bool _checkOverCapacity = true;

        public static bool _allowPerDsuSettings;
        public static DefChangeTracker _defTracker = new DefChangeTracker();
        private static string _architectMenuDesigCatDef = Settings.ArchitectMenuDefaultDesigCatDef;
        private static bool _architectMenuAlwaysShowCategory;

        private static bool _architectMenuMoveAllStorageItems = true;

        //   For later use if def is removed from menu...so we can put it back:
        private static DesignationCategoryDef _architectMenuActualDef;
        private static bool _architectMenuAlwaysShowTmp;
        private static bool _architectMenuMoveAllTmp = true;

        //TODO-scroll: can I make these non-static? Probably, but there's no point, right?
        //             Either way, there will be memory allocated for them :p
        private static Vector2 _scrollPosition = new Vector2(0f, 0f);
        private static float _totalContentHeight = 1000f;

        public static IEnumerable<ThingDef> AllDeepStorageUnits
        {
            get
            {
                var x = Settings.LoadedDeepStorageUnits;

                if (x == null) { Log.Error("Loaded is null"); }

                foreach (var d in x) { yield return d; }

                foreach (var d in Settings._defTracker.GetAllWithKeylet<ThingDef>("def")) { yield return d; }
            }
        }

        public static IEnumerable<ThingDef> LoadedDeepStorageUnits
        {
            get
            {
                var db = DefDatabase<ThingDef>.AllDefsListForReading;

                if (db == null)
                {
                    Log.Error("DefDatabase is nul");

                    yield break;
                }

                foreach (var d in db)
                {
                    if (d.HasComp(typeof(CompDeepStorage))) { yield return d; }
                }
            }
        }

        // NOTE: They removed Listing_Standard's scroll views in 1.3 :p
        //private static Rect viewRect=new Rect(0,0,100f,10000f); // OMG OMG OMG I got scrollView in Listing_Standard to work!
        public static void DoSettingsWindowContents(Rect inRect)
        {
            ModMetaData tmpMod;
            var         origColor = GUI.color; // make option gray if ignored
            var         outerRect = inRect.ContractedBy(10f);
            Widgets.DrawHighlight(outerRect);

            // We put a scrollbar around a listing_standard; it seems to work okay
            var scrollBarVisible = Settings._totalContentHeight > outerRect.height;
            var scrollViewTotal  = new Rect(0f, 0f, outerRect.width - (scrollBarVisible ? Settings.ScrollBarWidthMargin : 0), Settings._totalContentHeight);
            Widgets.BeginScrollView(outerRect, ref Settings._scrollPosition, scrollViewTotal);

            var l = new Listing_Standard(GameFont.Medium);           // my tiny high-resolution monitor :p
            l.Begin(new Rect(0f, 0f, scrollViewTotal.width, 9999f)); // Some RW window does this "9999f" thing, & it seems to work?
            //l.GapLine();  // Who can haul to Deep Storage (robots, animals, etc)
            l.Label("LWMDShaulToStorageExplanation".Translate());
            l.CheckboxLabeled("LWMDSrobotsCanUse".Translate(), ref Settings._robotsCanUse, "LWMDSrobotsCanUseDesc".Translate());

            string[] intLabels =
            {
                "LWM_DS_Int_Animal".Translate(), "LWM_DS_Int_ToolUser".Translate(), "LWM_DS_Int_Humanlike".Translate()
            };
            // Setting to allow bionic racoons to haul to Deep Storage:
            l.EnumRadioButton(ref PatchIsGoodStoreCell._necessaryIntelligenceToUseDeepStorage, "LWM_DS_IntTitle".Translate(), "LWM_DS_IntDesc".Translate(), false, intLabels);

            l.GapLine(); //Storing Delay Settings
            l.Label("LWMDSstoringDelaySettings".Translate());
            l.Label("LWMDSstoringDelayExplanation".Translate());
            l.CheckboxLabeled("LWMDSstoringTakesTimeLabel".Translate(), ref Settings._storingTakesTime, "LWMDSstoringTakesTimeDesc".Translate());
            l.Label("LWMDSstoringGlobalScale".Translate((Settings._storingGlobalScale * 100f).ToString("0.")));
            Settings._storingGlobalScale = l.Slider(Settings._storingGlobalScale, 0f, 2f);
            l.CheckboxLabeled("LWMDSstoringTimeConsidersStackSize".Translate(), ref Settings._storingTimeConsidersStackSize, "LWMDSstoringTimeConsidersStackSizeDesc".Translate());

            // Reset storing delay settings to defaults
            if (l.ButtonText("LWMDSstoringDelaySettings".Translate() + ": " + "ResetBinding".Translate() /*Reset to Default*/))
            {
                Settings._storingTakesTime              = true;
                Settings._storingGlobalScale            = 1f;
                Settings._storingTimeConsidersStackSize = true;
            }
            l.GapLine(); // default Storing Priority

            if (l.ButtonTextLabeled("LWM_DS_defaultStoragePriority".Translate(), Settings._defaultStoragePriority.Label()))
            {
                var mlist = new List<FloatMenuOption>();

                foreach (StoragePriority p in Enum.GetValues(typeof(StoragePriority)))
                {
                    mlist.Add(
                        new FloatMenuOption(
                            p.Label(), delegate
                            {
                                Settings._defaultStoragePriority = p;

                                foreach (var d in Settings.AllDeepStorageUnits) { d.building.defaultStorageSettings.Priority = p; }
                            }
                        )
                    );
                }
                Find.WindowStack.Add(new FloatMenu(mlist));
            }
            l.GapLine();
            l.Label("LWM_DS_userInterface".Translate());
            l.CheckboxLabeled("LWM_DS_useEjectButton".Translate(), ref Settings._useEjectButton, "LWM_DS_useEjectButtonDesc".Translate());

            //TODO::
            if ((tmpMod = ModLister.GetActiveModWithIdentifier("netrve.dsgui")) != null)
            {
                GUI.color = Color.gray;
                l.Label("LWMDSignoredDueTo".Translate(tmpMod.Name));
            }
            l.CheckboxLabeled("LWM_DS_useDSRightClick".Translate(), ref Settings._useDeepStorageRightClickLogic, "LWM_DS_useDSRightClickDesc".Translate());

            // Architect Menu:
            l.GapLine(); //Architect Menu location
            GUI.color = origColor;

            /*
            //            string archLabel=
            //            if (archLabel==n
            //            l.Label("LWMDSarchitectMenuSettings".Translate());
                        if (architectMenuDesigCatDef==architectMenuDefaultDesigCatDef) {
            //                if (architectLWM_DS_Storage_DesignationCatDef==null) {
            //                    Log.Error("LWM.DeepStorage: architectLWM_DS_Storage_DesignationCatDef was null; this should never happen.");
            //                    tmp="ERROR";
            //                } else {
            //                    tmp=architectCurrentDesignationCatDef.LabelCap; // todo: (default)
            //                }
                            archLabel+=" ("+
                        } else {
                            var x=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesignationCatDefDefName, false);
                            if (x==null) {
                                // TODO
                            }
                            tmp=x.LabelCap; // todo: (<menuname>)
                        }*/
            if (l.ButtonTextLabeled(
                    (Settings.ArchitectMenuDefaultDesigCatDef + "_ArchitectMenuSettings").Translate(), // Label
                    // value of dropdown button:
                    DefDatabase<DesignationCategoryDef>.GetNamed(Settings._architectMenuDesigCatDef)?.LabelCap ?? "--ERROR--"
                ))
            {
                // error display text
                //                                     , DefDatabase<DesigarchitectMenuDesigCatDef) ) {
                // Float menu for architect Menu choice:
                var alist = new List<FloatMenuOption>();
                var arl   = DefDatabase<DesignationCategoryDef>.AllDefsListForReading; //all reading list

                //oops:
                //                alist.Add(new FloatMenuOption(DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDefaultDesigCatDef).LabelCap
                alist.Add(
                    new FloatMenuOption(
                        Settings._architectMenuActualDef.LabelCap + " (" + "default".Translate() + " - " + Settings._architectMenuActualDef.defName + ")", delegate
                        {
                            Utils.Mess(Utils.Dbf.Settings, "Architect Menu placement set to default Storage");
                            Settings.ArchitectMenu_ChangeLocation(Settings.ArchitectMenuDefaultDesigCatDef);
                            //                                                  architectCurrentDesignationCatDef=architectLWM_DS_Storage_DesignationCatDef;
                            //                                                  architectMenuDesignationCatDefDefName="LWM_DS_Storage";
                            //
                            //                                                  SettingsChanged();
                        }
                    )
                );

                // Architect Menu:  You may remove the "Furniture" references here if you wish
                alist.Add(
                    new FloatMenuOption(
                        DefDatabase<DesignationCategoryDef>.GetNamed("Furniture").LabelCap + " (Furniture)", // I know what this one's defName is!
                        delegate
                        {
                            Utils.Mess(Utils.Dbf.Settings, "Architect Menu placement set to Furniture.");
                            Settings.ArchitectMenu_ChangeLocation("Furniture");
                        }
                    )
                );

                foreach (var adcd in arl)
                {
                    //architect designation cat def
                    if (adcd.defName != Settings.ArchitectMenuDefaultDesigCatDef && adcd.defName != "Furniture")
                    {
                        alist.Add(
                            new FloatMenuOption(
                                adcd.LabelCap + " (" + adcd.defName + ")", delegate
                                {
                                    Utils.Mess(Utils.Dbf.Settings, "Architect Menu placement set to " + adcd);
                                    Settings.ArchitectMenu_ChangeLocation(adcd.defName);
                                }
                            )
                        );
                    }
                }
                Find.WindowStack.Add(new FloatMenu(alist));
            }

            l.CheckboxLabeled(
                (Settings.ArchitectMenuDefaultDesigCatDef + "_ArchitectMenuAlwaysShowCategory").Translate(), ref Settings._architectMenuAlwaysShowCategory,
                (Settings.ArchitectMenuDefaultDesigCatDef + "_ArchitectMenuAlwaysShowDesc").Translate()
            );

            // Do we always display?  If so, display:
            if (Settings._architectMenuAlwaysShowCategory != Settings._architectMenuAlwaysShowTmp)
            {
                if (Settings._architectMenuAlwaysShowCategory) { Settings.ArchitectMenu_Show(); }
                else if (Settings._architectMenuDesigCatDef != Settings.ArchitectMenuDefaultDesigCatDef) { Settings.ArchitectMenu_Hide(); }
                Settings._architectMenuAlwaysShowTmp = Settings._architectMenuAlwaysShowCategory;
            }

            l.CheckboxLabeled(
                (Settings.ArchitectMenuDefaultDesigCatDef + "_ArchitectMenuMoveALL").Translate(), ref Settings._architectMenuMoveAllStorageItems, (Settings.ArchitectMenuDefaultDesigCatDef + "_ArchitectMenuMoveALLDesc").Translate()
            );

            if (Settings._architectMenuMoveAllStorageItems != Settings._architectMenuMoveAllTmp)
            {
                //  If turning off "all things in Storage", make sure to
                //    dump all the items into Furniture, to make sure they
                //    can at least be found somewhere.
                var ctmp = Settings._architectMenuDesigCatDef;

                if (Settings._architectMenuMoveAllStorageItems == false)
                {
                    Settings._architectMenuMoveAllStorageItems = true;
                    Settings.ArchitectMenu_ChangeLocation("Furniture");
                    Settings._architectMenuMoveAllStorageItems = false;
                }
                Settings.ArchitectMenu_ChangeLocation(ctmp);
                Settings._architectMenuMoveAllTmp = Settings._architectMenuMoveAllStorageItems;
            }
            // finished drawing settings for Architect Menu
            // -------------------
            // Allow player to turn of Over-Capacity check.
            //   Turn it off automatically for Project RimFactory and Extended Storage
            //   Note: should turn it off automatically for any other storage mods, too
            l.GapLine();
            tmpMod = ModLister.GetActiveModWithIdentifier("spdskatr.projectrimfactory") ?? ModLister.GetActiveModWithIdentifier("zymex.prf.lite") ?? ModLister.GetActiveModWithIdentifier("Skullywag.ExtendedStorage");

            if (tmpMod != null)
            {
                GUI.color = Color.gray;
                // This setting is disabled due to mod [Extended Storage, etc]
                l.Label("LWMDSignoredDueTo".Translate(tmpMod.Name));
            }
            l.CheckboxLabeled("LWMDSoverCapacityCheck".Translate(), ref Settings._checkOverCapacity, "LWMDSoverCapacityCheckDesc".Translate());
            GUI.color = origColor;
            // Per DSU settings - let players change them around...
            l.GapLine();

            if (Settings._allowPerDsuSettings)
            {
                if (l.ButtonText("LWMDSperDSUSettings".Translate())) { Find.WindowStack.Add(new DialogDSSettings()); }
            }
            else { l.CheckboxLabeled("LWMDSperDSUturnOn".Translate(), ref Settings._allowPerDsuSettings, "LWMDSperDSUturnOnDesc".Translate()); }
            l.GapLine(); // End. Finis. Looks pretty having a line at the end.
            Settings._totalContentHeight = l.CurHeight + 10f;
            l.End();
            Widgets.EndScrollView();
        }

        public static void DefsLoaded()
        {
            //            Log.Warning("LWM.deepstorag - defs loaded");
            // Todo? If settings are different from defaults, then:

            // Def-related changes:
            //TODO: this should probably have an option....
            if (Settings._defaultStoragePriority != StoragePriority.Important)
            {
                foreach (var d in Settings.AllDeepStorageUnits) { d.building.defaultStorageSettings.Priority = Settings._defaultStoragePriority; }
            }
            // Re-read Mod Settings - some won't have been read because Defs weren't loaded:
            //   (do this after priority changes above to allow user to override changes)
            //todo:
            Utils.Mess(Utils.Dbf.Settings, "Defs Loaded.  About to re-load settings");
            // NOTE/WARNING: the mod's settings' FolderName will be different for non-steam and steam versions.
            //   Internally, they are loaded using:
            //     this.modSettings = LoadedModManager
            //            .ReadModSettings<T>(this.intContent.FolderName, base.GetType().Name);
            // So don't do this:
            //   var s = LoadedModManager.ReadModSettings<Settings>("LWM.DeepStorage", "DeepStorageMod");
            // Do this instead:
            var mod = LoadedModManager.GetMod(typeof(DeepStorageMod));
            Utils.Warn(Utils.Dbf.Settings, "About to re-read mod settings from: " + GenText.SanitizeFilename(string.Format("Mod_{0}_{1}.xml", mod.Content.FolderName, "DeepStorageMod")));
            var s = LoadedModManager.ReadModSettings<Settings>(mod.Content.FolderName, "DeepStorageMod");

            // Architect Menu:
            if (Settings._architectMenuActualDef == null) { Settings._architectMenuActualDef = DefDatabase<DesignationCategoryDef>.GetNamed(Settings.ArchitectMenuDefaultDesigCatDef); }

            if (Settings._architectMenuDesigCatDef != Settings.ArchitectMenuDefaultDesigCatDef || Settings._architectMenuMoveAllStorageItems) // in which case, we need to redo menu anyway
            {
                Settings.ArchitectMenu_ChangeLocation(Settings._architectMenuDesigCatDef, true);
            }
        }

        // Architect Menu:
        public static void ArchitectMenu_ChangeLocation(string newDefName, bool loadingOnStartup = false)
        {
            //            Utils.Warn(Utils.DBF.Settings, "SettingsChanged()");
            DesignationCategoryDef prevDesignationCatDef;

            if (loadingOnStartup) { prevDesignationCatDef = DefDatabase<DesignationCategoryDef>.GetNamed(Settings.ArchitectMenuDefaultDesigCatDef); }
            else { prevDesignationCatDef                  = DefDatabase<DesignationCategoryDef>.GetNamed(Settings._architectMenuDesigCatDef, false); }

            // If switching to default, put default into def database.
            if (newDefName == Settings.ArchitectMenuDefaultDesigCatDef) { Settings.ArchitectMenu_Show(); }

            // Compatibility Logic:
            //   If certain mods are loaded and all storage units are to go in one menu,
            //   maybe we want to remove the other menu?  Or maybe we want to use that
            //   one by default:
            // For Deep Storage, if the player also has Quantum Storage, use their menu insead:
            if (Settings._architectMenuMoveAllStorageItems
                && !Settings._architectMenuAlwaysShowCategory
                && newDefName == Settings.ArchitectMenuDefaultDesigCatDef
                && ModLister.GetActiveModWithIdentifier("Cheetah.QuantumStorageRedux") != null) { newDefName = "QSRStorage"; }
            var newDesignationCatDef = DefDatabase<DesignationCategoryDef>.GetNamed(newDefName);

            if (newDesignationCatDef == null)
            {
                Log.Warning("LWM.DeepStorage: Failed to find menu category " + newDefName + " - reverting to default");
                newDefName = Settings.ArchitectMenuDefaultDesigCatDef;
                Settings.ArchitectMenu_Show();
                newDesignationCatDef = DefDatabase<DesignationCategoryDef>.GetNamed(newDefName);
            }
            // Architect Menu: Specify all your buildings/etc:
            //   var allMyBuildings=DefDatabase<ThingDef>.AllDefsListForReading.FindAll(x=>x.HasComp(etc)));
            var itemsToMove = Settings.LoadedDeepStorageUnits.ToList();
            // We can move ALL the storage buildings!  If the player wants.  I do.
            var desigsToNotMove  = new List<DesignationCategoryDef>();
            var desigsToOnlyCopy = new List<DesignationCategoryDef>();

            if (Settings._architectMenuMoveAllStorageItems)
            {
                //                Log.Error("Trying to mvoe everythign:");
                // Don't move hoppers, etc:
                desigsToNotMove.Add(DefDatabase<DesignationCategoryDef>.GetNamed("Production"));
                // Don't move Replimat either:
                //   (hoppers, etc.)
                //   Note that it's possible the ReplimatFeedTank should be copied to Storage,
                //   but I think it's okay to leave it in Replimat.
                var tmp = DefDatabase<DesignationCategoryDef>.GetNamed("Replimat_Replimat", false);

                if (tmp != null) { desigsToNotMove.Add(tmp); }
                // TODO: get these categories in a more flexible way!
                // ProjectRimFactory has several subclasses of Building_Storage that are in the Industrial category.
                //   Several users of PRF have gotten confused when they couldn't find the storage things.
                var industrialCategory = DefDatabase<DesignationCategoryDef>.GetNamed("Industrial", false);

                //   So we COULD remove those storage buildings from our list too:
                //     if (industrialCategory!=null) desigsToNotMove.Add(industrialCategory);
                //   But, let's just copy them:
                if (industrialCategory != null) { desigsToOnlyCopy.Add(industrialCategory); }
                // Bonus PRF: DocWorld changes the designation from Industrial to DZ_Industrial.
                // Get them both:
                industrialCategory = DefDatabase<DesignationCategoryDef>.GetNamed("DZ_Industrial", false);

                if (industrialCategory != null) { desigsToOnlyCopy.Add(industrialCategory); }

                // Interesting detail: apparently it IS possible to have thingDefs with null thingClass... weird.
                itemsToMove = DefDatabase<ThingDef>.AllDefsListForReading.FindAll(
                    x => x?.thingClass != null && (x.thingClass == typeof(Building_Storage) || x.thingClass.IsSubclassOf(typeof(Building_Storage))) && x.designationCategory != null && !desigsToNotMove.Contains(x.designationCategory)
                );
                /*if (ModLister.GetActiveModWithIdentifier("spdskatr.projectrimfactory")!=null) {
                    if (industrialCategory==null) {
                        Log.Warning("LWM.DeepStorage: menu compatibility with Project RimFactory failed: could not find Industrial cat");
                    } else {

                    }
                }*/
                // testing:
                //                itemsToMove.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.FindAll(x=>x.defName.Contains("MURWallLight")));
            }
            Utils.Mess(Utils.Dbf.Settings, "Moving these units to 'Storage' menu: " + string.Join(", ", itemsToMove));
            // get access to a DesignationCategoryDef's resolvedDesignators:
            var resolvedDesignatorsField = typeof(DesignationCategoryDef).GetField("resolvedDesignators", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var d in itemsToMove)
            {
                if (d.designationCategory == null)
                {
                    continue; // very very possible
                }
                //                Log.Error("Moving item "+d.defName+" (category: "+(d.designationCategory!=null?d.designationCategory.ToString():"NONE"));
                var resolvedDesignators = (List<Designator>) resolvedDesignatorsField.GetValue(d.designationCategory);

                if (d.designatorDropdown == null)
                {
                    //                    Log.Message("No dropdown");
                    // easy case:
                    // Old menu location:
                    if (!desigsToOnlyCopy.Contains(d.designationCategory))
                        //                    Log.Message("  Removed this many entries in "+d.designationCategory+": "+
                    {
                        resolvedDesignators.RemoveAll(x => x is Designator_Build && ((Designator_Build) x).PlacingDef == d);
                    }
                    //                        );
                    // Now do new:
                    resolvedDesignators = (List<Designator>) resolvedDesignatorsField.GetValue(newDesignationCatDef);
                    // To make sure there are no duplicates:
                    resolvedDesignators.RemoveAll(x => x is Designator_Build && ((Designator_Build) x).PlacingDef == d);
                    resolvedDesignators.Add(new Designator_Build(d));
                }
                else
                {
                    //                    Log.Warning("LWM.DeepStorage: ThingDef "+d.defName+" has a dropdown Designator.");
                    // Hard case: Designator_Dropdowns!
                    var dd = (Designator_Dropdown) resolvedDesignators.Find(x => x is Designator_Dropdown && ((Designator_Dropdown) x).Elements.Find(y => y is Designator_Build && ((Designator_Build) y).PlacingDef == d) != null);

                    if (dd != null)
                    {
                        //                        Log.Message("Found dropdown designator for "+d.defName);
                        if (!desigsToOnlyCopy.Contains(d.designationCategory)) { resolvedDesignators.Remove(dd); }
                        // Switch to new category:
                        resolvedDesignators = (List<Designator>) resolvedDesignatorsField.GetValue(newDesignationCatDef);

                        if (!resolvedDesignators.Contains(dd))
                        {
                            //                            Log.Message("  Adding to new category "+newDesignationCatDef);
                            resolvedDesignators.Add(dd);
                        }
                        //                    } else { //debug
                        //                        Log.Message("   ThingDef "+d.defName+" has designator_dropdown "+d.designatorDropdown.defName+
                        //                            ", but cannot find it in "+d.designationCategory+" - this is okay if something else added it.");
                    }
                }
                d.designationCategory = newDesignationCatDef;
            }
            // Flush designation category defs:.....dammit
            //            foreach (var x in DefDatabase<DesignationCategoryDef>.AllDefs) {
            //                x.ResolveReferences();
            //            }
            //            prevDesignationCatDef?.ResolveReferences();
            //            newDesignationCatDef.ResolveReferences();
            //ArchitectMenu_ClearCache(); // we do this later one way or another

            // To remove the mod's DesignationCategoryDef from Architect menu:
            //   remove it from RimWorld.MainTabWindow_Architect's desPanelsCached.
            // To do that, we remove it from the DefDatabase and then rebuild the cache.
            //   Removing only the desPanelsCached entry does work: the entry is
            //   recreated when a new game is started.  So if the options are changed
            //   and then a new game started...the change gets lost.
            // So we have to update the DefsDatabase.
            // This is potentially difficult: the .index can get changed, and that
            //   can cause problems.  But nothing seems to use the .index for any
            //   DesignationCategoryDef except for the menu, so manually adjusting
            //   the DefsDatabase is safe enough:
            if (!Settings._architectMenuAlwaysShowCategory && newDefName != Settings.ArchitectMenuDefaultDesigCatDef)
            {
                Settings.ArchitectMenu_Hide();
                // ArchitectMenu_ClearCache(); //hide flushes cache
                //                    if (tmp.AllResolvedDesignators.Count <= tmp.specialDesignatorClasses.Count)
                //                        isCategoryEmpty=false;
                /*
                //                    Log.Message("Removing old menu!");
                                    // DefDatabase<DesignationCategoryDef>.Remove(tmp);
                                    if (!tmp.AllResolvedDesignators.NullOrEmpty()) {
                                        foreach (var d in tmp.AllResolvedDesignators) {
                                            if (!tmp.specialDesignatorClasses.Contains(d)) {
                                                isCategoryEmpty=false;
                                                break;
                                            }
                                        }
                                    }
                                    */
                //                    if (isCategoryEmpty)
            }
            else
            {
                // Simply flush cache:
                Settings.ArchitectMenu_ClearCache();
            }
            // Note that this is not perfect: if the default menu was already open, it will still be open (and
            //   empty) when the settings windows are closed.  Whatever.

            // Oh, and actually change the setting that's stored:
            Settings._architectMenuDesigCatDef = newDefName;

            // Finally, if Extended Storage(!) is loaded, and we took all their
            //   storage items, remove their menu as well:
            DesignationCategoryDef tmpD;

            if (ModLister.HasActiveModWithName("Extended Storage")
                && (tmpD = DefDatabase<DesignationCategoryDef>.GetNamed("FurnitureStorage", false)) != null
                && !Settings._architectMenuAlwaysShowCategory
                && Settings._architectMenuDesigCatDef != "FurnitureStorage")
            {
                // DefDatabase<DesignationCategoryDef>.Remove(tmpD);
                typeof(DefDatabase<>).MakeGenericType(typeof(DesignationCategoryDef)).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic).Invoke(
                    null, new object[]
                    {
                        tmpD
                    }
                );
            }

            /*            List<ArchitectCategoryTab> archMenu=(List<ArchitectCategoryTab>)Harmony.AccessTools
                            .Field(typeof(RimWorld.MainTabWindow_Architect), "desPanelsCached")
                            .GetValue((MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow);
                        archMenu.RemoveAll(t=>t.def.defName==architectMenuDefaultDesigCatDef);
            
                        archMenu.Add(new ArchitectCategoryTab(newDesignationCatDef));
                        archMenu.Sort((a,b)=>a.def.order.CompareTo(b.def.order));
                        archMenu.SortBy(a=>a.def.order, b=>b.def.order); // May need (type of var a)=>...
            
                        */

            /*            Harmony.AccessTools.Method(typeof(RimWorld.MainTabWindow_Architect), "CacheDesPanels")
                            .Invoke(((MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow), null);*/

            /*
            
                        if (architectMenuDesignationCatDefDefName=="LWM_DS_Storage") { // default
                            if (DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage") == null) {
                                Utils.Mess(Utils.DBF.Settings,"Adding 'Storage' to the architect menu.");
                                DefDatabase<DesignationCategoryDef>.Add(architectLWM_DS_Storage_DesignationCatDef);
                            } else {
                                Utils.Mess(Utils.DBF.Settings, "No need to add 'Storage' to the architect menu.");
                            }
                            architectCurrentDesignationCatDef=architectLWM_DS_Storage_DesignationCatDef;
                        } else {
                            // remove our "Storage" from the architect menu:
                            Utils.Mess(Utils.DBF.Settings,"Removing 'Storage' from the architect menu.");
                            DefDatabase<DesignationCategoryDef>.AllDefsListForReading.Remove(architectLWM_DS_Storage_DesignationCatDef);
                            if (DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage") != null) {
                                Log.Error("Failed to remove LWM_DS_Storage :("+DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("LWM_DS_Storage"));
                            }
            
                            architectCurrentDesignationCatDef=DefDatabase<DesignationCategoryDef>.GetNamed(architectMenuDesignationCatDefDefName);
                        }
                        prevDesignationCatDef?.ResolveReferences();
                        architectCurrentDesignationCatDef.ResolveReferences();
            
                        Harmony.AccessTools.Method(typeof(RimWorld.MainTabWindow_Architect), "CacheDesPanels")
                            .Invoke((), null);
            */
            Utils.Warn(Utils.Dbf.Settings, "Settings changed architect menu");
        }

        public static void ArchitectMenu_Hide()
        {
            DesignationCategoryDef tmp;

            if ((tmp = DefDatabase<DesignationCategoryDef>.GetNamed(Settings.ArchitectMenuDefaultDesigCatDef, false)) != null && !Settings._architectMenuAlwaysShowCategory)
            {
                // DefDatabase<DesignationCategoryDef>.Remove(tmp);
                typeof(DefDatabase<>).MakeGenericType(typeof(DesignationCategoryDef)).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic).Invoke(
                    null, new object[]
                    {
                        tmp
                    }
                );
                // No need to SetIndices() or anything: .index are not used for DesignationCategoryDef(s).  I hope.
            }
            Settings.ArchitectMenu_ClearCache();
        }

        public static void ArchitectMenu_Show()
        {
            if (DefDatabase<DesignationCategoryDef>.GetNamed(Settings.ArchitectMenuDefaultDesigCatDef, false) == null) { DefDatabase<DesignationCategoryDef>.Add(Settings._architectMenuActualDef); }
            // No adding back Extended Storage menu if it's gone...
            Settings.ArchitectMenu_ClearCache();
        }

        public static void ArchitectMenu_ClearCache() =>
            // Clear the architect menu cache:
            //   Run the main Architect.TabWindow.CacheDesPanels()
            typeof(MainTabWindow_Architect).GetMethod("CacheDesPanels", BindingFlags.NonPublic | BindingFlags.Instance).Invoke((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow, null);

        public override void ExposeData()
        {
            Utils.Warn(Utils.Dbf.Settings, "Expose Data called: Mode: " + Scribe.mode);
            //Log.Error("LWM.DeepStorage: Settings ExposeData() called");
            base.ExposeData();

            Scribe_Values.Look(ref Settings._storingTakesTime, "storing_takes_time", true);
            Scribe_Values.Look(ref Settings._storingGlobalScale, "storing_global_scale", 1f);
            Scribe_Values.Look(ref Settings._storingTimeConsidersStackSize, "storing_time_CSS", true);
            Scribe_Values.Look(ref Settings._robotsCanUse, "robotsCanUse", true);
            Scribe_Values.Look(ref PatchIsGoodStoreCell._necessaryIntelligenceToUseDeepStorage, "int_to_use_DS", Intelligence.Humanlike);
            Scribe_Values.Look(ref Settings._defaultStoragePriority, "default_s_priority", StoragePriority.Important);
            Scribe_Values.Look(ref Settings._checkOverCapacity, "check_over_capacity", true);
            Scribe_Values.Look(ref Settings._useEjectButton, "useEjectButton", true);
            Scribe_Values.Look(ref Settings._useDeepStorageRightClickLogic, "useRightClickLogic", true); //turn on for everyone :p
            // Architect Menu:
            Scribe_Values.Look(ref Settings._architectMenuDesigCatDef, "architect_desig", Settings.ArchitectMenuDefaultDesigCatDef);
            Scribe_Values.Look(ref Settings._architectMenuAlwaysShowCategory, "architect_show");
            Scribe_Values.Look(ref Settings._architectMenuMoveAllStorageItems, "architect_moveall", true);
            // Per DSU Building storage settings:
            Scribe_Values.Look(ref Settings._allowPerDsuSettings, "allowPerDSUSettings");

            // Only load settigs if defs are loaded (there is separate mechanism to
            //   check settings after defs loaded)
            if (Settings._allowPerDsuSettings && StaticConstructorOnStartupUtility.coreStaticAssetsLoaded) { DialogDSSettings.ExposeDsuSettings(Settings.AllDeepStorageUnits); }
        } // end ExposeData()
    }

    internal static class DisplayHelperFunctions
    {
        public const float LabelHeight = 22f;

        // Helper function to create EnumRadioButton for Enums in settings
        public static bool EnumRadioButton<T>(this Listing_Standard ls, ref T val, string label, string tooltip = "", bool showEnumValues = true, string[] buttonLabels = null)
        {
            if (val as Enum == null)
            {
                Log.Error("LWM.DisplayHelperFunction: EnumRadioButton passed non-enum value");

                return false;
            }
            var result = false;

            if (tooltip == "") { ls.Label(label); }
            else { ls.Label(label, -1, tooltip); }
            var enumValues = Enum.GetValues(val.GetType());
            var i          = 0;

            foreach (T x in enumValues)
            {
                string optionLabel;

                if (showEnumValues || buttonLabels == null)
                {
                    optionLabel = x.ToString();

                    if (buttonLabels != null) { optionLabel += ": " + buttonLabels[i]; }
                }
                else
                {
                    optionLabel = buttonLabels[i]; // got a better way?
                }

                if (ls.RadioButton(optionLabel, val.ToString() == x.ToString()))
                {
                    val    = x; // I swear....ToString() was the only thing that worked.
                    result = true;
                } // nice try, C#, nice try.
                // ((val as Enum)==(x as Enum)) // nope
                // (System.Convert.ChangeType(val, Enum.GetUnderlyingType(val.GetType()))==
                //  System.Convert.ChangeType(  x, Enum.GetUnderlyingType(  x.GetType()))) // nope
                // (x.ToString()==val.ToString())// YES!
                i++;
            }

            return result;
        }

        // Helper function to create EnumRadioButton for Enums in settings
        /*public static bool EnumRadioButton<T>(float width, ref float y, ref T val, string label, string tooltip="",
                                              bool showEnumValues=true, string[] buttonLabels=null) {
            if ((val as Enum)==null) {
                Log.Error("LWM.DisplayHelperFunction: EnumRadioButton passed non-enum value");
                return false;
            }
            bool result=false;
            MyLabel(width, ref y, label, tooltip);
            var enumValues = Enum.GetValues(val.GetType());
            int i=0;
            foreach (T x in enumValues) {
                string optionLabel;
                if (showEnumValues || buttonLabels==null) {
                    optionLabel=x.ToString();
                    if (buttonLabels != null) {
                        optionLabel+=": "+buttonLabels[i];
                    }
                } else {
                    optionLabel=buttonLabels[i]; // got a better way?
                }
                //TODO
                if (l.RadioButton(optionLabel, (val.ToString()==x.ToString()))) {
                    val=x; // I swear....ToString() was the only thing that worked.
                    result=true;
                } // nice try, C#, nice try.
                // ((val as Enum)==(x as Enum)) // nope
                // (System.Convert.ChangeType(val, Enum.GetUnderlyingType(val.GetType()))==
                //  System.Convert.ChangeType(  x, Enum.GetUnderlyingType(  x.GetType()))) // nope
                // (x.ToString()==val.ToString())// YES!
                i++;
            }
            return result;
        }
        */
        public static void MyLabel(float width, ref float y, string label, string tooltip = null)
        {
            var h = Text.CalcHeight(label, width);
            var r = new Rect(0, y, width, y + h);
            Widgets.Label(r, label);

            if (tooltip != null && tooltip != "") { TooltipHandler.TipRegion(r, tooltip); }
            y += h + 2;
        }
    } // end DisplayHelperFunctions
}
