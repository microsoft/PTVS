[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$extract = [IO.Path]::GetTempFileName()
del $extract
$zip = "$extract.zip"
$target = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Join-Path -ChildPath "Typeshed"
mkdir -f $target, $target\stdlib, $target\third_party;

iwr "https://github.com/python/typeshed/archive/master.zip" -OutFile $zip

Expand-Archive $zip $extract -Force


pushd $extract\typeshed-master
try {
    copy -r -fo stdlib, third_party, LICENSE $target
} finally {
    popd
}

rmdir -r -fo $extract
del -fo $zip

"Latest version of typeshed extracted. Use git add -u to pull in changes."
