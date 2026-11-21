#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "fix-cs0117-sanitize-on-fusion";
    public const string GitHint = "SanitizeBaseUnitFusionFlags-on-CardFusionSystem-CS0117";
    public const string BuiltUtc = "2026-11-21T19:20:00Z";
}
#endif
