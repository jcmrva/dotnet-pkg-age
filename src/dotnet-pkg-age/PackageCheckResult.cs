using System;

namespace DotnetPkgAge;

public record PackageCheckResult(
    string Package,
    string Version,
    int MinAgeDays,
    DateTimeOffset? Published)
{
    public int? AgeDays => Published.HasValue
        ? (int)(DateTimeOffset.Now - Published.Value).TotalDays
        : null;

    public bool MeetsRequirement => AgeDays.HasValue && AgeDays.Value >= MinAgeDays;
}
