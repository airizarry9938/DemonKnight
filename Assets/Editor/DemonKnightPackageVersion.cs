#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "setnight-sync-cachebust";
    public const string GitHint = "raw-cdn-cachebust-no-setnight-call";
    public const string BuiltUtc = "2026-11-20T02:00:00Z";
}
#endif
