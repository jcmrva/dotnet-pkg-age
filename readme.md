# dotnet-pkg-age

Connects to nuget.org to determine how recently a package was published, mostly for compliance with organizational cooldown rules.

## [Install](https://www.nuget.org/packages/dotnet-pkg-age/)

`dotnet tool install dotnet-pkg-age`

Requires .NET SDK 8.0 or greater.

## Usage

```bash
Description:
  A dotnet tool to check the age of NuGet packages.

Usage:
  dotnet-pkg-age [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information

Commands:
  package <name> <version> <min-age-days>  Check a specific package
```

`package` command:

```bash
Usage:
  dotnet-pkg-age package <name> <version> <min-age-days> [options]

Arguments:
  <name>          The package to check
  <version>       The package version to check
  <min-age-days>  Desired minimum age of the package in days

Options:
  -c, --cache-file <cache-file>  Path to the cache file (default: ~/.dotnet-pkg-age/cache.json)
```

## Examples

```bash
dotnet pkg-age package System.CommandLine 2.0.7 10
```

From this repo:

```bash
dotnet run --project ./src/dotnet-pkg-age/ -- package System.CommandLine 2.0.7 10

# or install it

dotnet tool install --local dotnet-pkg-age --add-source ./src/dotnet-pkg-age/nupkg
dotnet pkg-age package System.CommandLine 2.0.7 10
```

## Dependencies

- `System.CommandLine` for CLI arg parsing
- `NuGet.Protocol` & `NuGet.Versioning` for working with the NuGet API
- `xunit` for automated testing

## TODO

- **Exit codes** - return a non-zero exit code when a package does not meet the age requirement, so the tool can gate CI pipelines directly (e.g. `dotnet pkg-age package ... || exit 1`)
- **Bulk input** - accept a `Directory.Packages.props` or `packages.lock.json` file and check all listed packages in one run (*proj files not planned)
- **Bypass list** - enter specific package versions to skip the age check, typically for security hotfixes (package-age-bypass.json) `{ "System.CommandLine@2.0.8": "reason..." }`
- **Custom NuGet feed** - `--source` option to target private or internal feeds instead of nuget.org (maybe not)
- **Prerelease support** - opt-in flag to include prerelease versions in the lookup
- **JSON output** - `--format json` for scripting and downstream tooling, provide json schema
- **Cache management** - `cache clear` subcommand to invalidate all or specific entries
