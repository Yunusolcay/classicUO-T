[CmdletBinding()]
param(
    [switch]$NativeAot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Get-Command git, dotnet -ErrorAction Stop | Out-Null
Push-Location $PSScriptRoot
try {
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
        throw "This checkout requires an active .NET 10 SDK. Selected SDK: $sdkVersion"
    }

    # Use the exact submodule revisions recorded by this checkout, never --remote.
    & git -c url.https://.insteadOf=git:// submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) { throw 'Submodule initialization failed.' }
    $submoduleStatus = @(& git submodule status --recursive)
    if ($LASTEXITCODE -ne 0 -or @($submoduleStatus | Where-Object { $_ -match '^[-+U]' }).Count -gt 0) {
        throw 'Submodules are missing, conflicted, or at unexpected revisions.'
    }

    $nativeLibraries = @('SDL3.dll', 'FNA3D.dll', 'FAudio.dll', 'libtheorafile.dll', 'zlib.dll')
    foreach ($library in $nativeLibraries) {
        if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot "external\x64\$library") -PathType Leaf)) {
            throw "Required native library is missing: external/x64/$library"
        }
    }

    $projectPath = Join-Path $PSScriptRoot 'src\ClassicUO.Client\ClassicUO.Client.csproj'
    $logDirectory = Join-Path $PSScriptRoot 'artifacts\build'
    $mode = if ($NativeAot) { 'nativeaot' } else { 'managed' }
    $outputDirectory = Join-Path $PSScriptRoot "bin\dist\win-x64-$mode"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $aotProperty = "/p:PublishAot=$($NativeAot.IsPresent.ToString().ToLowerInvariant())"
    $commonArguments = @($projectPath, '-c', 'Release', '-r', 'win-x64', $aotProperty, '--nologo', '-v', 'minimal')

    # Publish a directly executable client; the optional plugin bootstrap is separate.
    & dotnet build @commonArguments "/flp:LogFile=$logDirectory/build-$mode.log;Verbosity=normal"
    if ($LASTEXITCODE -ne 0) { throw "Release build failed. See artifacts/build/build-$mode.log." }

    & dotnet publish @commonArguments --self-contained true -o $outputDirectory "/flp:LogFile=$logDirectory/publish-$mode.log;Verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed. See artifacts/build/publish-$mode.log. NativeAOT additionally requires Visual Studio C++ tools and a Windows SDK."
    }

    foreach ($library in @('cuo.exe') + $nativeLibraries) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputDirectory $library) -PathType Leaf)) {
            throw "Publish output is incomplete: $library"
        }
    }
    Write-Host "Release Windows x64 ($mode): $(Join-Path $outputDirectory 'cuo.exe')"
}
finally {
    Pop-Location
}
