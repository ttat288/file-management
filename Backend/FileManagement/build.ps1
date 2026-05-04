param(
  [ValidateSet('Debug', 'Release')]
  [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$overlaySdksPath = Join-Path $repoRoot '.msbuild_sdks_overlay'

function New-MsbuildSdksOverlay {
  param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot,
    [Parameter(Mandatory = $true)]
    [string] $OverlayPath
  )

  $globalJsonPath = Join-Path $RepoRoot 'global.json'
  if (-not (Test-Path $globalJsonPath)) {
    throw "Missing global.json at repo root: $globalJsonPath"
  }

  $sdkVersion = ((Get-Content $globalJsonPath -Raw) | ConvertFrom-Json).sdk.version
  if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "global.json does not specify sdk.version"
  }

  $dotnetSdksPath = Join-Path $env:ProgramFiles "dotnet\\sdk\\$sdkVersion\\Sdks"
  if (-not (Test-Path $dotnetSdksPath)) {
    throw "Installed .NET SDK folder not found: $dotnetSdksPath"
  }

  if (Test-Path $OverlayPath) {
    Remove-Item -LiteralPath $OverlayPath -Recurse -Force
  }
  New-Item -ItemType Directory -Path $OverlayPath | Out-Null

  # Link all built-in SDKs from the installed .NET SDK into the overlay.
  Get-ChildItem -LiteralPath $dotnetSdksPath -Directory | ForEach-Object {
    $linkPath = Join-Path $OverlayPath $_.Name
    New-Item -ItemType Junction -Path $linkPath -Target $_.FullName | Out-Null
  }

  # Add missing workload locator SDKs (minimal/no-op versions) so MSBuild doesn't fail with MSB4276.
  $autoImportSdk = Join-Path $OverlayPath 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\\Sdk'
  New-Item -ItemType Directory -Path $autoImportSdk -Force | Out-Null
  Set-Content -LiteralPath (Join-Path $autoImportSdk 'Sdk.props') -Value '<Project />' -Encoding UTF8
  Set-Content -LiteralPath (Join-Path $autoImportSdk 'Sdk.targets') -Value '<Project />' -Encoding UTF8
  Set-Content -LiteralPath (Join-Path $autoImportSdk 'AutoImport.props') -Value '<Project />' -Encoding UTF8

  $manifestSdk = Join-Path $OverlayPath 'Microsoft.NET.SDK.WorkloadManifestTargetsLocator\\Sdk'
  New-Item -ItemType Directory -Path $manifestSdk -Force | Out-Null
  Set-Content -LiteralPath (Join-Path $manifestSdk 'Sdk.props') -Value '<Project />' -Encoding UTF8
  Set-Content -LiteralPath (Join-Path $manifestSdk 'Sdk.targets') -Value '<Project />' -Encoding UTF8
  Set-Content -LiteralPath (Join-Path $manifestSdk 'WorkloadManifest.targets') -Value '<Project />' -Encoding UTF8

  return $OverlayPath
}

$env:MSBuildSDKsPath = New-MsbuildSdksOverlay -RepoRoot $repoRoot -OverlayPath $overlaySdksPath

dotnet build (Join-Path $PSScriptRoot 'FileManagement.sln') -c $Configuration
exit $LASTEXITCODE
