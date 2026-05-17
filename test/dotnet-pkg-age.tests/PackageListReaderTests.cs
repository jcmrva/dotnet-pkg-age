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
        Assert.Contains(("Newtonsoft.Json", "13.0.3", "Direct"), result);
        Assert.Contains(("SomeDep", "1.0.0", "Transitive"), result);
    }

    [Fact]
    public void ReadPackagesLockJson_ReturnsCorrectType()
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

        Assert.Equal("Direct", result.Single(r => r.Package == "Newtonsoft.Json").Type);
        Assert.Equal("Transitive", result.Single(r => r.Package == "SomeDep").Type);
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
        Assert.Contains(("Newtonsoft.Json", "13.0.3", "Direct"), result);
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
        Assert.Contains(("Newtonsoft.Json", "13.0.3", "Direct"), result);
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

    // --- FindDirectoryPackagesProps ---

    [Fact]
    public void FindDirectoryPackagesProps_FindsFile_InStartDir()
    {
        WriteFile("Directory.Packages.props", "<Project />");

        var result = PackageListReader.FindDirectoryPackagesProps(_tempDir);

        Assert.Equal(Path.Combine(_tempDir, "Directory.Packages.props"), result);
    }

    [Fact]
    public void FindDirectoryPackagesProps_FindsFile_InParentDir()
    {
        WriteFile("Directory.Packages.props", "<Project />");
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "src", "MyProject")).FullName;

        var result = PackageListReader.FindDirectoryPackagesProps(subDir);

        Assert.Equal(Path.Combine(_tempDir, "Directory.Packages.props"), result);
    }

    [Fact]
    public void FindDirectoryPackagesProps_ReturnsNull_WhenNotFound()
    {
        var result = PackageListReader.FindDirectoryPackagesProps(_tempDir);

        Assert.Null(result);
    }

    // --- FindPackagesLockFiles ---

    [Fact]
    public void FindPackagesLockFiles_FindsFiles_InSubdirectories()
    {
        var proj1 = Directory.CreateDirectory(Path.Combine(_tempDir, "src", "ProjectA")).FullName;
        var proj2 = Directory.CreateDirectory(Path.Combine(_tempDir, "src", "ProjectB")).FullName;
        File.WriteAllText(Path.Combine(proj1, "packages.lock.json"), "{}");
        File.WriteAllText(Path.Combine(proj2, "packages.lock.json"), "{}");

        var result = PackageListReader.FindPackagesLockFiles(_tempDir);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindPackagesLockFiles_ExcludesFilesInObjDirectory()
    {
        var proj = Directory.CreateDirectory(Path.Combine(_tempDir, "src", "Project")).FullName;
        var obj = Directory.CreateDirectory(Path.Combine(proj, "obj")).FullName;
        File.WriteAllText(Path.Combine(proj, "packages.lock.json"), "{}");
        File.WriteAllText(Path.Combine(obj, "packages.lock.json"), "{}");

        var result = PackageListReader.FindPackagesLockFiles(_tempDir);

        Assert.Single(result);
        Assert.DoesNotContain(result, f => f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void FindPackagesLockFiles_ReturnsEmpty_WhenNoneExist()
    {
        var result = PackageListReader.FindPackagesLockFiles(_tempDir);

        Assert.Empty(result);
    }
}
