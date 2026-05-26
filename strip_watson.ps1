$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false

function Strip-WatsonRefs {
    param([string]$Path)

    $utf8Bom   = [System.Text.UTF8Encoding]::new($true)
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $enc = if ($hasBom) { $utf8Bom } else { $utf8NoBom }
    $offset = if ($hasBom) { 3 } else { 0 }
    $content = $enc.GetString($bytes, $offset, $bytes.Length - $offset)
    $original = $content

    # 1) Multi-line cluster reference: " Watson\n            // cluster X (#..., #...)."
    $content = [regex]::Replace($content, ' Watson\r?\n\s+//\s+cluster [A-Z] \(#\d+(?:,\s*#\d+)*\)\.', '')

    # 2) End-of-line cluster reference: " Watson cluster D (#..., #...)."
    $content = [regex]::Replace($content, ' Watson cluster [A-Z] \(#\d+(?:,\s*#\d+)*\)\.', '')

    # 3) Standalone Watson-only comment line: "            // Watson #NNNN."
    $content = [regex]::Replace($content, '\r?\n[ \t]*//[ \t]*Watson #\d+\.[ \t]*(?=\r?\n)', '')

    # 4) Embedded "(Watson #NNNN). " with trailing space (keeps the rest of the sentence)
    $content = [regex]::Replace($content, '\(Watson #\d+\)\. ', '')

    # 5) End-of-comment " Watson #NNNN." appended to a sentence ending with its own period
    $content = [regex]::Replace($content, ' Watson #\d+\.', '')

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($Path, $content, $enc)
        return $true
    }
    return $false
}

$branchFiles = [ordered]@{
    'fix-watson-1222779-conda-history-watcher'        = @('Python/Product/VSInterpreters/PackageManager/CondaPackageManager.cs')
    'fix-watson-1229982-pip-onrenamed-pathtoolong'    = @('Python/Product/VSInterpreters/PackageManager/PipPackageManager.cs')
    'fix-watson-1345469-processoutput-envvars'        = @(
        'Common/Product/SharedProject/ProcessOutput.cs',
        'Python/Product/Common/Infrastructure/ProcessOutput.cs',
        'Python/Product/Cookiecutter/Shared/Infrastructure/ProcessOutput.cs'
    )
    'fix-watson-1446269-reanalyze-uithread'           = @('Python/Product/PythonTools/PythonTools/Project/PythonProjectNode.cs')
    'fix-watson-1459718-processservices-start'        = @('Python/Product/Common/Core/OS/ProcessServices.cs')
    'fix-watson-1488201-cookiecutter-waitforoutput'   = @('Python/Product/Cookiecutter/Model/CookiecutterClient.cs')
    'fix-watson-1641198-hierarchy-closedoc'           = @('Common/Product/SharedProject/HierarchyNode.cs')
    'fix-watson-1812702-stream-intercepter'           = @('Python/Product/PythonTools/PythonTools/LanguageServerClient/StreamHacking/StreamIntercepter.cs')
    'fix-watson-2325087-invoke-connectionlost'        = @('Python/Product/PythonTools/PythonTools/LanguageServerClient/PythonLanguageClient.cs')
    'fix-watson-2455159-lsp-getsettings-dispose'      = @('Python/Product/PythonTools/PythonTools/LanguageServerClient/PythonLanguageClient.cs')
    'fix-watson-clusterB-filewatcher-listener'        = @('Python/Product/PythonTools/PythonTools/LanguageServerClient/FileWatcher/Listener.cs')
    'fix-watson-clusterD-task-extensions'             = @('Python/Product/Common/Infrastructure/TaskExtensions.cs')
}

$root = 'Z:\Repos\PTVS'
$summary = @()

foreach ($entry in $branchFiles.GetEnumerator()) {
    $branch = $entry.Key
    $files  = $entry.Value
    Write-Host "==================================================================="
    Write-Host "=== Branch: $branch"
    Write-Host "==================================================================="

    cmd /c "git checkout $branch 2>&1" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "checkout failed for $branch" }

    $touchedAny = $false
    foreach ($f in $files) {
        $full = Join-Path $root $f
        $changed = Strip-WatsonRefs -Path $full
        if ($changed) {
            Write-Host "  modified: $f"
            $touchedAny = $true
        } else {
            Write-Host "  unchanged: $f"
        }
    }

    if ($touchedAny) {
        Write-Host "--- git diff --stat ---"
        cmd /c "git diff --stat 2>&1" | Out-Host
        Write-Host "--- amending commit ---"
        cmd /c "git add -A 2>&1" | Out-Host
        cmd /c "git commit --amend --no-edit 2>&1" | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Host "AMEND FAILED for $branch"; $summary += "$branch : AMEND FAILED"; continue }
        Write-Host "--- pushing (force-with-lease) ---"
        cmd /c "git push --force-with-lease origin $branch 2>&1" | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Host "PUSH FAILED for $branch"; $summary += "$branch : PUSH FAILED"; continue }
        $summary += "$branch : MODIFIED + PUSHED"
    } else {
        $summary += "$branch : NO CHANGE"
    }
}

Write-Host ""
Write-Host "===== Summary ====="
$summary | ForEach-Object { Write-Host $_ }
