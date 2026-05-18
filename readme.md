# dotnet-pkg-age

Pulls metadata from nuget.org to determine how recently a package was published, mostly for compliance with public package cooldown rules.

- Check one package or everything in a solution.
- Package name, version, and publish date are automatically cached.
- Bypass list enables hotfixes to avoid the minimum age check.

## [Install](https://www.nuget.org/packages/dotnet-pkg-age/)

`dotnet tool install dotnet-pkg-age`

Requires .NET SDK 8.0 or greater.

## Examples

`package` - check a single package, version, and minimum age:

```console
dotnet pkg-age package xunit 2.9.3 10
```

`bulk` - check all packages in a solution's `packages.lock.json` files:

```console
dotnet pkg-age bulk 5 --lock-files
```

`bulk` - check all packages in a `Directory.Packages.props` file and output json:

```console
dotnet pkg-age bulk 5 --props -f json
```

`cache` - clear the entire cache:

```console
dotnet pkg-age cache --clear-all
```

The GitHub wiki has more examples and documentation.

## Bypass list

Specific package versions can be excluded from the age check by adding them to `.config/pkg-age-bypass.json` at the repo root. This is intended for security hotfixes where a version must be adopted immediately regardless of age.

```json
{
  "NuGet.Versioning@7.6.0": "critical fix, approved by management"
}
```

Use `--ignore-bypass` to override the bypass list and enforce the age check regardless.

## Key Dependencies

- `System.CommandLine` for CLI arg parsing
- `NuGet.Protocol` & `NuGet.Versioning` for working with the NuGet API
- `Xunit` for automated testing

## TODO

- **Build integration** - Fail builds if a new package version is too new
- **Custom NuGet feed** - `--source` option to target private or internal feeds instead of nuget.org (maybe not)
