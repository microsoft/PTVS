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

function begin_sign_files {
    param($files, $outdir, $approvers, $projectName, $projectUrl, $jobDescription, $jobKeywords, $certificates)
    
	[Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.6, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
    [Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")

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
        copy -path $jobCompletionPath\* -dest $outdir -Force
        if (-not $?) {
            Write-Output "Failed to copy $jobCompletionPath to $outdir"
        }
    }
}
