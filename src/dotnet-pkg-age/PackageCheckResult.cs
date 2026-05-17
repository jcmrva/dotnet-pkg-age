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

public record BulkPackageResult(
    string Package,
    string Version,
    string Status,
    int? AgeDays,
    string? BypassReason,
    string? DependencyType);

public record BulkSummary(int Passed, int Failed, int Bypassed, int NotFound);
