param([string] $options, [switch] $sign, [switch] $mocksign, [switch] $rebuild)

if ($rebuild) {
    $buildtarget = "Rebuild"
} else {
    $buildtarget = "Build"
}

if (-not (get-command msbuild -EA 0)) {
    Write-Error "Visual Studio build tools are required."
    exit 1
}

$projectDir = Split-Path -parent $MyInvocation.MyCommand.Definition

$buildroot = $projectDir
while ((Test-Path $buildroot) -and -not (Test-Path "$buildroot\build.root")) {
    $buildroot = (Split-Path -Parent $buildroot)
}
Write-Output "Build Root: $buildroot"

if (-not $outdir) {
    $outdir = "$projectDir"
}
Write-Output "Writing output MSIs to $outdir"

$originalbuildtarget = $buildtarget
if ($sign -or $mocksign) {
    $buildtarget = "BuildVSTestHost"
}

msbuild $projectDir\Installer\Installer.wixproj `
    /fl /flp:logfile="$projectDir\VSTestHost.build.log" `
    /v:m `
    /nologo `
    /t:$buildtarget `
    /p:VSTestHostTarget=$originalbuildtarget `
    /p:Configuration=Release `
    $options

if (-not $?) {
    Throw "Build failed"
}

if ($sign -or $mocksign) {
    Write-Output "Submitting signing job"
    if ($sign) {
        Import-Module -force $buildroot\Build\BuildReleaseHelpers.psm1
    } else {
        Import-Module -force $buildroot\Build\BuildReleaseMockHelpers.psm1
    }
    
    $approvers = "smortaz", "dinov", "stevdo", "pminaev", "gilbertw", "huvalo", "sitani", "jinglou", "crwilcox"
    $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})

    $dllfiles = @(Get-ChildItem "$projectDir\VSTestHost\bin\Release_*\Microsoft.VisualStudioTools.VSTestHost.*.dll" | %{ @{path=$_.FullName; name=$_.Name} })
    $destdir = "$outdir\VSTestHost\bin\SignedBinaries"
    $dlljob = begin_sign_files $dllfiles $destdir $approvers "VS Test Host" "http://pytools.codeplex.com" `
                               "VS Test Host" "Visual Studio; test" "authenticode;strongname"

    end_sign_files $dlljob
    
    Write-Output "Rebuilding MSI with signed binaries"
    
    msbuild $projectDir\Installer\Installer.wixproj `
        /fl /flp:logfile="$projectDir\VSTestHost.build_signed.log" `
        /v:m `
        /nologo `
        /t:$originalbuildtarget `
        /p:VSTestHostSignedBinariesPath=$destdir `
        /p:Configuration=Release

    if (-not $?) {
        Throw "Rebuild failed"
    }
    
    Write-Output "Submitting MSI signing job"
    $msifiles = @(@{path="$projectDir\Installer\bin\Release\VSTestHost.msi"; name="VSTestHost.msi"})
    $msijob = begin_sign_files $msifiles $outdir $approvers "VS Test Host Installer" "http://pytools.codeplex.com" `
                               "VS Test Host" "Visual Studio; test" "authenticode"
    
    end_sign_files $msijob
} else {
    Copy-Item "$projectDir\Installer\bin\Release\VSTestHost.msi" $outdir
}

Write-Output ""
Write-Output " *"
Write-Output " * Final MSI is at $(gci $outdir\*.msi)"
Write-Output " *"
Write-Output ""
