param ($vstarget, $source, [switch] $clean, [switch] $full)

# This is the list of packages we require to build, and the version to use for each supported $vstarget
$packages = @(
    @{ name="Microsoft.VSSDK.BuildTools"; version=@{ "14.0"="14.0.23205"; "15.0"="15.0.25022-Dev15CTP1" }; required=$true },
    @{ name="Newtonsoft.Json"; version=@{ "14.0"="6.0.8"; "15.0"="6.0.8" }; required=$true },
    @{ name="MicroBuild.Core"; version=@{ "14.0"="0.2.0"; "15.0"="0.2.0" }; required=$false }
)

if ($full) {
    $packages += @(
        @{ name="Wix"; version=@{ "14.0"="3.9.2.1"; "15.0"="3.9.2.1" }; required=$false }
    )
}

"Restoring Packages"

if (-not $vstarget) {
    $vstarget = "14.0"
} elseif ($vstarget.ToString() -match "^\d\d$") {
    $vstarget = "$vstarget.0"
}

$buildroot = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Split-Path -Parent
pushd "$buildroot\Build"
if ($source) {
    .\nuget.exe sources add -Name PreBuildSource -Source $source
}
if ($env:BUILD_BINARIESDIRECTORY) {
    $outdir = "${env:BUILD_BINARIESDIRECTORY}"
} else {
    $outdir = "$buildroot\BuildOutput"
}


if ($clean) {
    $packages.name | %{ rmdir -r -fo -ea 0 "$outdir\$_" }
}

$packages | %{
    $arglist = "install", $_.name, "-Version", $_.version[$vstarget], "-ExcludeVersion", "-OutputDirectory", $outdir
    if ($_.required) {
        Start-Process -Wait -NoNewWindow .\nuget.exe -ErrorAction Stop -ArgumentList $arglist
    } else {
        Start-Process -Wait -NoNewWindow .\nuget.exe -ErrorAction Continue -ArgumentList $arglist
    }
}

if ($source) {
    .\nuget.exe sources remove -Name PreBuildSource
}
popd
