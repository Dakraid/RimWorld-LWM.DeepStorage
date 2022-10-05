using RimWorld;
using Verse;
using UnityEngine;

namespace LWM.DeepStorage
{
	// ripped shamelessly from Dialog_RenameZone
	public class DialogRenameDsu : Dialog_Rename
	{
		public DialogRenameDsu(CompDeepStorage cds)
		{
			this._cds = cds;
			this.curName = cds.parent.Label;
		}

        // ... Actually, whatever, name it whatever you want.
        // But use "" to reset to default.
		protected override AcceptanceReport NameIsValid(string name)
		{
            if (name.Length == 0)
            {
                return true;
            }
            var result = base.NameIsValid(name);
			if (!result.Accepted)
			{
				return result;
			}
			return true;
		}

		protected override void SetName(string name)
		{
			this._cds._buildingLabel = name;
			Messages.Message("LWM_DSU_GainsName".Translate(this._cds.parent.def.label, _cds.parent.Label),
                             MessageTypeDefOf.TaskCompletion, false);
		}

		public override Vector2 InitialSize
		{
			get
			{
                var o=base.InitialSize;
                o.y+=50f;
                return o;
			}
		}

        public override void DoWindowContents(Rect inRect) {
            base.DoWindowContents(inRect);
            if (Widgets.ButtonText(new Rect(15f, inRect.height -35f -15f -50f, inRect.width-15f-15f, 35f), "ResetButton".Translate(),
                                   true,false,true)) {
                this.SetName("");
                Find.WindowStack.TryRemove(this, true);
            }

        }
        // TODO:  Make a nice button, eh?
        // override InitialSize to make it bigger
        // override DoWindowContents to add a new button on top of "Okay" that says "reset"?

		private CompDeepStorage _cds;
	}
}
