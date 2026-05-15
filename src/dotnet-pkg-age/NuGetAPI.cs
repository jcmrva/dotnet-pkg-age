using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Common;

namespace DotnetPkgAge;

public class NuGetAPI
{
    public static async Task<bool> GetPackageInfo(string packageName, string version, int minAgeDays)
    {
        if (Cache.TryGet(packageName, version, out var cachedPublished))
            return (DateTimeOffset.Now - cachedPublished).TotalDays >= minAgeDays;

        var cache = new SourceCacheContext();
        var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repo.GetResourceAsync<PackageMetadataResource>();
        var metadata = await resource.GetMetadataAsync(
            packageName, includePrerelease: false, includeUnlisted: false, cache, NullLogger.Instance, CancellationToken.None);

        var match = metadata.FirstOrDefault(pkg => pkg.Identity.Version.ToString() == version);
        if (match?.Published is { } published)
        {
            Cache.Set(packageName, version, published);
            return (DateTimeOffset.Now - published).TotalDays >= minAgeDays;
        }

        return false;
    }
}
