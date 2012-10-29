param( $outDir, $build_name, [switch] $fast )

if (-not $outDir)
{
    Write-Error ("Must provide outdir parameter, the directory the release will be saved.")
    exit 1
}
if (-not $build_name)
{
    Write-Error "Must provide build_name parameter, such as '1.5 Alpha'"
    exit 1
}

if (Test-Path $outDir)
{
    rmdir -Recurse -Force $outDir
    if (-not $?)
    {
        Write-Error "Could not clean output directory: $outDir"
        exit 1
    }
}

$buildroot = (Get-Location).Path
while ((Test-Path $buildroot) -and -not (Test-Path ([System.IO.Path]::Combine($buildroot, "build.root")))) {
    $buildroot = [System.IO.Path]::Combine($buildroot, "..")
}
$buildroot = [System.IO.Path]::GetFullPath($buildroot)
"Build Root: $buildroot"
if (-not $fast) { tfpt scorch /noprompt $buildroot }

$prevOutDir = $outDir

$versions = @{number="11.0"; name="VS 2012"}, @{number="10.0"; name="VS 2010"}

foreach ($version in $versions) {
    ###################################################################
    # Build the actual binaries
    $outDir = "$prevOutDir\$($version.name)"
    mkdir $outDir
    
    echo "Building release to $outDir ..."
    
    if ($fast) {
        & $buildroot\Release\Product\Setup\BuildRelease.ps1 $prevOutdir -vsTarget $version.number -noclean -nocopy -skiptests -skipdebug > $outDir\release_output.txt
    } else {
        & $buildroot\Release\Product\Setup\BuildRelease.ps1 $prevOutdir -vsTarget $version.number -noclean -nocopy > $outDir\release_output.txt
    }
    
    ###################################################################
    # Index symbols
    
    $buildid = $prevOutDir.Substring($prevOutDir.LastIndexOf('\') + 1)
    
    $request = `
    "BuildId=$buildid $($version.name)
    BuildLabPhone=7058786
    BuildRemark=beta
    ContactPeople=$env:username;dinov;smortaz
    Directory=$outDir\Release\Symbols
    Project=TechnicalComputing
    Recursive=yes
    StatusMail=$env:username;dinov;smortaz
    UserName=$env:username
    SubmitToArchive=ALL
    SubmitToInternet=Yes"
    
    mkdir -force requests
    $request | Out-File -Encoding ascii -FilePath request.txt
    \\symbols\tools\createrequest.cmd -i request.txt -d .\requests -c -s
    
    [Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.6, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
    [Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
    
    #################################################################
    # Submit managed binaries
    
    $approvers = "smortaz", "dinov", "stevdo", "pminaev", "arturl", "zacha"
    $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})
    
    while($True) {
      try {
          $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
          $job.Description = "Python Tools for Visual Studio - managed code"
          $job.Keywords = "PTVS; Visual Studio; Python"
          
          $job.SelectCertificate("10006")  # Authenticode
          $job.SelectCertificate("67")     # StrongName key
          
          foreach ($approver in $approvers) { $job.AddApprover($approver) }
          
          $files = ("Microsoft.PythonTools.Analysis.dll", 
                    "Microsoft.PythonTools.Analyzer.exe", 
                    "Microsoft.PythonTools.Attacher.exe", 
                    "Microsoft.PythonTools.AttacherX86.exe", 
                    "Microsoft.PythonTools.Debugger.dll", 
                    "Microsoft.PythonTools.dll", 
                    "Microsoft.PythonTools.Hpc.dll", 
                    "Microsoft.PythonTools.ImportWizard.dll", 
                    "Microsoft.PythonTools.IronPython.dll", 
                    "Microsoft.PythonTools.MpiShim.exe", 
                    "Microsoft.PythonTools.Profiling.dll", 
                    "Microsoft.VisualStudio.ReplWindow.dll",
                    "Microsoft.PythonTools.PyKinect.dll",
                    "Microsoft.PythonTools.WebRole.dll",
                    "Microsoft.PythonTools.Django.dll",
                    "Microsoft.PythonTools.AzureSetup.exe",
                    "Microsoft.IronPythonTools.Resolver.dll",
                    "Microsoft.PythonTools.Pyvot.dll")
          
          $firstjobCount = $files.Length
          foreach ($filename in $files) {
              $fullpath =  "$outDir\Release\Binaries\$filename"
              $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
          }

          $job.Send()
          break;
      }catch [Exception] {
        echo $_.Exception.Message
        sleep 60
      }
    }
    
    $firstjob = $job
    
    #################################################################
    ### Submit native binaries
    
     while($True) {
       try {
          $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
          $job.Description = "Python Tools for Visual Studio - native code"
          $job.Keywords = "PTVS; Visual Studio; Python"
          
          $job.SelectCertificate("10006")  # Authenticode
          
          foreach ($approver in $approvers) { $job.AddApprover($approver) }
          
          $files = "PyDebugAttach.dll", "PyDebugAttachX86.dll", "VsPyProf.dll", "VsPyProfX86.dll", "PyKinectAudio.dll"
          $secondjobCount = $files.Length
          
          foreach ($filename in $files) {
              $fullpath = "$outDir\Release\Binaries\$filename"
              $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
          }
          $job.Send()
          break;
      }catch [Exception] {
        echo $_.Exception.Message
        sleep 60
      }
    }

    $secondjob = $job
    
    # wait for both jobs to finish being signed...
    $jobs = $firstjob, $secondjob
    $expectedFiles = $firstjobCount, $secondjobCount
    for($i = 0; $i -lt $jobs.Length; $i++) { 
        $job = $jobs[$i]
        $expectedFileLength = $expectedFiles[$i]
        
        $activity = "Job ID $($job.JobID) still processing"
        $percent = 0
        do {
            $files = dir $job.JobCompletionPath
            write-progress -activity $activity -status "Waiting for completion:" -percentcomplete $percent;
            $percent = ($percent + 1) % 100
            sleep -seconds 5
        } while(-not $files -or $files.Length -ne $expectedFileLength);
    }
    
    # save binaries to release share
    $destpath = "$outDir\Release\SignedBinaries"
    mkdir $destpath
    # copy files back to binaries
    echo 'Completion path', $firstjob.JobCompletionPath
    
    robocopy $firstjob.JobCompletionPath $destpath
    robocopy $secondjob.JobCompletionPath $destpath
    
    # copy files back to binaries for re-building the MSI
    robocopy $firstjob.JobCompletionPath $buildroot\Binaries\Release$($version.number)\
    robocopy $secondjob.JobCompletionPath $buildroot\Binaries\Release$($version.number)\
    
    # now generate MSI with signed binaries.
    $file = Get-Content $outDir\release_output.txt
    foreach($line in $file) {
        if($line.IndexOf('Light.exe') -ne -1) { 
            if($line.IndexOf('Release') -ne -1) { 
                $end = $line.IndexOf('.msm')
                if ($end -eq -1) {
                    $end = $line.IndexOf('.msi')
                }
                $start = $line.LastIndexOf('\', $end)
                $targetdir = $line.Substring($start + 1, $end - $start - 1)
                # hacks for mismatched names
                if ($targetdir -eq "IronPythonInterpreterMsm") {
                    $targetdir = "IronPythonInterpreter"
                }
                if ($targetdir -eq "PythonProfiler") {
                    $targetdir = "PythonProfiling"
                }
                if ($targetdir -eq "PythonHpcSupportMsm") {
                    $targetdir = "PythonHpcSupport"
                }
                if ($targetdir -eq "PyvotMsm") {
                    $targetdir = "PyVot"
                }
                if ($targetdir -eq "PyKinectMsm") {
                    $targetdir = "PyKinect"
                }
                if ($targetdir -eq "DjangoMsm") {
                    $targetdir = "Django"
                }
                if ($targetdir -eq "WFastCGI") {
                    $targetdir = "FastCgiInstaller"
                }
                echo $targetdir
    
                try {
                  cd $targetdir
                } catch {
                  echo "Unable to cd to $targetDir to execute line $line"
                  echo "Enter directory name to cd to: "
                  $targetDir = [Console]::ReadLine()
                  cd $targetdir
                }
                
                Invoke-Expression $line
                
                cd ..
            }
        }
    }
    
    mkdir $outDir\Release\UnsignedMsi
    foreach ($msi in (Get-ChildItem $outDir\Release\*.msi)) {
        move $msi "$outDir\Release\UnsignedMsi\$($msi.BaseName) $($version.name).msi"
    }
    
    mkdir $outDir\Release\SignedBinariesUnsignedMsi
    foreach ($msi in (Get-ChildItem $buildroot\Binaries\Release$($version.number)\*.msi)) {
        copy $msi "$outDir\Release\SignedBinariesUnsignedMsi\$($msi.BaseName) $($version.name).msi"
    }
    
    #################################################################
    ### Now submit the MSI for signing
    
    while($True) {
      try {
        $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
        $job.Description = "Python Tools for Visual Studio - managed code"
        $job.Keywords = "PTVS; Visual Studio; Python"
        
        $job.SelectCertificate("10006")  # Authenticode
        
        foreach ($approver in $approvers) { $job.AddApprover($approver) }
        
        $job.AddFile("$buildroot\Binaries\Release$($version.number)\PythonToolsInstaller.msi", "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
        $job.AddFile("$buildroot\Binaries\Release$($version.number)\PyKinectInstaller.msi", "Python Tools for Visual Studio - PyKinect", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
        $job.AddFile("$buildroot\Binaries\Release$($version.number)\PyvotInstaller.msi", "Python Tools for Visual Studio - Pyvot", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
        $job.AddFile("$buildroot\Binaries\Release$($version.number)\WFastCGI.msi", "Python Tools for Visual Studio - WFastCGI", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
        $expectedFileCount = 4
        
        $job.Send()
        break;
      } catch [Exception] {
        echo $_.Exception.Message
        sleep 60
      }
    }
    
    $activity = "Job ID $($job.JobID) still processing"
    $percent = 0
    do {
        $files = dir $job.JobCompletionPath
        write-progress -activity $activity -status "Waiting for completion:" -percentcomplete $percent;
        $percent = ($percent + 1) % 100
        sleep -seconds 5
    } while(-not $files -or $files.Length -ne $expectedFileCount);
    
    copy -force "$($job.JobCompletionPath)\PythonToolsInstaller.msi" "$outDir\Release\PTVS $build_name $($version.name).msi"
    copy -force "$($job.JobCompletionPath)\PyKinectInstaller.msi" "$outDir\Release\PTVS $build_name $($version.name) - PyKinect Sample.msi"
    copy -force "$($job.JobCompletionPath)\PyvotInstaller.msi" "$outDir\Release\PTVS $build_name $($version.name) - Pyvot Sample.msi"
    copy -force "$($job.JobCompletionPath)\WFastCGI.msi" "$outDir\Release\WFastCGI $build_name $($version.name).msi"
}

echo "Copying source files ..."
robocopy /s $buildRoot $prevOutDir\Sources /xd TestResults Binaries Servicing | Out-Null
