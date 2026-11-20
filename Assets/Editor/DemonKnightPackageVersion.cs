#if UNITY_EDITOR
/// <summary>Bump when shipping package updates so Diagnose can prove Unity loaded the new build.</summary>
public static class DemonKnightPackageVersion
{
    public const string Id = "sync-pin-jsdelivr";
    public const string GitHint = "never-jsdelivr-at-main-pin-commit-bootstrap";
    public const string BuiltUtc = "2026-11-20T15:10:00Z";
}
#endif
