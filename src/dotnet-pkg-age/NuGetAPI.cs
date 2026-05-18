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
    private const int MaxConcurrency = 8;
    private const string FeedUrl = "https://api.nuget.org/v3/index.json";

    public static async Task<PackageCheckResult> GetPackageInfo(string packageName, string version, int minAgeDays)
    {
        if (Cache.TryGet(packageName, version, out var cachedPublished))
            return new PackageCheckResult(packageName, version, minAgeDays, cachedPublished);

        try
        {
            var cache = new SourceCacheContext();
            var repo = Repository.Factory.GetCoreV3(FeedUrl);
            var resource = await repo.GetResourceAsync<PackageMetadataResource>();
            var metadata = await resource.GetMetadataAsync(
                packageName, includePrerelease: true, includeUnlisted: false, cache, NullLogger.Instance, CancellationToken.None);

            var match = metadata.FirstOrDefault(pkg => pkg.Identity.Version.ToString() == version);
            if (match?.Published is { } published)
            {
                Cache.Set(packageName, version, published);
                return new PackageCheckResult(packageName, version, minAgeDays, published);
            }

            return new PackageCheckResult(packageName, version, minAgeDays, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve metadata for {packageName} {version}: {ex.Message}", ex);
        }
    }

    public static async Task<IReadOnlyDictionary<(string Package, string Version), DateTimeOffset?>> GetPublishedDatesBatch(
        IEnumerable<(string Package, string Version)> packages,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<(string, string), DateTimeOffset?>();
        var toFetch = new List<(string Package, string Version)>();

        foreach (var pkg in packages)
        {
            if (Cache.TryGet(pkg.Package, pkg.Version, out var cached))
                result[pkg] = cached;
            else
                toFetch.Add(pkg);
        }

        if (toFetch.Count == 0) return result;

        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var sourceCache = new SourceCacheContext();
        PackageMetadataResource resource;
        try
        {
            var repo = Repository.Factory.GetCoreV3(FeedUrl);
            resource = await repo.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to NuGet feed: {ex.Message}", ex);
        }

        var tasks = toFetch.Select(async pkg =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var metadata = await resource.GetMetadataAsync(
                    pkg.Package, includePrerelease: true, includeUnlisted: false,
                    sourceCache, NullLogger.Instance, cancellationToken);

                var match = metadata.FirstOrDefault(m => m.Identity.Version.ToString() == pkg.Version);
                return (pkg, Date: match?.Published);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: failed to fetch {pkg.Package} {pkg.Version}: {ex.Message}");
                return (pkg, Date: (DateTimeOffset?)null);
            }
            finally
            {
                semaphore.Release();
            }
        });

        foreach (var (pkg, date) in await Task.WhenAll(tasks))
        {
            if (date is { } published)
            {
                Cache.Set(pkg.Package, pkg.Version, published);
                result[pkg] = published;
            }
            else
            {
                result[pkg] = null;
            }
        }

        return result;
    }
}
