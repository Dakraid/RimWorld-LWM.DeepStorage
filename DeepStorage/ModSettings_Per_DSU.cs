using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

/// <summary>
///   Two Dialog windows to allow whiny RimWorld players to change settings for the Deep Storage units
///     and the logic to make that happen.
///   Basic idea:
///     on load, check if modifying units is turned on.  If so, once defs are loaded, do second
///     pass through the Setting's ExposeData() and this time, parse "DSU_LWM_defName_fieldName",
///     populate a dictionary with the default values, and change the defs on the fly.
///
///   This file contains the two dialog windows, keeps the default settings, and handles making the def changes.
///   The call to re-read the storage settings is done in ModSettings.cs, and the call to ExposeDSUSettings is
///     done in ModSettings' ExposeData.
/// </summary>

namespace LWM.DeepStorage
{
    // The window that lists all the DSUs available:
    public class DialogDSSettings : Window {
        public DialogDSSettings() {
			this.forcePause = true;
			this.doCloseX = true;
            this.doCloseButton = false;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
        }

		public override Vector2 InitialSize => new Vector2(900f, 700f);

        public override void DoWindowContents(Rect inRect)
		{
            var contentRect = new Rect(0, 0, inRect.width, inRect.height - (CloseButSize.y + 10f)).ContractedBy(10f);
            var scrollBarVisible = _totalContentHeight > contentRect.height;
            var scrollViewTotal = new Rect(0f, 0f, contentRect.width - (scrollBarVisible ? ScrollBarWidthMargin : 0), _totalContentHeight);
            Widgets.DrawHighlight(contentRect);
            Widgets.BeginScrollView(contentRect, ref _scrollPosition, scrollViewTotal);
            var curY = 0f;
            var r=new Rect(0,curY,scrollViewTotal.width, LabelHeight);

            Widgets.CheckboxLabeled(r, "LWMDSperDSUturnOn".Translate(), ref Settings._allowPerDsuSettings);//TODO
            TooltipHandler.TipRegion(r, "LWMDSperDSUturnOnDesc".Translate());
            curY+=LabelHeight+1f;
            if (!Settings._allowPerDsuSettings) {
                r=new Rect(5f, curY, scrollViewTotal.width-10f, LabelHeight);
                Widgets.Label(r, "LWMDSperDSUWarning".Translate());
                curY+=LabelHeight;
            }
            Widgets.DrawLineHorizontal(0f, curY, scrollViewTotal.width);
            curY+=10f;

            // todo: make this static?
            //List<ThingDef> l=DefDatabase<ThingDef>.AllDefsListForReading.Where(ThingDef d => d.Has

            // Roll my own buttons, because dammit, I want left-justified buttons:
            //   (mirroring Widgets.ButtonTextWorker)
            GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
            var bg=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBG", true);
            var bgmouseover=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGMouseover", true);
            var bgclick=ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGClick", true);
            //  note: make own list b/c this can modify what's in the DefDatabase.
            foreach (var u in Settings.AllDeepStorageUnits.ToList()) {
                //////////////// Disble button: //////////////////
                // disabled if it's already been disabled previously
                //   or if it's slated to be disabled on window close
                var isEnabled=!_tracker.HasDefaultValueFor(u.defName, "def") &&
                              (this._unitsToBeDisabled==null || !_unitsToBeDisabled.Contains(u));
                var wasEnabled = isEnabled;
                var disableRect = new Rect(5f, curY, LabelHeight, LabelHeight);
                TooltipHandler.TipRegion(disableRect, "TODO: Add description. But basically, you can disable some units and they won't show up in game.\n\nVERY likely to cause unimportant errors in saved games.");
                Widgets.Checkbox(disableRect.x, disableRect.y, ref isEnabled, LabelHeight, false, true, null, null);
                if (!isEnabled && wasEnabled) { // newly disabled
                    Utils.Warn(Utils.Dbf.Settings, "Marking unit for disabling: "+u.defName);
                    if (_unitsToBeDisabled==null)
                    {
                        _unitsToBeDisabled = new HashSet<ThingDef>();
                    }
                    _unitsToBeDisabled.Add(u); // hash sets don't care if it's already there!
                }
                if (isEnabled && !wasEnabled) { // add back:
                    Utils.Warn(Utils.Dbf.Settings, "Restoring disabled unit: "+u.defName);
                    if (_unitsToBeDisabled !=null &&  _unitsToBeDisabled.Contains(u)) {
                        _unitsToBeDisabled.Remove(u);
                    }
                    if (_tracker.HasDefaultValueFor(u.defName, "def")) {
                        _tracker.Remove(u.defName, "def");
                    }
                    if (!DefDatabase<ThingDef>.AllDefsListForReading.Contains(u))
                    {
                        DialogDSSettings.ReturnDefToUse(u);
                    }
                }
                //////////////// Select def: //////////////////
                r=new Rect(10f+LabelHeight, curY, (scrollViewTotal.width)*2/3-12f-LabelHeight, LabelHeight);
                // Draw button-ish background:
                var atlas = bg;
				if (Mouse.IsOver(r))
				{
					atlas = bgmouseover;
					if (Input.GetMouseButton(0))
					{
						atlas = bgclick;
					}
				}
				Widgets.DrawAtlas(r, atlas);
                // button text:
                Widgets.Label(r, u.label+" (defName: "+u.defName+")");
                // button clickiness:
                if (Widgets.ButtonInvisible(r)) {
                    Find.WindowStack.Add(new DialogDsuSettings(u));
                }
                //////////////// Reset button: //////////////////
                r=new Rect((scrollViewTotal.width)*2/3+2f,curY, (scrollViewTotal.width)/3-7f, LabelHeight);
                if (_tracker.IsChanged(u.defName) && Widgets.ButtonText(r, "ResetBinding".Translate())) {
                    DialogDSSettings.ResetDsuToDefaults(u.defName);
                }
                curY+=LabelHeight+2f;
            }
            GenUI.ResetLabelAlign();
            // end buttons

            Widgets.EndScrollView();
            // close button:
            r=new Rect(inRect.width/2-(CloseButSize.x/2), inRect.height-CloseButSize.y-5f, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(r, "CloseButton".Translate())) {
                if (_unitsToBeDisabled != null && _unitsToBeDisabled.Count > 0) {
                    //TODO: add out-of-order flag.
                    foreach (var d in _unitsToBeDisabled) {
                        Utils.Warn(Utils.Dbf.Settings, "Closing Window: Removing def: "+d.defName);
                        RemoveDefFromUse(d);
                        _tracker.AddDefaultValue(d.defName, "def", d);
                    }
                    _unitsToBeDisabled=null;
                }
                Close();
            }
            r=new Rect(10f, inRect.height-CloseButSize.y-5f, 2*CloseButSize.x, CloseButSize.y);
            if (_tracker.HasAnyDefaultValues && Widgets.ButtonText(r, "LWM.ResetAllToDefault".Translate())) {
                Utils.Mess(Utils.Dbf.Settings, "Resetting all per-building storage settings to default:");
                ResetAllToDefaults();
            }
            _totalContentHeight = curY;
        }
        private static void RemoveDefFromUse(ThingDef def) {
            // Remove from DefDatabase:
            //   equivalent to DefDatabase<DesignationCategoryDef>.Remove(def);
            //                  that's a private method, of course ^^^^^^
            //   #reflection #magic
            typeof(DefDatabase<>).MakeGenericType(new Type[] {typeof(ThingDef)})
                    .GetMethod("Remove", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke (null, new object [] { def });

            DefDatabase<ThingDef>.AllDefsListForReading.Remove(def);
            var tester=DefDatabase<ThingDef>.GetNamed(def.defName, false);
            if (tester != null)
            {
                Log.Error("Tried to remove "+def.defName+" from DefDatabase, but it's stil there???");
            }

            // remove from architect menu
            if (def.designationCategory != null) {
                Utils.Mess(Utils.Dbf.Settings, "  removing "+def+" from designation category "+def.designationCategory);
                def.designationCategory.AllResolvedDesignators.RemoveAll(x=>((x is Designator_Build) &&
                                                                             ((Designator_Build)x).PlacingDef==def));
            }
            return;
        }
        private static void ReturnDefToUse(ThingDef def) {
            Utils.Mess(Utils.Dbf.Settings, "Returning def "+def+" to use.");
            // Def database
            DefDatabase<ThingDef>.AllDefsListForReading.Add(def);
            // restore to architect menu:
            if (def.designationCategory != null) {
                def.designationCategory.AllResolvedDesignators.Add(new Designator_Build(def));
            }
        }
  ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private class DialogDsuSettings : Window {
            public DialogDsuSettings(ThingDef def) {
                this.forcePause = true;
                this.doCloseX = true;
                this.doCloseButton = false;
                this.closeOnClickedOutside = true;
                this.absorbInputAroundWindow = true;
                this._def=def;

                if (_tracker.HasDefaultValueFor(def.defName, "filter")) {
                    this._useCustomThingFilter=true;
                }

                SetTempVars();
            }
            private void SetTempVars() {
                _tmpLabel=_def.label;
                _tmpMaxNumStacks=_def.GetCompProperties<Properties>()._maxNumberStacks;
                _tmpMaxTotalMass=_def.GetCompProperties<Properties>()._maxTotalMass;
                _tmpMaxMassStoredItem=_def.GetCompProperties<Properties>()._maxMassOfStoredItem;
                _tmpShowContents=_def.GetCompProperties<Properties>()._showContents;
                _tmpStoragePriority=_def.building.defaultStorageSettings.Priority;
                _tmpOverlayType=_def.GetCompProperties<Properties>()._overlayType;
            }

            private void SetTempVarsToDefaults() {
                SetTempVars();
                _tmpLabel=_tracker.GetDefaultValue(_def.defName, "label", _tmpLabel);
                _tmpMaxNumStacks=_tracker.GetDefaultValue(_def.defName, "maxNumStacks", _tmpMaxNumStacks);
                _tmpMaxTotalMass=_tracker.GetDefaultValue(_def.defName, "maxTotalMass", _tmpMaxTotalMass);
                _tmpMaxMassStoredItem=_tracker.GetDefaultValue(_def.defName, "maxMassStoredItem", _tmpMaxMassStoredItem);
                _tmpShowContents=_tracker.GetDefaultValue(_def.defName, "showContents", _tmpShowContents);
                _tmpStoragePriority=_tracker.GetDefaultValue(_def.defName, "storagePriority", _tmpStoragePriority);
                _tmpOverlayType=_tracker.GetDefaultValue(_def.defName, "overlayType", _tmpOverlayType);
                _useCustomThingFilter=false;
                _customThingFilter=null;
            }
            private bool AreTempVarsDefaults() {
                var cp=_def.GetCompProperties<LWM.DeepStorage.Properties>();
                if (_tmpLabel!=_def.label)
                {
                    return false;
                }

                if (_tmpMaxMassStoredItem!=cp._maxMassOfStoredItem ||
                    _tmpMaxNumStacks!=cp._maxNumberStacks ||
                    _tmpMaxTotalMass!=cp._maxTotalMass ||
                    _tmpOverlayType!=cp._overlayType ||
                    _tmpShowContents!=cp._showContents
                   )
                {
                    return false;
                }

                if (_tmpStoragePriority!=_def.building.defaultStorageSettings.Priority)
                {
                    return false;
                }

                if (_useCustomThingFilter)
                {
                    return false;
                }

                return true;
            }

            public override Vector2 InitialSize => new Vector2(900f, 700f);

            public override void DoWindowContents(Rect inRect) // For a specific DSU
            {
                // for the record, Listing_Standards kind of suck. Convenient enough, but no flexibility
                // TODO when I'm bored: switch to manual, add red background for disabled units
                // Bonus problem with Listing_Standard: nested scrolling windows do not work
                //   well - specifically the ThingFilter UI insisde a Listing_Standard
                // We are able to get around that problem by having our ScrollView be outside
                //   the Listing_S... (instead of using the L_S's .ScrollView) and having the
                //   ThingFilter's UI after the L_S ends.

                // First: Set up a ScrollView:
                // outer window (with room for buttons at bottom:
                var s = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - CloseButSize.y - 5f);
                // inner window that has entire content:
                _y = _y + 200; // this is to ensure the Listing_Standard does not run out of height,
                //                and can fully display everything - giving a proper length 'y' at
                //                its .End() call.
                //          Worst case scenario: y starts as 0, and the L_S gets to a CurHeight of
                //            200, and then updates at 400 the next time.  Because of the way RW's
                //            windows work, this will rapidly converge on a large enough value.
                var inner = new Rect(0, 0, s.width - 20, this._y);
                Widgets.BeginScrollView(s, ref _dsuScrollPosition, inner);
                // We cannot do the scrollview inside the L_S:
                // l.BeginScrollView(s, ref DSUScrollPosition, ref v); // Does not allow filter UI
                var l = new Listing_Standard();
                l.Begin(inner);
                l.Label(_def.label);
                l.GapLine();
                // Much TODO, so wow:
                _tmpLabel=l.TextEntryLabeled("LWMDSpDSUlabel".Translate(), _tmpLabel);
                string tmpstring=null;
                l.TextFieldNumericLabeled("LWM_DS_maxNumStacks".Translate().CapitalizeFirst()+" "
                                          +"LWM_DS_Default".Translate(_tracker.GetDefaultValue(_def.defName, "maxNumStacks",
                                                                                              _tmpMaxNumStacks)),
                                          ref _tmpMaxNumStacks, ref tmpstring,0);
                tmpstring=null;
                l.TextFieldNumericLabeled<float>("LWM_DS_maxTotalMass".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(_tracker.GetDefaultValue(_def.defName,
                                                                            "maxTotalMass", _tmpMaxTotalMass).ToString()),
                                                 ref _tmpMaxTotalMass, ref tmpstring,0f);
                tmpstring=null;
                l.TextFieldNumericLabeled<float>("LWM_DS_maxMassOfStoredItem".Translate().CapitalizeFirst()+" "+
                                                 "LWM_DS_Default".Translate(_tracker.GetDefaultValue(_def.defName,
                                                             "maxMassStoredItem", _tmpMaxMassStoredItem).ToString()),
                                                 ref _tmpMaxMassStoredItem, ref tmpstring,0f);
                l.CheckboxLabeled("LWMDSpDSUshowContents".Translate(), ref _tmpShowContents);
                l.GapLine();
                l.EnumRadioButton(ref _tmpOverlayType, "LWMDSpDSUoverlay".Translate());
                l.GapLine();
                l.EnumRadioButton(ref _tmpStoragePriority, "LWMDSpDSUstoragePriority".Translate());
                l.GapLine();
                l.CheckboxLabeled("LWMDSpDSUchangeFilterQ".Translate(), ref _useCustomThingFilter,
                                  "LWMDSpDSUchangeFilterQDesc".Translate());
                _y = l.CurHeight;
                l.End();
                if (_useCustomThingFilter) {
                    if (_customThingFilter==null) {
                        _customThingFilter=new ThingFilter();
                        _customThingFilter.CopyAllowancesFrom(_def.building.fixedStorageSettings.filter);
                        Utils.Mess(Utils.Dbf.Settings,"Created new filter for "+_def.defName+": "+_customThingFilter);
//                        Log.Error("Old filter has: "+def.building.fixedStorageSettings.filter.AllowedDefCount);
//                        Log.Warning("New filter has: "+customThingFilter.AllowedDefCount);
                    }
                    // Since this is outside the L_S, we make our own rectangle and use it:
                    //   Nope: Rect r=l.GetRect(CustomThingFilterHeight); // this fails
                    var r = new Rect(20, _y, (inner.width - 40)*3/4, CustomThingFilterHeight);
                    _y += CustomThingFilterHeight;
                    ThingFilterUI.DoThingFilterConfigWindow(r, _thingFilterState, _customThingFilter);
                } else { // not using custom thing filter:
                    if (_customThingFilter!=null || _tracker.HasDefaultValueFor(this._def.defName, "filter")) {
                        _customThingFilter=null;
                        if (_tracker.HasDefaultValueFor(this._def.defName, "filter")) {
                            Utils.Mess(Utils.Dbf.Settings, "  Removing filter for "+_def.defName);
                            _def.building.fixedStorageSettings.filter=(ThingFilter)_tracker
                                .GetDefaultValue<ThingFilter>(_def.defName, "filter", null);
                            _tracker.Remove(_def.defName, "filter");
                        }
                    }
                }

                // This fails: l.EndScrollView(ref v);
                Widgets.EndScrollView();

                // Cancel button
                var closeRect = new Rect(inRect.width-CloseButSize.x, inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "CancelButton".Translate())) {
                    Utils.Mess(Utils.Dbf.Settings, "Cancel button selected - no changes made");
                    Close();
                }
                // Accept button - with accompanying logic
                closeRect = new Rect(inRect.width-(2*CloseButSize.x+5f), inRect.height-CloseButSize.y,CloseButSize.x,CloseButSize.y);
                if (Widgets.ButtonText(closeRect, "AcceptButton".Translate())) {
                    var props=_def.GetCompProperties<Properties>();
                    GUI.FocusControl(null); // unfocus, so that a focused text field may commit its value
                    Utils.Warn(Utils.Dbf.Settings, "\"Accept\" button selected: changing values for "+_def.defName);
                    _tracker.UpdateToNewValue(_def.defName, "label", _tmpLabel, ref _def.label);
                    _tracker.UpdateToNewValue(_def.defName,
                               "maxNumStacks", _tmpMaxNumStacks, ref props._maxNumberStacks);
                    _tracker.UpdateToNewValue(_def.defName,
                               "maxTotalMass", _tmpMaxTotalMass, ref props._maxTotalMass);
                    _tracker.UpdateToNewValue(_def.defName,
                               "maxMassStoredItem", _tmpMaxMassStoredItem, ref props._maxMassOfStoredItem);
                    _tracker.UpdateToNewValue(_def.defName,
                               "showContents", _tmpShowContents, ref props._showContents);
                    _tracker.UpdateToNewValue(_def.defName,
                               "overlayType", _tmpOverlayType, ref props._overlayType);
                    var tmpSp=_def.building.defaultStorageSettings.Priority; // hard to access private field directly
                    _tracker.UpdateToNewValue(_def.defName, "storagePriority", _tmpStoragePriority, ref tmpSp);
                    _def.building.defaultStorageSettings.Priority=tmpSp;
                    if (_useCustomThingFilter) { // danger ahead - automatically use it, even if stupidly set up
                        if (!_tracker.HasDefaultValueFor(_def.defName, "filter")) {
                            Utils.Mess(Utils.Dbf.Settings, "Creating default filter record for item "+_def.defName);
                            _tracker.AddDefaultValue(_def.defName, "filter", _def.building.fixedStorageSettings.filter);
                        }
                        _def.building.fixedStorageSettings.filter=_customThingFilter;
                    } else {
                        // restore default filter:
                        if (_tracker.HasDefaultValueFor(_def.defName, "filter")) {
                            // we need to remove it
                            Utils.Mess(Utils.Dbf.Settings, "Removing default filter record for item "+_def.defName);
                            _def.building.fixedStorageSettings.filter=(ThingFilter)_tracker
                                .GetDefaultValue<ThingFilter>(_def.defName, "filter", null);
                            _tracker.Remove(_def.defName, "filter");
                        }
                    }
                    Close();
                }
                // Reset to Defaults
                closeRect = new Rect(inRect.width-(4*CloseButSize.x+10f), inRect.height-CloseButSize.y,2*CloseButSize.x,CloseButSize.y);
                if (!AreTempVarsDefaults() && Widgets.ButtonText(closeRect, "ResetBinding".Translate())) {
                    SetTempVarsToDefaults();
                }
            }

