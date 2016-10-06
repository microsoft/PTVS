param($vs, [switch] $uninstall)

if (-not $vs) {
    throw "Missing -vs [path] parameter"
    exit 1
}

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
}

if (-not $uninstall) {
    [Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null

    $source = $MyInvocation.MyCommand.Definition | Split-Path -Parent

    # Need to use top level directory to avoid exceeding MAX_PATH
    $tmp = mkdir "${env:SystemDrive}\__p" -Force
    pushd $tmp

    copy -Recurse -Force "$source\*.vsix" .

    gci "*.vsix" | %{
        $d = mkdir "Content_$($_.Name)" -Force

        "Extracting $($_.Name)..."
        [System.IO.Compression.ZipFile]::ExtractToDirectory($_, $d)

        if (Test-Path "$d\contents") {
            pushd "$d\contents"
            gci * -Recurse | ?{ $_.Name -Match '%' } | %{ move $_ "$([System.Web.HttpUtility]::UrlDecode($_))" }
            copy -Recurse -Force * $vs
            popd
        }

        rmdir $d -Recurse -Force
    }
    
    popd
    rmdir $tmp -Recurse -Force
}

"Running devenv.exe /setup..."
Start-Process -Wait "$vs\Common7\IDE\devenv.exe" "/setup"

"Complete!"
