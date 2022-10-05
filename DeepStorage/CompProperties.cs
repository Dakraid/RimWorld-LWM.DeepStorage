

//using Harmony;
// for OpCodes in Harmony Transpiler

// trace utils

namespace DeepStorage
{
#region
    using HarmonyLib;

    using RimWorld;

    using System.Collections.Generic;

    using Verse;
#endregion

    public class Properties : CompProperties
    {
        /************************* Stat window (information window) ***********************/
        // This will hopefully reduce the number of annoying questions in the discussion thread
        //   "What does this store?  Can I put X in there?"
        private static StatCategoryDef _deepStorageCategory;
        public int _additionalTimeEachDef = 0;      // extra time to store for each different type of object there
        public int _additionalTimeEachStack = 0;    // extra time to store for each stack already there
        public float _additionalTimeStackSize = 0f; // item with stack size 75 may take longer to store
        public StatDef _altStat = null;

        private string _categoriesString; // for the Stats window (information window)
        private string _defsString;
        private string _disallowedString;
        public bool _isSecure = false; // harder for pawns to get into, harder to break things inside, etc
        public float _maxMassOfStoredItem = 0f;
        public int _maxNumberStacks = 2;
        public float _maxTotalMass = 0f;

        public int _minNumberStacks = 1;
        public int _minTimeStoringTakes = -1;
        public GuiOverlayType _overlayType = GuiOverlayType.Normal;
        public ThingDef _parent; // :p  Have to keep track of this myself
        public List<ThingDef> _quickStoringItems = null;
        public bool _showContents = true;

        public int _size;
        public int _timeStoringTakes = 1000; // measured in ticks
        public Properties() => compClass = typeof(CompDeepStorage);

