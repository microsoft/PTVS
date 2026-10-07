param(
    [Parameter(Mandatory = $true)]
    [string]$SetupPath,
    [Parameter(Mandatory = $true)]
    [string]$TemplatePath,
    [Parameter(Mandatory = $true)]
    [string]$UnsignedTemplatePath,
    [Parameter(Mandatory = $true)]
    [string]$VerificationPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(
    @{
        Name = 'Microsoft.PythonTools.Django.Templates'
        TemplateKind = 'Django'
        FontCount = 1
        CatalogName = 'Microsoft.PythonTools.Django.Templates.Fonts.cat'
    },
    @{
        Name = 'Microsoft.PythonTools.Web.Templates'
        TemplateKind = 'Web'
        FontCount = 3
        CatalogName = 'Microsoft.PythonTools.Web.Templates.Fonts.cat'
    }
)
$payloadsPrepared = $false
New-Item -ItemType Directory -Path $VerificationPath | Out-Null
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
            $fonts = @($archive.Entries | Where-Object { $_.Name -like '*.ttf' } | Sort-Object Name)
            if ($fonts.Count -ne $package.FontCount) {
                throw "Expected $($package.FontCount) TTF files in '$packagePath', found $($fonts.Count)."
            }
            if (@($fonts | Where-Object { $_.Name -ine 'glyphicons-halflings-regular.ttf' }).Count -gt 0 -or
                @($fonts.FullName | Sort-Object -Unique).Count -ne $package.FontCount) {
                throw "Unexpected or duplicate font payload paths in '$packagePath'."
            }
            for ($index = 0; $index -lt $fonts.Count; $index++) {
                $font = $fonts[$index]
                # Use generated names so archive paths cannot escape the verification directory.
                $fontPath = Join-Path $VerificationPath ("$($package.Name)-$index.ttf")
                [IO.Compression.ZipFileExtensions]::ExtractToFile($font, $fontPath)
                Write-Host "Checking embedded font: $packagePath -> $($font.FullName)"
                $entryPath = $font.FullName.Replace('\', '/')
                $prefix = 'Contents/Common7/IDE/'
                if (!$entryPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Unexpected font payload path '$entryPath'; cannot resolve its signing input."
                }
                $relativePath = $entryPath.Substring($prefix.Length).Replace('/', '\')
                if (@($relativePath.Split('\') | Where-Object { $_ -eq '..' -or $_ -eq '.' }).Count -gt 0) {
                    throw "Invalid font payload path '$entryPath'."
                }
                $stagedPath = Join-Path (Join-Path $TemplatePath $package.TemplateKind) $relativePath
                $unsignedPath = Join-Path (Join-Path $UnsignedTemplatePath $package.TemplateKind) $relativePath
                $packagedHash = (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash
                $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash
                $unsignedHash = (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash
                Write-Host "Packaged SHA256: $packagedHash"
                Write-Host "Staged SHA256:   $stagedHash ($stagedPath)"
                Write-Host "Unsigned SHA256: $unsignedHash ($unsignedPath)"
                if ($packagedHash -ne $stagedHash) {
                    throw "Packaging mismatch: '$($font.FullName)' does not contain the staged signing output."
                }
                if ($stagedHash -eq $unsignedHash) {
                    throw "Signing did not change '$stagedPath': packaged and staged bytes match the preserved unsigned input."
                }
            }

            $catalogs = @($archive.Entries | Where-Object { $_.Name -like '*.cat' } | Sort-Object Name)
            if ($catalogs.Count -ne 1) {
                throw "Expected exactly one CAT file in '$packagePath', found $($catalogs.Count)."
            }
            $catalog = $catalogs[0]
            if ($catalog.Name -ne $package.CatalogName) {
                throw "Unexpected CAT filename '$($catalog.Name)' in '$packagePath'."
            }
            $catalogPath = Join-Path $VerificationPath $catalog.Name
            [IO.Compression.ZipFileExtensions]::ExtractToFile($catalog, $catalogPath)
            Write-Host "Checking embedded catalog: $packagePath -> $($catalog.FullName)"
            $entryPath = $catalog.FullName.Replace('\', '/')
            $prefix = 'Contents/Common7/IDE/'
            if (!$entryPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Unexpected catalog payload path '$entryPath'; cannot resolve its signing input."
            }
            $relativePath = $entryPath.Substring($prefix.Length).Replace('/', '\')
            if (@($relativePath.Split('\') | Where-Object { $_ -eq '..' -or $_ -eq '.' }).Count -gt 0) {
                throw "Invalid catalog payload path '$entryPath'."
            }
            if ($relativePath -ne $catalog.Name) {
                throw "Unexpected catalog payload path '$entryPath'."
            }
            $stagedPath = Join-Path (Join-Path $TemplatePath $package.TemplateKind) $relativePath
            $unsignedPath = Join-Path (Join-Path $UnsignedTemplatePath 'catalogs') $catalog.Name
            $packagedHash = (Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256).Hash
            $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash
            $unsignedHash = (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash
            Write-Host "Packaged SHA256: $packagedHash"
            Write-Host "Staged SHA256:   $stagedHash ($stagedPath)"
            Write-Host "Unsigned SHA256: $unsignedHash ($unsignedPath)"
            if ($packagedHash -ne $stagedHash) {
                throw "Packaging mismatch: '$($catalog.FullName)' does not contain the staged signing output."
            }
            if ($stagedHash -eq $unsignedHash) {
                throw "Signing did not change '$stagedPath': packaged and staged bytes match the preserved unsigned input."
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    $payloadsPrepared = $true
    Write-Host "##vso[task.setvariable variable=TemplateFontPayloadsPrepared]true"
    Write-Host "All four packaged fonts and both packaged catalogs match the modified signing output. Extracted payloads in '$VerificationPath' require MicroBuild signature verification."
}
finally {
    if (!$payloadsPrepared) {
        Remove-Item -LiteralPath $VerificationPath -Recurse -Force
    }
}
