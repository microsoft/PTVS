
$delete_pip = $false
$restore_pip_ini = $false
$delete_pip_ini = $false
$restore_pypirc = $false
$delete_pypirc = $false

try {
    if (-not (Test-Path ~\pip)) {
        mkdir ~\pip | Out-Null
        $delete_pip = $true
    }

    if (Test-Path ~\pip\pip.ini) {
        Move-Item ~\pip\pip.ini ~\pip\pip.ini.bak
        $restore_pip_ini = $true
    }
    
    Copy-Item pip.ini ~\pip\pip.ini
    $delete_pip_ini = $true
    
    if (Test-Path ~\.pypirc) {
        Move-Item ~\.pypirc ~\.pypirc.bak
        $restore_pypirc = $true
    }
    
    Copy-Item .pypirc ~\.pypirc
    $delete_pypirc = $true
    
    py -2.7 -m pip install pypiserver passlib
    
    if (-not (Test-Path .\TestPackages)) {
        mkdir .\TestPackages | Out-Null
    }
    
    py -2.7 -m pypiserver -o --disable-fallback -p 8080 -P .htpasswd .\TestPackages
    
} finally {
    if ($delete_pypirc) {
        Remove-Item ~\.pypirc -EA 0
    }
    if ($restore_pypirc) {
        Move-Item ~\.pypirc.bak ~\.pypirc -force -EA 0
    }
    
    if ($delete_pip_ini) {
        Remove-Item ~\pip\pip.ini -EA 0
    }
    if ($restore_pip_ini) {
        Move-Item ~\pip\pip.ini.bak ~\pip\pip.ini -force -EA 0
    }
    
    if ($delete_pip) {
        Remove-Item ~\pip -force -r
    }
}