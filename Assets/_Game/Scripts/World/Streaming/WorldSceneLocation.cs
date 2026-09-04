namespace ZZ
{
    /// <summary>
    /// Identifies the persistent world and each streamable world region.
    /// Values 6-8 extend the legacy region mapping for R01's per-Area streaming
    /// units (Area01SubArea00 stays R01 A01); existing values never reorder.
    /// </summary>
    public enum WorldSceneLocation
    {
        PersistentWorld = 0,
        Area01SubArea00 = 1,
        Area01SubArea01 = 2,
        Area01SubArea02 = 3,
        Area01SubArea03 = 4,
        Area01SubArea04 = 5,
        Area01SubArea05 = 6,
        Area01SubArea06 = 7,
        Area01SubArea07 = 8
    }
}