            public override void PreOpen() {
                // Per Dialog_BillsConfig
                base.PreOpen();
                _thingFilterState.quickSearch.Reset();
            }

            private DefChangeTracker _tracker = Settings._defTracker;
            private ThingDef _def;
            private string _tmpLabel;
            private int _tmpMaxNumStacks;
            private float _tmpMaxTotalMass;
            private float _tmpMaxMassStoredItem;
            private bool _tmpShowContents;
            private LWM.DeepStorage.GuiOverlayType _tmpOverlayType;
            private StoragePriority _tmpStoragePriority;

            private bool _useCustomThingFilter=false;
            private ThingFilter _customThingFilter=null;
            private ThingFilterUI.UIState _thingFilterState = new ThingFilterUI.UIState();
            private Vector2 _dsuScrollPosition=new Vector2(0,0);
            private float _y=1000f;
            private const float CustomThingFilterHeight=400f;
        }
        private static void ResetDsuToDefaults(string resetDefName) {
            object tmpObject;
            var defName=resetDefName;
            string prop;
            var resetAll=(resetDefName==null || resetDefName=="");
            while ((resetAll && Settings._defTracker.GetFirstDefaultValue(out defName, out prop, out tmpObject))
                   || Settings._defTracker.GetFirstDefaultValueFor(defName, out prop, out tmpObject)) {
                Utils.Mess(Utils.Dbf.Settings,"Resetting "+prop+" to default value for "+defName);
                var def=DefDatabase<ThingDef>.GetNamed(defName, false);
                if (def==null) {
                    var tmp = (ThingDef)Settings._defTracker.GetDefaultValue(defName, "def", def);
                    if (tmp!=null) {
                        def=tmp;
                        // We are resetting the def, so we need it back in the DefDatabase!
                        ReturnDefToUse(def);
                        Settings._defTracker.Remove(defName, "def");
                        if (prop=="def")
                        {
                            continue;
                        }
                    } else {
                        //todo: put this error message it translate
                        Log.Warning("LWM.DeepStorage: Tried to change mod setting for "+defName+" but could not find def.\nClear your settings to remove this error.");
                        Settings._defTracker.Remove(defName, prop);
                        continue;
                    }
                }
                if (prop=="label") {
                    def.label=(string)(tmpObject);
                } else if (prop=="maxNumStacks") {
                    def.GetCompProperties<Properties>()._maxNumberStacks=(int)(tmpObject);
                } else if (prop=="maxTotalMass") {
                    def.GetCompProperties<Properties>()._maxTotalMass=(float)(tmpObject);
                } else if (prop=="maxMassStoredItem") {
                    def.GetCompProperties<Properties>()._maxMassOfStoredItem=(float)(tmpObject);
                } else if (prop=="showContents") {
                    def.GetCompProperties<Properties>()._showContents=(bool)(tmpObject);
                } else if (prop=="storagePriority") {
                    def.building.defaultStorageSettings.Priority=(StoragePriority)(tmpObject);
                } else if (prop=="overlayType") {
                    def.GetCompProperties<Properties>()._overlayType=(LWM.DeepStorage.GuiOverlayType)(tmpObject);
                } else if (prop=="filter") {
                    def.building.fixedStorageSettings.filter=(ThingFilter)(tmpObject);
                } else if (prop=="def") {
                    // Def was marked for removal but hasn't been removed yet
                    Utils.Mess(Utils.Dbf.Settings, "Removing "+defName+" from list of defs to remove.");
                    ReturnDefToUse(def);
                } else {
                    Log.Error("LWM.DeepStorage: FAILED TO RESET OPTION TO DEFAULT: "+defName+", "+prop);
                }
                Settings._defTracker.Remove(defName, prop);
            } // end while loop, defSettings.DefTracker didn't have anything else
            // done resetting!
        }

