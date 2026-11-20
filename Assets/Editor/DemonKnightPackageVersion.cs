#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "sync-complete-replace";
    public const string GitHint = "force-delete-sync-compat-cs1061-guard";
    public const string BuiltUtc = "2026-11-20T15:00:00Z";
}
#endif
