function submit_symbols {
    param($buildname, $buildid, $filetype, $sourcedir, $contacts)
    
    Write-Debug "*** Symbol Submission Text ***
    BuildId=$buildid $filetype
    BuildLabPhone=7058786
    BuildRemark=$buildname
    ContactPeople=$contacts
    Directory=$sourcedir
    Project=TechnicalComputing
    Recursive=yes
    StatusMail=$contacts
    UserName=$env:username"
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
                Throw "Delay-signed check failed: $($not_delay_signed | %{ $_.Name })
You may need to skip strong name verification on this machine."
            }
        }
    }
    
    [Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.6, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL") | Out-Null
    [Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL") | Out-Null

    $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
    $msg = "*** Signing Job Details ***
job.Description:  $jobDescription
job.Keywords:     $jobKeywords"
    $job.Description = $jobDescription
    $job.Keywords = $jobKeywords
    
    if ($certificates -match "authenticode") {
        $msg = "$msg
job.SelectCertificate(10006)"
        $job.SelectCertificate("10006")  # Authenticode
    }
    if ($certificates -match "strongname") {
        $msg = "$msg
job.SelectCertificate(67)"
        $job.SelectCertificate("67")     # StrongName key
    }
    if ($certificates -match "opc") {
        $job.SelectCertificate("160")     # Microsoft OPC Publisher (VSIX)
    }
    
    foreach ($approver in $approvers) {
        $msg = "$msg
job.AddApprover($approver)"
        $job.AddApprover($approver)
    }
    
    foreach ($file in $files) {
        $msg = "$msg
job.AddFile($($file.path), $($file.name), $projectUrl, None)"
        $job.AddFile($file.path, $file.name, $projectUrl, [CODESIGN.JavaPermissionsTypeEnum]::None)
    }
    
    $msg = "$msg
Returning @{filecount=$($files.Count); outdir=$outdir}"
    Write-Debug $msg
    
    $uniqueJobFolderID = ((Get-Date -format "yyyyMMdd-HHmmss").ToString() + "-" + (Get-Date).Millisecond.ToString())

    $folder = ((Get-Item $($files[0].path)).DirectoryName.ToString() + "\MockSigned")

    $mockJob = New-Object PSObject
    $mockJob | Add-Member NoteProperty JobID $uniqueJobFolderID
    $mockJob | Add-Member NoteProperty JobMockFolder $folder
    $mockJob | Add-Member NoteProperty JobCompletionPath $folder\$uniqueJobFolderID          

    $mockFolderPath = $mockJob.JobCompletionPath    
    mkdir $mockFolderPath -EA 0 | Out-Null

    foreach($file in $files) {
        $destPath = "$($mockFolderPath)\$($fileInfo.Name)"
        copy -path $($file.path) -dest $destPath
        if (-not $?) {
            Write-Output "Failed to copy $($file.path) to $destPath"
        }
    }
    
    return @{rjob=$job; job=$mockJob; description=$jobDescription; filecount=$($files.Count); outdir=$outdir}
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
            throw "job.JobCompletionPath is not valid: $($job.JobCompletionPath)"
        }

        do {
            $files = @()
            Write-Progress -activity $activity -status "Waiting for completion: $jobCompletionPath" -percentcomplete $percent;
            $percent = ($percent + 1) % 100
            if ($percent -eq 90) {
                $files = dir $jobCompletionPath
            }
            sleep -Milliseconds 50 
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

        #Get rid of the MockSigned directory
        Remove-Item -Recurse $jobCompletionPath\*
        Remove-Item -Recurse $jobCompletionPath

        if((Get-Item $($job.JobMockFolder)).GetDirectories().Count -eq 0) {
            Remove-Item -Recurse $($job.JobMockFolder)
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
    
    Write-Debug "Posting to http://vcs/process.asp:
$($xml.OuterXml)"
}
