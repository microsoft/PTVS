[CmdletBinding()]
param([switch] $release, [switch] $mockrelease, [switch] $upload)

$versions = @( `
    @{cmd="-2.7"; suffix="win-amd64-2.7" }, `
    @{cmd="-2.7-32"; suffix="win32-2.7" } `
)

foreach ($ver in $versions) {
    py $($ver.cmd) -c "import setuptools, wheel"
    if (-not $?) {
        Write-Error -EA:Stop "Python interpreter is not configured."
    }
}

$buildroot = (Split-Path -Parent $MyInvocation.MyCommand.Definition)
while ((Test-Path $buildroot) -and -not (Test-Path "$buildroot\build.root")) {
    $buildroot = (Split-Path -Parent $buildroot)
}
Write-Output "Build Root: $buildroot"

pushd $buildroot\Python\Product\PyKinect\PyKinect

if (Get-Command tf -EA 0) {
    tf edit * /r
}

try {
    $signedbuild = $release -or $mockrelease
    if ($signedbuild) {
        $approvers = "smortaz", "dinov", "stevdo", "pminaev", "gilbertw", "huvalo", "sitani", "crwilcox"
        $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})
        
        $projectName = "PyKinect"
        $projectUrl = "http://pytools.codeplex.com"
        $projectKeywords = "PyKinect; Visual Studio; Python; Kinect"

        Push-Location (Split-Path -Parent $MyInvocation.MyCommand.Definition)
        if ($mockrelease) {
            Set-Variable -Name DebugPreference -Value "Continue" -Scope "global"
            Import-Module -force $buildroot\Common\Setup\ReleaseMockHelpers.psm1
        } else {
            Import-Module -force $buildroot\Common\Setup\ReleaseHelpers.psm1
        }
        Pop-Location
    }

    if ($release) {
        $repo = "pypi"
    } else {
        $repo = "mock"
    }

    $default = $($versions[0].cmd)

    "build", "dist", "pykinect.egg-info" | ?{ Test-Path $_ } | %{ rmdir -r -fo $_ }

    $jobs = @()

    if ($upload) {
        py $default setup.py sdist register -r $repo upload -r $repo
    } else {
        py $default setup.py sdist
    }

    if ($signedbuild) {
        foreach ($ver in $versions) {
            py $($ver.cmd) setup.py bdist_wheel
            
            $item = Get-Item ".\build\lib.$($ver.suffix)\pykinect\audio\PyKinectAudio.dll"
            "Signing $item"
            $jobs += begin_sign_files @(@{path=$item.FullName; name=$item.Name}) `
                                      $item.Directory `
                                      $approvers `
                                      $projectName $projectUrl `
                                      "lib.$($ver.suffix)\pykinect\audio\PyKinectAudio.dll" $projectKeywords `
                                      "authenticode"
        }

        end_sign_files $jobs
    }

    foreach ($ver in $versions) {
        if ($upload) {
            py $($ver.cmd) setup.py bdist_wheel upload -r $repo
        } else {
            py $($ver.cmd) setup.py bdist_wheel
        }
    }
} finally {
    if (Get-Command tfpt -EA 0) {
        tfpt uu /noget /noprompt . /r
    }
    
    popd
}
