namespace DotnetPkgAge.Tests;

public class PackageListReaderTests : IDisposable
{
    private readonly string _tempDir;

    public PackageListReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // --- Directory.Packages.props ---

    [Fact]
    public void ReadDirectoryPackagesProps_ReturnsAllPackages()
    {
        var path = WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageVersion Include="System.CommandLine" Version="2.0.7" />
              </ItemGroup>
            </Project>
            """);

        var result = PackageListReader.ReadDirectoryPackagesProps(path);

        Assert.Equal(2, result.Count);
        Assert.Contains(("Newtonsoft.Json", "13.0.3"), result);
        Assert.Contains(("System.CommandLine", "2.0.7"), result);
    }

    [Fact]
    public void ReadDirectoryPackagesProps_SkipsEntries_MissingVersion()
    {
        var path = WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" />
                <PackageVersion Include="System.CommandLine" Version="2.0.7" />
              </ItemGroup>
            </Project>
            """);

        var result = PackageListReader.ReadDirectoryPackagesProps(path);

        Assert.Single(result);
        Assert.Contains(("System.CommandLine", "2.0.7"), result);
    }

    [Fact]
    public void ReadDirectoryPackagesProps_ReturnsEmpty_WhenNoPackageVersionElements()
    {
        var path = WriteFile("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """);

        var result = PackageListReader.ReadDirectoryPackagesProps(path);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadDirectoryPackagesProps_HandlesMultipleItemGroups()
    {
        var path = WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="System.CommandLine" Version="2.0.7" />
              </ItemGroup>
            </Project>
            """);

        var result = PackageListReader.ReadDirectoryPackagesProps(path);

        Assert.Equal(2, result.Count);
    }

    // --- packages.lock.json ---

    [Fact]
    public void ReadPackagesLockJson_ReturnsAllPackages_ByDefault()
    {
        var path = WriteFile("packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net8.0": {
                  "Newtonsoft.Json": { "type": "Direct", "resolved": "13.0.3" },
                  "SomeDep": { "type": "Transitive", "resolved": "1.0.0" }
                }
              }
            }
            """);

        var result = PackageListReader.ReadPackagesLockJson(path);

        Assert.Equal(2, result.Count);
        Assert.Contains(("Newtonsoft.Json", "13.0.3"), result);
        Assert.Contains(("SomeDep", "1.0.0"), result);
    }

    [Fact]
    public void ReadPackagesLockJson_FiltersToDirectOnly_WhenRequested()
    {
        var path = WriteFile("packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net8.0": {
                  "Newtonsoft.Json": { "type": "Direct", "resolved": "13.0.3" },
                  "SomeDep": { "type": "Transitive", "resolved": "1.0.0" }
                }
              }
            }
            """);

        var result = PackageListReader.ReadPackagesLockJson(path, directOnly: true);

        Assert.Single(result);
        Assert.Contains(("Newtonsoft.Json", "13.0.3"), result);
    }

    [Fact]
    public void ReadPackagesLockJson_DeduplicatesAcrossFrameworks()
    {
        var path = WriteFile("packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net8.0": {
                  "Newtonsoft.Json": { "type": "Direct", "resolved": "13.0.3" }
                },
                "net9.0": {
                  "Newtonsoft.Json": { "type": "Direct", "resolved": "13.0.3" }
                }
              }
            }
            """);

        var result = PackageListReader.ReadPackagesLockJson(path);

        Assert.Single(result);
    }

    [Fact]
    public void ReadPackagesLockJson_SkipsEntries_MissingResolved()
    {
        var path = WriteFile("packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net8.0": {
                  "Newtonsoft.Json": { "type": "Direct", "resolved": "13.0.3" },
                  "BadPackage": { "type": "Direct" }
                }
              }
            }
            """);

        var result = PackageListReader.ReadPackagesLockJson(path);

        Assert.Single(result);
        Assert.Contains(("Newtonsoft.Json", "13.0.3"), result);
    }

    [Fact]
    public void ReadPackagesLockJson_ReturnsEmpty_WhenNoDependencies()
    {
        var path = WriteFile("packages.lock.json", """
            { "version": 2, "dependencies": {} }
            """);

        var result = PackageListReader.ReadPackagesLockJson(path);

        Assert.Empty(result);
    }
}
