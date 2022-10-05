// for OpCodes in Harmony Transpiler

namespace DeepStorage
{
#region
    using HarmonyLib;

    using RimWorld;
    using RimWorld.Planet;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEngine;

    using Verse;
#endregion

    /********* UI ITab ******************************
     * Original ITab from sumghai - thanks!         *
     *   That saved a lot of work.                  *
     * Now almost all rewritten, with many requests *
     *   from various ppl on steam                  *
     *                                              */
    [StaticConstructorOnStartup]
    public class TabDeepStorageInventory : ITab
    {
        private const float TopPadding = 20f;
        private const float ThingIconSize = 28f;
        private const float ThingRowHeight = 28f;
        private const float ThingLeftX = 36f;
        private const float StandardLineHeight = 22f;
        private static readonly Texture2D _drop; // == TexButton.Drop
        public static readonly Color _thingLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color _highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private Building_Storage _buildingStorage;
        private Vector2 _scrollPosition = Vector2.zero;
        private float _scrollViewHeight = 1000f;

        private string _searchString = string.Empty;
        static TabDeepStorageInventory() => TabDeepStorageInventory._drop = (Texture2D) AccessTools.Field(AccessTools.TypeByName("Verse.TexButton"), "Drop").GetValue(null);

        public TabDeepStorageInventory()
        {
            size     = new Vector2(460f, 450f);
            labelKey = "Contents"; // could define <LWM.Contents>Contents</LWM.Contents> in Keyed language, but why not use what's there.
        }

        protected override void FillTab()
        {
            _buildingStorage = SelThing as Building_Storage; // don't attach this to other things, 'k?
            List<Thing> storedItems;
            //TODO: set fonts ize, etc.
            Text.Font = GameFont.Small;
            // 10f border:
            var frame = new Rect(10f, 10f, size.x - 10, size.y - 10);
            GUI.BeginGroup(frame);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            /*******  Title *******/
            var curY = 0f;

            Widgets.ListSeparator(
                ref curY, frame.width, labelKey.Translate()
                                   #if DEBUG
                                       + "    ("
                                       + _buildingStorage.ToString()
                                       + ")" // extra info for debugging
            #endif
            );
            curY += 5f;
            /****************** Header: Show count of contents, mass, etc: ****************/
            //TODO: handle each cell separately?
            string header, headerTooltip;
            var    cds = _buildingStorage.GetComp<CompDeepStorage>();

            if (cds != null) { storedItems = cds.GetContentsHeader(out header, out headerTooltip); }
            else { storedItems             = CompDeepStorage.GenericContentsHeader(_buildingStorage, out header, out headerTooltip); }
            var tmpRect = new Rect(8f, curY, frame.width - 16, Text.CalcHeight(header, frame.width - 16));
            Widgets.Label(tmpRect, header);
            // TODO: tooltip.  Not that it's anything but null now
            curY += tmpRect.height; //todo?

            /*************          ScrollView              ************/
            /*************          (contents)              ************/
            storedItems = storedItems.OrderBy(x => x.def.defName).ThenByDescending(
                x =>
                {
                    QualityCategory c;
                    x.TryGetQuality(out c);

                    return (int) c;
                }
            ).ThenByDescending(x => x.HitPoints / x.MaxHitPoints).ToList();
            // outRect is the is the rectangle that is visible on the screen:
            var outRect = new Rect(0f, 10f + curY, frame.width, frame.height - curY - GenUI.ListSpacing - TabDeepStorageInventory.TopPadding);
            // viewRect is inside the ScrollView, so it starts at y=0f
            var viewRect = new Rect(0f, 0f, frame.width - 16f, _scrollViewHeight); //TODO: scrollbars are slightly too far to the right
            // 16f ensures plenty of room for scrollbars.
            // scrollViewHeight is set at the end of this call (via layout?); it is the proper
            //   size the next time through, so it all works out.
            Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);

            curY = 0f; // now inside ScrollView

            if (storedItems.Count < 1)
            {
                Widgets.Label(viewRect, "NoItemsAreStoredHere".Translate());
                curY += 22;
            }

            var stringUpper = _searchString.ToUpperInvariant();
            var itemToDraw  = storedItems.Where(t => t.LabelNoCount.ToUpperInvariant().Contains(stringUpper)).ToList();

            TabDeepStorageInventory.GetIndexRangeFromScrollPosition(outRect.height, _scrollPosition.y, out var from, out var to, GenUI.ListSpacing);
            to = to > itemToDraw.Count ? itemToDraw.Count : to;

            curY = from * GenUI.ListSpacing;

            for (var i = from; i < to; i++) { DrawThingRow(ref curY, viewRect.width, itemToDraw[i]); }

