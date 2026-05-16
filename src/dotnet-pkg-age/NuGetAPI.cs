using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Xml.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Common;

namespace DotnetPkgAge;

public class NuGetAPI
{
    private static readonly HttpClient HttpClient = new();
    private const int BatchSize = 20;

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace D = "http://schemas.microsoft.com/ado/2007/08/dataservices";
    private static readonly XNamespace M = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

    public static async Task<PackageCheckResult> GetPackageInfo(string packageName, string version, int minAgeDays)
    {
        if (Cache.TryGet(packageName, version, out var cachedPublished))
            return new PackageCheckResult(packageName, version, minAgeDays, cachedPublished);

        var cache = new SourceCacheContext();
        var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repo.GetResourceAsync<PackageMetadataResource>();
        var metadata = await resource.GetMetadataAsync(
            packageName, includePrerelease: false, includeUnlisted: false, cache, NullLogger.Instance, CancellationToken.None);

        var match = metadata.FirstOrDefault(pkg => pkg.Identity.Version.ToString() == version);
        if (match?.Published is { } published)
        {
            Cache.Set(packageName, version, published);
            return new PackageCheckResult(packageName, version, minAgeDays, published);
        }

        return new PackageCheckResult(packageName, version, minAgeDays, null);
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

        foreach (var chunk in toFetch.Chunk(BatchSize))
        {
            var filter = string.Join(" or ", chunk.Select(p =>
                $"(Id eq '{ODataEscape(p.Package)}' and NormalizedVersion eq '{ODataEscape(p.Version)}')"));

            var url = "https://www.nuget.org/api/v2/Packages()?" +
                        $"$filter={Uri.EscapeDataString(filter)}" +
                        "&$select=Id,NormalizedVersion,Published";

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var props = entry.Element(M + "properties");
                var id = props?.Element(D + "Id")?.Value;
                var version = props?.Element(D + "NormalizedVersion")?.Value;
                var publishedStr = props?.Element(D + "Published")?.Value;

                if (id is null || version is null) continue;
                if (!DateTimeOffset.TryParse(publishedStr, out var published)) continue;

                result[(id, version)] = published;
                Cache.Set(id, version, published);
            }
        }

        foreach (var pkg in toFetch.Where(p => !result.ContainsKey(p)))
            result[pkg] = null;

        return result;
    }

    private static string ODataEscape(string value) => value.Replace("'", "''");
}
