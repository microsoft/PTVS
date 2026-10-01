param(
    [Parameter(Mandatory = $true)]
    [string]$SetupPath,
    [string]$SignToolPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!$SignToolPath) {
    $sdkBinPath = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $signTools = @(Get-ChildItem -LiteralPath $sdkBinPath -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($signTools.Count -eq 0) {
        throw "Cannot find Windows SDK x64 signtool.exe under '$sdkBinPath'."
    }
    $SignToolPath = $signTools[0]
}
if (!(Test-Path -LiteralPath $SignToolPath -PathType Leaf)) {
    throw "SignTool was not found at '$SignToolPath'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(
    @{ Name = 'Microsoft.PythonTools.Django.Templates'; FontCount = 1 },
    @{ Name = 'Microsoft.PythonTools.Web.Templates'; FontCount = 3 }
)
$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('PTVS-FontVerification-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPath | Out-Null
try {
    foreach ($package in $packages) {
        $packagePattern = '^' + [regex]::Escape($package.Name) + '(\.Vsix)?\.vsix$'
        $packageFiles = @(Get-ChildItem -LiteralPath $SetupPath -File -Filter '*.vsix' |
            Where-Object { $_.Name -match $packagePattern })
        if ($packageFiles.Count -ne 1) {
            throw "Expected exactly one '$($package.Name)' VSIX in '$SetupPath', found $($packageFiles.Count)."
        }
        $packagePath = $packageFiles[0].FullName
        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $fonts = @($archive.Entries | Where-Object { $_.Name -like '*.ttf' })
            if ($fonts.Count -ne $package.FontCount) {
                throw "Expected $($package.FontCount) TTF files in '$packagePath', found $($fonts.Count)."
            }
            for ($index = 0; $index -lt $fonts.Count; $index++) {
                $font = $fonts[$index]
                # Use generated names so archive paths cannot escape the temporary directory.
                $fontPath = Join-Path $temporaryPath ("$($package.Name)-$index.ttf")
                [IO.Compression.ZipFileExtensions]::ExtractToFile($font, $fontPath)
                Write-Host "Verifying embedded font: $packagePath -> $($font.FullName)"
                & $SignToolPath verify /pa /all /v $fontPath
                if ($LASTEXITCODE -ne 0) {
                    throw "Signature verification failed for '$($font.FullName)' in '$packagePath' (SignTool exit code $LASTEXITCODE)."
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    Write-Host 'All four packaged template font signatures verified successfully.'
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Recurse -Force
}
