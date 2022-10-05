namespace DeepStorage.Cache
{
#region
    using System.Collections.Generic;

    using Verse;
#endregion

    public class StackableThingComparer : IEqualityComparer<Thing>
    {
        public static StackableThingComparer _instance = new StackableThingComparer();

        private StackableThingComparer() { }

    #region Implementation of IEqualityComparer<in Thing>
        public bool Equals(Thing x, Thing y)
        {
            if (x == y) { return true; }

            if (x == null || y == null) { return false; }

            return x.CanStackWith(y);
        }

        public int GetHashCode(Thing obj) => (obj.def.GetHashCode() * 397) ^ (obj.Stuff?.GetHashCode() ?? 0);
    #endregion
    }
}
