#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "fix-cs0103-cs0414-wire-flags";
    public const string GitHint = "Somerset-using-System-Merlin-drop-unused-wire-bools";
    public const string BuiltUtc = "2026-11-21T18:30:00Z";
}
#endif
