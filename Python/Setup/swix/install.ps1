param($vs, $vsdrop, [switch] $uninstall)

$install_dirs = @(
    "Common7\IDE\Extensions\Microsoft\Python",
    "Common7\IDE\ProjectTemplates\Python",
    "Common7\IDE\ProjectTemplatesCache\Python",
    "Common7\IDE\ItemTemplates\Python",
    "Common7\IDE\ItemTemplatesCache\Python",
    "MSBuild\Microsoft\VisualStudio\v14.0\Python Tools",
    "MSBuild\Microsoft\VisualStudio\v15.0\Python Tools"
)

$to_delete = $install_dirs | ?{ Test-Path "$vs\$_" } | %{ gi "$vs\$_" }
if ($to_delete) {
    "Cleaning old install..."
    $to_delete | rmdir -Recurse -Force
    if ($uninstall) {
        # Only uninstalling, so run devenv /setup now
        Start-Process -Wait "$vs\Common7\IDE\devenv.exe" "/setup"
    }
}

if (-not $uninstall) {
    $source = $MyInvocation.MyCommand.Definition | Split-Path -Parent
    
    copy -Recurse -Force $vsdrop\engine ${env:Temp}\engine
    
    $catalog = "${env:Temp}\engine\catalog.vsman";
    (gc "$source\Microsoft.PythonTools_Sideload.vsman") -replace '"manifestVersion": ".+?"', '"manifestVersion": "1.0"' | Out-File $catalog
    & "${env:Temp}\engine\setup.exe" install --catalog "$source\Microsoft.PythonTools_Sideload.vsman" --installdir "$vs" --layoutdir "$source"
    # devenv /setup is run by setup.exe
}
