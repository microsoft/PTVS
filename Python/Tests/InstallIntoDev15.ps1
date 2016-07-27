param($vs, [switch] $uninstall)

$install_dirs = @(
    "Common7\IDE\Extensions\Microsoft\Python",
    "Common7\IDE\ProjectTemplates\Python",
    "Common7\IDE\ProjectTemplatesCache\Python",
    "Common7\IDE\ItemTemplates\Python",
    "Common7\IDE\ItemTemplatesCache\Python",
    "Common7\IDE\ItemTemplates\CloudService\NETFramework4\Web Role\Python",
    "Common7\IDE\ItemTemplates\CloudService\NETFramework4\Worker Role\Python",
    "MSBuild\Microsoft\VisualStudio\v14.0\Python Tools",
    "MSBuild\Microsoft\VisualStudio\v15.0\Python Tools"
)

$to_delete = $install_dirs | ?{ Test-Path "$vs\$_" } | %{ gi "$vs\$_" }

if ($to_delete) {
    "Cleaning old install..."
    $to_delete | rmdir -Recurse -Force

    del "$vs\Common7\IDE\ProjectTemplates\Microsoft.PythonTools*.vstman" -EA 0
    del "$vs\Common7\IDE\ItemTemplates\Microsoft.PythonTools*.vstman" -EA 0

    if ($uninstall) {
        # Only uninstalling, so run devenv /setup now
        Start-Process -Wait "$vs\Common7\IDE\devenv.exe" "/setup"
    }
}

if (-not $uninstall) {
    $source = $MyInvocation.MyCommand.Definition | Split-Path -Parent
    $ext = "$vs\Common7\IDE\Extensions\Microsoft\Python";

    "Copying from $source"

    "Extensions to $ext"
    Copy -Recurse "$source\Microsoft.PythonTools\*"             (mkdir "$ext\Core" -Force)
    Copy -Recurse "$source\Microsoft.PythonTools.Django\*"      (mkdir "$ext\Django" -Force)
    Copy -Recurse "$source\Microsoft.PythonTools.IronPython\*"  (mkdir "$ext\IronPython" -Force)
    Copy -Recurse "$source\Microsoft.PythonTools.Profiling\*"   (mkdir "$ext\Profiling" -Force)
    Copy -Recurse "$source\Microsoft.PythonTools.Uwp\*"         (mkdir "$ext\Uwp" -Force)
    
    "Targets to $vs\MSBuild"
    Move (gci -Recurse "$ext\*.targets") (mkdir "$vs\MSBuild\Microsoft\VisualStudio\v15.0\Python Tools" -Force)
    Move (gci -Recurse `
        "$ext\Core\Microsoft.PythonTools.AzureSetup.*", `
        "$ext\Core\Microsoft.PythonTools.WebRole.*", `
        "$ext\Core\web_config.xml" , `
        "$ext\Core\web_debug_config.xml" , `
        "$ext\Core\wfastcgi.py" `
    ) (mkdir "$vs\MSBuild\Microsoft\VisualStudio\v15.0\Python Tools" -Force)

    "Templates to $vs\Common7\IDE\ProjectTemplates\Python"
    Copy -Recurse "$source\Microsoft.PythonTools.Templates\ProjectTemplates\Python\*"       (mkdir "$vs\Common7\IDE\ProjectTemplates\Python" -Force)
    $manifests = gi "$source\Microsoft.PythonTools.Templates\ProjectTemplates\*.vstman"
    for ($i = 0; $i -lt $manifests.Count; $i += 1) {
        Copy $manifests[$i] "$vs\Common7\IDE\ProjectTemplates\Microsoft.PythonTools.$i.vstman";
    }

    "Templates to $vs\Common7\IDE\ItemTemplates\Python"
    Copy -Recurse -Force "$source\Microsoft.PythonTools.Templates\ItemTemplates\Python\*"          (mkdir "$vs\Common7\IDE\ItemTemplates\Python" -Force)

    "Templates to $vs\Common7\IDE\ItemTemplates\CloudService"
    Copy -Recurse -Force "$source\Microsoft.PythonTools.Templates\ItemTemplates\CloudService\*"    (mkdir "$vs\Common7\IDE\ItemTemplates\CloudService" -Force)
    $manifests = gi "$source\Microsoft.PythonTools.Templates\ItemTemplates\*.vstman"
    for ($i = 0; $i -lt $manifests.Count; $i += 1) {
        Copy $manifests[$i] "$vs\Common7\IDE\ItemTemplates\Microsoft.PythonTools.$i.vstman";
    }

    Start-Process -Wait "$vs\Common7\IDE\devenv.exe" "/setup"
}
