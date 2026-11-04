namespace Incranalytic.App;

internal static class DistributionChannel
{
    public static bool IsStore
    {
        get
        {
#if INCRANALYTIC_STORE
            return true;
#else
            return false;
#endif
        }
    }
    public static string UpdateMenuLabel => IsStore ? "Microsoft Store updates…" : "Install / manage versions…";
    public static string AboutDistribution => IsStore
        ? "Microsoft Store edition. Updates are managed by Microsoft Store."
        : "Unsigned public development release.";
}
