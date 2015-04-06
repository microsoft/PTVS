function submit_symbols {
    param($buildname, $buildid, $filetype, $sourcedir, $contacts)
    
    $request = `
    "BuildId=$buildid $filetype
    BuildLabPhone=7058786
    BuildRemark=$buildname
    ContactPeople=$contacts
    Directory=$sourcedir
    Project=TechnicalComputing
    Recursive=yes
    StatusMail=$contacts
    UserName=$env:username"

    Write-Output "*** Symbol Submission Text ***
$request"

    $request | Out-File -Encoding ascii -FilePath request_$filetype.txt
    \\symbols\tools\createrequest.cmd -i request_$filetype.txt -d .\SymSrvRequestLogs -c -s
}

function _find_sdk_tool {
    param($tool)
    
    $_tool_item = ""
    foreach ($ver in ("v8.1A", "v8.0A")) {
        foreach ($kit in ("WinSDK-NetFx40Tools-x64", "WinSDK-NetFx40Tools-x86", "WinSDK-NetFx40Tools")) {
            $_kit_path = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\Windows\$ver\$kit" -EA 0)
            if (-not $_kit_path) {
                $_kit_path = (Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows\$ver\$kit" -EA 0)
            }

            if ($_kit_path -and (Test-Path $_kit_path.InstallationFolder)) {
                $_tool_item = Get-Item "$($_kit_path.InstallationFolder)\$tool.exe" -EA 0
                if (-not (Test-Path alias:\$tool) -and $_tool_item) {
                    Set-Alias -Name $tool -Value $_tool_item.FullName -Scope Global
                    return
                }
            }
        }
    }
    foreach ($ver in ("KitsRoot81", "KitsRoot")) {
        $_kit_path = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots" -Name $ver -EA 0).$ver
        if (-not $_kit_path) {
            $_kit_path = (Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows Kits\Installed Roots" -Name $ver -EA 0).$ver
        }

        foreach ($kit in ("x64", "x86")) {
            if ($_kit_path -and (Test-Path "$_kit_path\bin\$kit")) {
                $_tool_item = Get-Item "$_kit_path\bin\$kit\$tool.exe" -EA 0
                if (-not (Test-Path alias:\$tool) -and $_tool_item) {
                    Set-Alias -Name $tool -Value $_tool_item.FullName -Scope Global
                    return
                }
            }
        }
    }
}

function begin_sign_files {
    param($files, $outdir, $approvers, $projectName, $projectUrl, $jobDescription, $jobKeywords, $certificates, [switch] $delaysigned)
    
    if ($files.Count -eq 0) {
        return
    }
    
    if ($delaysigned) {
        # Ensure that all files are delay-signed
        # "sn -q -v ..." is true if the assembly has strong name and skip verification
        _find_sdk_tool "sn"
        if (Test-Path alias:\sn) {
            $not_delay_signed = $files | %{ gi $_.path } | ?{ sn -q -v $_ }
            if ($not_delay_signed) {
                Throw "Delay-signed check failed: $($not_delay_signed.Name -join '
')"
            }
        }
    }
    
    [Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.6, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL") | Out-Null
    [Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL") | Out-Null

    while ($True) {
        try {
            $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
            $job.Description = $jobDescription
            $job.Keywords = $jobKeywords
            
            if ($certificates -match "authenticode") {
                $job.SelectCertificate("10006")  # Authenticode
            }
            if ($certificates -match "strongname") {
                $job.SelectCertificate("67")     # StrongName key
            }
            if ($certificates -match "opc") {
                $job.SelectCertificate("160")     # Microsoft OPC Publisher (VSIX)
            }
            
            foreach ($approver in $approvers) {
                $job.AddApprover($approver)
            }
            
            foreach ($file in $files) {
                $job.AddFile($file.path, $file.name, $projectUrl, [CODESIGN.JavaPermissionsTypeEnum]::None)
            }
            
            $job.Send()
            return @{job=$job; description=$jobDescription; filecount=$($files.Count); outdir=$outdir}
        } catch [Exception] {
            echo $_.Exception.Message
            sleep 60
        }
    }
}

function end_sign_files {
    param($jobs)
    
    if ($jobs.Count -eq 0) {
        return
    }
    
    foreach ($jobinfo in $jobs) {
        $job = $jobinfo.job
        if($job -eq $null) {
            throw "jobinfo in unexpected format $jobinfo"
        }
        $filecount = $jobinfo.filecount
        $outdir = $jobinfo.outdir
        $activity = "Processing $($jobinfo.description) (Job ID $($job.JobID))"
        $percent = 0
        $jobCompletionPath = $job.JobCompletionPath

        if([string]::IsNullOrWhiteSpace($jobCompletionPath)) {
            throw "job.JobCompletionPath is not valid: $job.JobCompletionPath"
        }

        do {
            $files = dir $jobCompletionPath
            Write-Progress -activity $activity -status "Waiting for completion: $jobCompletionPath" -percentcomplete $percent;
            $percent = ($percent + 1) % 100
            sleep -seconds 5
        } while(-not $files -or $files.Count -ne $filecount);
        
        mkdir $outdir -EA 0 | Out-Null
        Write-Progress -Activity $activity -Completed
        
        Write-Output "Copying from $jobCompletionPath to $outdir"
        $retries = 9
        $delay = 2
        $copied = $null
        while ($retries) {
            try {
                $copied = (Copy-Item -path $jobCompletionPath\* -dest $outdir -Force -PassThru)
                break
            } catch {
                if ($retries -eq 0) {
                    break
                }
                Write-Warning "Failed to copy - retrying in $delay seconds ($retries tries remaining)"
                Sleep -seconds $delay
                --$retries
                $delay += $delay
            }
        }
        if (-not $copied) {
            Throw "Failed to copy $jobCompletionPath to $outdir"
        } else {
            Write-Output "Copied $($copied.Count) files"
        }
    }
}

function start_virus_scan {
    param($description, $contact, $path)
    
    $xml = New-Object XML
    $xml.LoadXml("<root><description /><contact /><path /><region>AOC</region></root>")
    $xml.root.description = $description
    $xml.root.contact = $contact
    $xml.root.path = $path
    
    Invoke-WebRequest "http://vcs/process.asp" -Method Post -Body $xml -ContentType "text/xml" -UseDefaultCredentials -UseBasicParsing
}

function check_signing {
    param($outdir)

    _find_sdk_tool "signtool"

    $unsigned = @()

    $msis = gci $outdir\*.msi
    foreach ($m in $msis) {
        Write-Host "Checking signatures for $m"
        & signtool verify /pa "$m" 2>&1 | Out-Null
        if (-not $?) {
            $unsigned += "$m"
        }

        $dir = mkdir -fo "${env:TEMP}\msi_test"
        & msiexec /q /a "$m" TARGETDIR="$dir" | Out-Null
        
        foreach ($f in (gci $dir\*.exe, $dir\*.dll -r)) {
            & signtool verify /pa "$f" 2>&1 | Out-Null
            if (-not $?) {
                $unsigned += "$m - $($f.Name)"
            }
        }
        
        rmdir -r -fo $dir
    }

    Add-Type -assembly "System.IO.Compression.FileSystem"
    $zips = gci $outdir\*.vsix
    foreach ($m in $zips) {
        Write-Host "Checking signatures for $m"
        $dir = mkdir -fo "${env:TEMP}\msi_test"
        [IO.Compression.ZipFile]::ExtractToDirectory($m, $dir)
        
        if (-not (Test-Path "$dir\package\services\digital-signature\xml-signature")) {
            $unsigned += "$m"
        }

        foreach ($f in (gci $dir\*.exe, $dir\*.dll -r)) {
            & signtool verify /pa "$f" 2>&1 | Out-Null
            if (-not $?) {
                $unsigned += "$m - $($f.Name)"
            }
        }
        
        rmdir -r -fo $dir
    }

    if ($unsigned) {
        throw "Following files have invalid signatures: 
$(($unsigned | select -unique) -join '
')"
    }
}