            if (Event.current.type == EventType.Layout)
            {
                _scrollViewHeight = storedItems.Count * GenUI.ListSpacing + 25f; //25f buffer   -- ??
            }

            Widgets.EndScrollView();

            _searchString = Widgets.TextField(new Rect(0, outRect.yMax, outRect.width - GenUI.ScrollBarWidth, GenUI.ListSpacing), _searchString);

            GUI.EndGroup();
            //TODO: this should get stored at top and set here.
            GUI.color = Color.white;
            //TODO: this should get stored at top and set here.
            // it should get set to whatever draw-row uses at top
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawThingRow(ref float y, float width, Thing thing)
        {
            // Sumghai started from the right, as several things in vanilla do, and that's fine with me:
            Rect yetAnotherRect;

            /************************* InfoCardButton *************************/
            //       (it's the little "i" that pulls up full info on the item.)
            //   It's 24f by 24f in size
            width -= 24f;
            Widgets.InfoCardButton(width, y, thing);

            /************************* Allow/Forbid toggle *************************/
            //   We make this 24 by 24 too:
            width -= 24f;
            var forbidRect = new Rect(width, y, 24f, 24f); // is creating this rect actually necessary?
            var allowFlag  = !thing.IsForbidden(Faction.OfPlayer);
            var tmpFlag    = allowFlag;

            if (allowFlag) { TooltipHandler.TipRegion(forbidRect, "CommandNotForbiddenDesc".Translate()); }
            else { TooltipHandler.TipRegion(forbidRect, "CommandForbiddenDesc".Translate()); }
            //            TooltipHandler.TipRegion(forbidRect, "Allow/Forbid"); // TODO: Replace "Allow/Forbid" with a translated entry in a Keyed Language XML file
            Widgets.Checkbox(forbidRect.x, forbidRect.y, ref allowFlag, 24f, false, true);

            if (allowFlag != tmpFlag) // spamming SetForbidden is bad when playing multi-player - it spams Sync requests
            {
                thing.SetForbidden(!allowFlag, false);
            }

            /************************* Eject button *************************/
            if (Settings._useEjectButton)
            {
                width          -= 24f;
                yetAnotherRect =  new Rect(width, y, 24f, 24f);
                TooltipHandler.TipRegion(yetAnotherRect, "LWM.ContentsDropDesc".Translate());

                if (Widgets.ButtonImage(yetAnotherRect, TabDeepStorageInventory._drop, Color.gray, Color.white, false)) { TabDeepStorageInventory.EjectTarget(thing); }
            }
            /************************* Mass *************************/
            width -= 60f; // Caravans use 100f
            var massRect = new Rect(width, y, 60f, 28f);
            CaravanThingsTabUtility.DrawMass(thing, massRect);
            /************************* How soon does it rot? *************************/
            // Some mods add non-food items that rot, so we track those too:
            var cr = thing.TryGetComp<CompRottable>();

            if (cr != null)
            {
                var rotTicks = Math.Min(int.MaxValue, cr.TicksUntilRotAtCurrentTemp);

                if (rotTicks < 36000000)
                {
                    width -= 60f; // Caravans use 75f?  TransferableOneWayWidget.cs
                    var rotRect = new Rect(width, y, 60f, 28f);
                    GUI.color = Color.yellow;
                    Widgets.Label(rotRect, (rotTicks / 60000f).ToString("0.#"));
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(rotRect, "DaysUntilRotTip".Translate());
                    //TODO: figure out how to give this estimate if not in a fridge!
                }
            } // finish how long food will last

            /************************* Text area *************************/
            // TODO: use a ButtonInvisible over the entire area with a label and the icon.
            var itemRect = new Rect(0f, y, width, 28f);

            if (Mouse.IsOver(itemRect))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(itemRect, TexUI.HighlightTex);
            }

            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null) { Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing); }
            //TODO: set all this once:
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color   = ITab_Pawn_Gear.ThingLabelColor; // TODO: Aaaaah, sure?
            var textRect = new Rect(36f, y, itemRect.width - 36f, itemRect.height);
            var text     = thing.LabelCap;
            Text.WordWrap = false;
            Widgets.Label(textRect, text.Truncate(textRect.width));

            //            if (Widgets.ButtonText(rect4, text.Truncate(rect4.width, null),false)) {
            if (Widgets.ButtonInvisible(itemRect))
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(thing);
            }
            //TODO: etc
            Text.WordWrap = true;
            /************************* mouse-over description *************************/
            var text2 = thing.DescriptionDetailed;

            if (thing.def.useHitPoints)
            {
                var text3 = text2;
                text2 = string.Concat(text3, "\n", thing.HitPoints, " / ", thing.MaxHitPoints);
            }
            TooltipHandler.TipRegion(itemRect, text2);
            y += 28f;
        } // end draw thing row

        /// <summary>
        ///     Get the index range for a list whose content will be rendered on screen.
        /// </summary>
        /// <param name = "viewRectLength"> The length of view rect. </param>
        /// <param name = "scrollPosition"> Scroll position for the list view. </param>
        /// <param name = "from"> Start index of a list where drawing begins. </param>
        /// <param name = "to"> <paramref name = "to" /> is positioned at one element behind the index where drawing should stop. </param>
        /// <param name = "unitLength"> The length of a unit element in the list. </param>
        public static void GetIndexRangeFromScrollPosition(float viewRectLength, float scrollPosition, out int from, out int to, float unitLength)
        {
            from = Mathf.FloorToInt(scrollPosition / unitLength);
            to   = from + (int) Math.Ceiling(viewRectLength / unitLength);
        }

        // make this separate function instead of delegate() so the MP people can link to it
        public static void EjectTarget(Thing thing)
        {
            var loc = thing.Position;
            var map = thing.Map;
            thing.DeSpawn(); //easier to pick it up and put it down using existing game

            //  logic by way of GenPlace than to find a good place myself
            if (!GenPlace.TryPlaceThing(
                    thing, loc, map, ThingPlaceMode.Near, null,
                    // Try to put it down not in a storage building:
                    delegate(IntVec3 newLoc)
                    {
                        // validator
                        foreach (var t in map.thingGrid.ThingsListAtFast(newLoc))
                        {
                            if (t is Building_Storage) { return false; }
                        }

                        return true;
                    }
                ))
            {
                GenSpawn.Spawn(thing, loc, map); // Failed to find spot: so it WILL go back into Deep Storage!
                //                                  if non-DS using this, who knows/cares? It'll go somewhere. Probably.
            }

            if (!thing.Spawned || thing.Position == loc)
            {
                Messages.Message(
                    "You have filled the map.", // no translation for anyone crazy enuf to do this.
                    new LookTargets(loc, map), MessageTypeDefOf.NegativeEvent
                );
            }
        }
    }

    /* End itab */
    /* Now make the itab open automatically! */
    /*   Thanks to Falconne for doing this in ImprovedWorkbenches, and showing how darn useful it is! */
    [HarmonyPatch(typeof(Selector), "Select")]
    public static class OpenDSTabOnSelect
    {
        public static void Postfix(Selector instance)
        {
            if (instance.NumSelected != 1) { return; }
            var t = instance.SingleSelectedThing;

            if (t == null) { return; }

            if (!(t is ThingWithComps)) { return; }
            var cds = t.TryGetComp<CompDeepStorage>();

            if (cds == null) { return; }
            // Off to a good start; it's a DSU
            // Check to see if a tab is already open.
            var pane               = (MainTabWindow_Inspect) MainButtonDefOf.Inspect.TabWindow;
            var alreadyOpenTabType = pane.OpenTabType;

            if (alreadyOpenTabType != null)
            {
                var listOfTabs = t.GetInspectTabs();

                foreach (var x in listOfTabs)
                {
                    if (x.GetType() == alreadyOpenTabType)
                    {
                        // Misses any subclassing?
                        return; // standard Selector behavior should kick in.
                    }
                }
            }
            // If not, open ours!
            // TODO: ...make this happen for shelves, heck, any storage buildings?
            ITab tab = null;

            /* If there are no items stored, default intead to settings (preferably with note about being empty?) */
            // If we find a stored item, open Contents tab:
            // TODO: Make storage settings tab show label if it's empty
            if (t.Spawned && t is IStoreSettingsParent && t is ISlotGroupParent)
            {
                foreach (var c in ((ISlotGroupParent) t).GetSlotGroup().CellsList)
                {
                    var l = t.Map.thingGrid.ThingsListAt(c);

                    foreach (var tmp in l)
                    {
                        if (tmp.def.EverStorable(false))
                        {
                            goto EndLoop;
                            // Seriously?  C# doesn't have "break 2;"?
                        }
                    }
                }
                tab = t.GetInspectTabs().OfType<ITab_Storage>().First();
            }
        EndLoop:

            if (tab == null) { tab = t.GetInspectTabs().OfType<TabDeepStorageInventory>().First(); }

            if (tab == null)
            {
                Log.Error("LWM Deep Storage object " + t + " does not have an inventory tab?");

                return;
            }
            tab.OnOpen();

            if (tab is TabDeepStorageInventory) { pane.OpenTabType = typeof(TabDeepStorageInventory); }
            else { pane.OpenTabType                                = typeof(ITab_Storage); }
        }
    } // end patch of Select to open ITab
}