        private static void ResetAllToDefaults() => DialogDSSettings.ResetDsuToDefaults(null);

        public static void ExposeDsuSettings(IEnumerable<ThingDef> units) {
            // note: make our own list in case we modify DefDatabase/etc from here
            if (units==null) { Log.Warning("Passed null units"); return; }
            if (Settings._defTracker==null) {Log.Error("DefChangeTracker is null"); return;}
            foreach (var u in units.ToList()) {
                Utils.Warn(Utils.Dbf.Settings, "Expose DSU Settings: "+u.defName+" ("+Scribe.mode+")");
                var defName=u.defName;
                Settings._defTracker.ExposeSetting<string>(defName, "label",ref u.label);
                Settings._defTracker.ExposeSetting(defName, "maxNumStacks", ref u.GetCompProperties<Properties>()._maxNumberStacks);
                Settings._defTracker.ExposeSetting(defName, "maxTotalMass", ref u.GetCompProperties<Properties>()._maxTotalMass);
                Settings._defTracker.ExposeSetting(defName, "maxMassStoredItem", ref u.GetCompProperties<Properties>()._maxMassOfStoredItem);
                Settings._defTracker.ExposeSetting(defName, "showContents", ref u.GetCompProperties<Properties>()._showContents);
                Settings._defTracker.ExposeSetting(defName, "overlayType", ref u.GetCompProperties<Properties>()._overlayType);
                var tmpSp=u.building.defaultStorageSettings.Priority; // hard to access private field directly
                Settings._defTracker.ExposeSetting<StoragePriority>(defName, "storagePriority", ref tmpSp);
                u.building.defaultStorageSettings.Priority=tmpSp;
                // If fixedStorageSettings is null, it's because it can store anything. We don't change that:
                if (u.building?.fixedStorageSettings != null)
                {
                    Settings._defTracker.ExposeSettingDeep(defName, "filter", ref u.building.fixedStorageSettings.filter);
                }

                //Utils.Mess(Utils.DBF.Settings, "  Basics exposed.");
                //////////////////////// disabling defs //////////////////////////
                if (Scribe.mode == LoadSaveMode.LoadingVars) {
                    // Check if this unit has been disabled:
                    var disabled=false;
                    Scribe_Values.Look(ref disabled, "DSU_"+defName+"_disabled", false);
                    if (disabled) {
//todo                        defaultDSUValues["outOfOrder"]=true;
                        Utils.Mess(Utils.Dbf.Settings, "Startup: disabling unit "+u.defName);
                        Settings._defTracker.AddDefaultValue(defName, "def", u);
                        RemoveDefFromUse(u);
                    }
                } else if (Scribe.mode == LoadSaveMode.Saving) {
                    if (Settings._defTracker.HasDefaultValueFor(defName, "def")) {
                        var disabled=true;
                        Scribe_Values.Look(ref disabled, "DSU_"+defName+"_disabled", false);
                        Utils.Mess(Utils.Dbf.Settings, "Saving disabled unit name "+u.defName);
                    }
                }
            } // end units loop
        }

        private float _totalContentHeight=1000f;
        private Vector2 _scrollPosition;

		private const float TopAreaHeight = 40f;
		private const float TopButtonHeight = 35f;
		private const float TopButtonWidth = 150f;
        private const float ScrollBarWidthMargin = 18f;
        private const float LabelHeight=22f;


        // Actual Logic objects:
        //   list of DSUs to be disabled on window close:
        private HashSet<ThingDef> _unitsToBeDisabled=null;
        // Helpful for typing:
        public DefChangeTracker _tracker=Settings._defTracker;
    }

}
