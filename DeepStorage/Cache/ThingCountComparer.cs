namespace DeepStorage.Cache
{
#region
    using System.Collections.Generic;

    using Verse;
#endregion

    public class ThingCountComparer : EqualityComparer<Thing>
    {
        public static ThingCountComparer _instance = new ThingCountComparer();

        private ThingCountComparer() { }

    #region Overrides of EqualityComparer<Thing>
        public override bool Equals(Thing x, Thing y)
        {
            if (object.ReferenceEquals(x, y)) { return true; }

            if (x is null || y is null) { return false; }

            return x.CanStackWith(y) && x.stackCount == y.stackCount;
        }

        public override int GetHashCode(Thing obj) => (obj.thingIDNumber * 397) ^ obj.stackCount;
    #endregion
    }
}
