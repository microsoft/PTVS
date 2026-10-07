param(
    [Parameter(Mandatory = $true)]
    [string]$TemplateRoot,
    [Parameter(Mandatory = $true)]
    [string]$DocumentationPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $baseUri = [Uri]((Join-Path ([IO.Path]::GetFullPath($BasePath)) '.'))
    $pathUri = [Uri]([IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

if (!(Test-Path -LiteralPath $TemplateRoot -PathType Container)) {
    throw "Template root '$TemplateRoot' was not found."
}
if (!(Test-Path -LiteralPath $DocumentationPath -PathType Leaf)) {
    throw "Template update documentation '$DocumentationPath' was not found."
}

$templateRootPath = (Resolve-Path -LiteralPath $TemplateRoot).Path
$documentationFullPath = (Resolve-Path -LiteralPath $DocumentationPath).Path
$violations = [Collections.Generic.List[string]]::new()

foreach ($font in Get-ChildItem -LiteralPath $templateRootPath -Recurse -File -Filter '*.ttf') {
    $violations.Add("TTF asset: $(Get-RelativePath $templateRootPath $font.FullName)")
}

$textExtensions = @('.css', '.htm', '.html', '.js', '.json', '.proj', '.props', '.pyproj', '.targets', '.vstemplate', '.xml')
$referencePattern = '\.ttf([''")?#]|$)|format\([''"]truetype[''"]\)'
$textFiles = Get-ChildItem -LiteralPath $templateRootPath -Recurse -File |
    Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() }
foreach ($match in $textFiles | Select-String -Pattern $referencePattern) {
    $relativePath = Get-RelativePath $templateRootPath $match.Path
    $violations.Add("TTF reference: ${relativePath}:$($match.LineNumber)")
}

foreach ($styleSheet in Get-ChildItem -LiteralPath $templateRootPath -Recurse -File -Filter 'bootstrap*.css') {
    if (!(Select-String -LiteralPath $styleSheet.FullName -SimpleMatch 'glyphicons-halflings-regular' -Quiet)) {
        continue
    }

    foreach ($extension in @('eot', 'svg', 'woff')) {
        $fontPath = Join-Path $styleSheet.DirectoryName "glyphicons-halflings-regular.$extension"
        if (!(Test-Path -LiteralPath $fontPath -PathType Leaf)) {
            $relativePath = Get-RelativePath $templateRootPath $fontPath
            $violations.Add("Missing glyphicons fallback: $relativePath")
        }
    }
}

if ($violations.Count -gt 0) {
    $details = $violations | Sort-Object -Unique | ForEach-Object { " - $_" }
    throw "Template asset validation failed.`n$($details -join "`n")`nSee $documentationFullPath."
}

Write-Host "Template asset validation passed. See $documentationFullPath for update requirements."
