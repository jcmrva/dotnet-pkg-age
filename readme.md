# dotnet-pkg-age

## Usage

```text
Description:
  A dotnet tool to check the age of a NuGet package

Usage:
  dotnet-pkg-age [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information

Commands:
  package <name> <version> <min-age-days>  Check a specific package
```

## Examples

```bash
dotnet tool install dotnet-pkg-age
dotnet pkg-age package System.CommandLine 2.0.7 7
```

From this repo:

```bash
dotnet run --project .\src\dotnet-pkg-age\ -- package System.CommandLine 2.0.7 7

# or install it

dotnet tool install --local dotnet-pkg-age --add-source ./src/dotnet-pkg-age/nupkg
dotnet pkg-age package System.CommandLine 2.0.7 7
```
