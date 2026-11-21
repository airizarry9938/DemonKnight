#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "sync-skip-self-hover-rmb";
    public const string GitHint = "Skip-SyncHost-overwrite-hover-preview-RMB-menu";
    public const string BuiltUtc = "2026-11-21T17:05:00Z"; // America/Denver morning ship
}
#endif
