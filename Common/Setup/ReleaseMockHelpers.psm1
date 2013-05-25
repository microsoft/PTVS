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

function begin_sign_files {
    param($files, $outdir, $approvers, $projectName, $projectUrl, $jobDescription, $jobKeywords, $certificates)
    
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
Returning @{@{filecount=$($files.Count); outdir=$outdir}"
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
   
    return @{job=$mockJob; description=$jobDescription; filecount=$($files.Count); outdir=$outdir}
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
        copy -path $jobCompletionPath\* -dest $outdir -Force
        if (-not $?) {
            Write-Output "Failed to copy $jobCompletionPath to $outdir"
        }
        #Get rid of the MockSigned directory        
        Remove-Item -Recurse $jobCompletionPath\*
        Remove-Item -Recurse $jobCompletionPath

        if((Get-Item $($job.JobMockFolder)).GetDirectories().Count -eq 0) {
            Remove-Item -Recurse $($job.JobMockFolder)
        }
    }
}