        public string AllowedCategoriesString
        {
            get
            {
                if (_categoriesString == null)
                {
                    _categoriesString = "";
                    var tf = _parent?.building?.fixedStorageSettings?.filter;

                    if (tf == null)
                    {
                        // filters can be null, e.g., shelves
                        //Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var c = (List<string>) AccessTools.Field(typeof(ThingFilter), "categories").GetValue(tf);

                    if (c.NullOrEmpty()) { return ""; }

                    foreach (var x in c)
                    {
                        if (_categoriesString != "") { _categoriesString += "\n"; }
                        _categoriesString += DefDatabase<ThingCategoryDef>.GetNamed(x).LabelCap;
                    }
                }

                return _categoriesString;
            }
        }

        public string AllowedDefsString
        {
            get
            {
                if (_defsString == null)
                {
                    _defsString = "";
                    var tf = _parent?.building?.fixedStorageSettings?.filter;

                    if (tf == null)
                    {
                        //Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var d = (List<ThingDef>) AccessTools.Field(typeof(ThingFilter), "thingDefs").GetValue(tf);

                    if (d.NullOrEmpty()) { return ""; }

                    foreach (var x in d)
                    {
                        if (_defsString != "") { _defsString += "\n"; }
                        _defsString += x.LabelCap;
                    }
                }

                return _defsString;
            }
        }

        public string DisallowedString
        {
            get
            {
                if (_disallowedString == null)
                {
                    _disallowedString = "";
                    var tf = _parent?.building?.fixedStorageSettings?.filter; // look familiar yet?

                    if (tf == null)
                    {
                        //Log.Warning("LWM.DeepStorage:could not find filter for "+parent.defName);
                        return "";
                    }
                    var c = (List<string>) AccessTools.Field(typeof(ThingFilter), "disallowedCategories").GetValue(tf);

                    if (!c.NullOrEmpty())
                    {
                        foreach (var x in c)
                        {
                            if (_disallowedString != "") { _disallowedString += "\n"; }
                            _disallowedString += DefDatabase<ThingCategoryDef>.GetNamed(x).LabelCap;
                        }
                    }
                    var d = (List<ThingDef>) AccessTools.Field(typeof(ThingFilter), "disallowedThingDefs").GetValue(tf);

                    if (!d.NullOrEmpty())
                    {
                        foreach (var x in d)
                        {
                            if (_defsString != "") { _defsString += "\n"; }
                            _defsString += x.LabelCap;
                        }
                    }
                }

                return _disallowedString;
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (var s in base.SpecialDisplayStats(req)) { yield return s; }

            if (Properties._deepStorageCategory == null)
            {
                Properties._deepStorageCategory = DefDatabase<StatCategoryDef>.GetNamed("LWM_DS_Stats");

                if (Properties._deepStorageCategory == null)
                {
                    Log.Warning("LWM.DeepStorage: Stat Category FAILED to load.");

                    yield break;
                }
            }

            yield return new StatDrawEntry(
                Properties._deepStorageCategory, "LWM_DS_maxNumStacks".Translate().ToString(), _size > 1 ? "LWM_DS_TotalAndPerCell".Translate(_maxNumberStacks * _size, _maxNumberStacks).ToString() : _maxNumberStacks.ToString(),
                "LWM_DS_maxNumStacksDesc".Translate(), 11 /*display priority*/
            );

            if (_minNumberStacks > 2)
            {
                yield return new StatDrawEntry(
                    Properties._deepStorageCategory, "LWM_DS_minNumStacks".Translate(), _size > 1 ? "LWM_DS_TotalAndPerCell".Translate(_minNumberStacks * _size, _minNumberStacks).ToString() : _minNumberStacks.ToString(),
                    "LWM_DS_minNumStacksDesc".Translate(_minNumberStacks * _size), 10 /*display priority*/
                );                                                                    //todo: more info here would be good!
            }

            if (_maxTotalMass > 0f)
            {
                yield return new StatDrawEntry(
                    Properties._deepStorageCategory, "LWM_DS_maxTotalMass".Translate(), _size > 1 ? "LWM_DS_TotalAndPerCell".Translate(Kg(_maxTotalMass * _size), Kg(_maxTotalMass)).ToString() : Kg(_maxTotalMass),
                    "LWM_DS_maxTotalMassDesc".Translate(), 9
                );
            }

            if (_maxMassOfStoredItem > 0f) { yield return new StatDrawEntry(Properties._deepStorageCategory, "LWM_DS_maxMassOfStoredItem".Translate(), Kg(_maxMassOfStoredItem), "LWM_DS_maxMassOfStoredItemDesc".Translate(), 8); }

            if (AllowedCategoriesString != "") { yield return new StatDrawEntry(Properties._deepStorageCategory, "LWM_DS_allowedCategories".Translate(), AllowedCategoriesString, "LWM_DS_allowedCategoriesDesc".Translate(), 7); }

            if (AllowedDefsString != "") { yield return new StatDrawEntry(Properties._deepStorageCategory, "LWM_DS_allowedDefs".Translate(), AllowedDefsString, "LWM_DS_allowedDefsDesc".Translate(), 6); }

            if (DisallowedString != "") { yield return new StatDrawEntry(Properties._deepStorageCategory, "LWM_DS_disallowedStuff".Translate(), DisallowedString, "LWM_DS_disallowedStuffDesc".Translate(), 5); }

            //            if (parent?.building?.fixedStorageSettings?.filter
        }

        private string Kg(float s)
        {
            if (_altStat == null) { return "LWM_DS_kg".Translate(s); }

            return "LWM_DS_BulkEtcOf".Translate(s, _altStat.label);
        }
        /************************* Done with Stat window ***********************/

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            _parent = parentDef; // no way to actually get this via def :p
            _size   = parentDef.Size.Area;
        }

        public static void RemoveAnyMultipleCompProps()
        {
            // For each def, make sure that only the last DS.Properties is
            // used.  (this can happen if a modder makes another DSU based
            // off of one of the base ones; see Pallet_Covered)  Call this
            // after all defs are loaded
            foreach (var d in DefDatabase<ThingDef>.AllDefs)
            {
                if (typeof(Building_Storage).IsAssignableFrom(d.thingClass))
                {
                    var cmps = d.comps;

                    for (var i = cmps.Count - 1; i >= 0; i--)
                    {
                        if (cmps[i] is Properties && i > 0)
                        {
                            // remove any earlier instances
                            // last one in should count:
                            for (i--; i >= 0; i--)
                            {
                                if (cmps[i] is Properties) { cmps.RemoveAt(i); }
                            }

                            break;
                        }
                    }
                }
                //continue to next def
            }
        } //end RemoveAnyMultipleCompProps
    }

    public enum GuiOverlayType : byte
    {
        Normal,
        CountOfAllStacks,     // Centered on DSU
        CountOfStacksPerCell, // Standard overlay position for each cell
        SumOfAllItems,        // Centered on DSU
        SumOfItemsPerCell,    // For e.g., Big Shelf
        None                  // Some users may want this
    }
}
