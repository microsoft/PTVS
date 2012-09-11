param( $outdir, $build_name )

if (-not $outdir)
{
    Write-Error "Must provide $outdir, the directory the release will be saved."
	exit 1
}
if (-not $build_name)
{
    Write-Error "Must provide build_name parameter, such as '1.5 Alpha'"
	exit 1
}

if (Test-Path $outdir)
{
    rmdir -Recurse -Force $outdir
    if (-not $?)
    {
        Write-Error "Could not clean output directory: $outdir"
        exit 1
    }
}

$buildroot = (Get-Location).Path
while ((Test-Path $buildroot) -and -not (Test-Path ([System.IO.Path]::Combine($buildroot, "build.root")))) {
    $buildroot = [System.IO.Path]::Combine($buildroot, "..")
}
$buildroot = [System.IO.Path]::GetFullPath($buildroot)
"Build Root: $buildroot"

$prevOutDir = $outDir


foreach ($version in ("10.0","11.0")) {
    ###################################################################
    # Build the actual binaries
    echo "Building release to $outdir ..."

    & $buildroot\Release\Product\Setup\BuildRelease.ps1 $outdir -vsTarget $version -noclean > release_output.txt
    
    if ($version -eq "10.0") {
        $outDir = $prevOutDir
    } else {
        $outDir = $prevOutDir + "\Dev" + $version
    }

    ###################################################################
    # Index symbols
    
    $buildid = $prevOutDir.Substring($prevOutDir.LastIndexOf('\') + 1)
    
    $request = `
    "BuildId=$buildid
    BuildLabPhone=7058786
    BuildRemark=beta
    ContactPeople=$env:username;dinov;smortaz
    Directory=$outdir\Release\Symbols
    Project=TechnicalComputing
    Recursive=yes
    StatusMail=$env:username;dinov;smortaz
    UserName=$env:username
    SubmitToArchive=ALL
    SubmitToInternet=Yes"
    
    mkdir -force requests
    $request | Out-File -Encoding ascii -FilePath request.txt
    \\symbols\tools\createrequest.cmd -i request.txt -d .\requests -c -s
    
    [Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.4, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
    [Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
    
    #################################################################
    # Submit managed binaries
    
    $approvers = "smortaz", "arturl", "weidongh", "dinov", "stevdo"
    $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})
    
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
    
    
    foreach ($filename in $files) {
        $fullpath =  "$outdir\Release\Binaries\$filename"
        $filename
        $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
    }
    $job.Send()
    
    $firstjob = $job
    
    #################################################################
    ### Submit native binaries
    
    $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
    $job.Description = "Python Tools for Visual Studio - native code"
    $job.Keywords = "PTVS; Visual Studio; Python"
    
    $job.SelectCertificate("10006")  # Authenticode
    
    foreach ($approver in $approvers) { $job.AddApprover($approver) }
    
    $files = "PyDebugAttach.dll", "PyDebugAttachX86.dll", "VsPyProf.dll", "VsPyProfX86.dll", "PyKinectAudio.dll"
    
    foreach ($filename in $files) {
        $fullpath = "$outdir\Release\Binaries\$filename"
        $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
    }
    $job.Send()
    $secondjob = $job
    
    # wait for both jobs to finish being signed...
    $jobs = $firstjob, $secondjob
    foreach($job in $jobs) {
        $activity = "Job ID " + $job.JobID + " still processing"
        $percent = 0
        do {
            $files = dir $job.JobCompletionPath
            write-progress -activity $activity -status "Waiting for completion:" -percentcomplete $percent;
            $percent = ($percent + 1) % 100
            sleep -seconds 5
        } while(-not $files);
    }
    
    # save binaries to release share
    $destpath = "$outdir\Release\SignedBinaries"
    mkdir $destpath
    # copy files back to binaries
    echo 'Completion path', $firstjob.JobCompletionPath
    
    robocopy $firstjob.JobCompletionPath $destpath\
    robocopy $secondjob.JobCompletionPath $destpath\
    
    # copy files back to binaries for re-building the MSI
    robocopy $firstjob.JobCompletionPath $buildroot\Binaries\Release\
    robocopy $secondjob.JobCompletionPath $buildroot\Binaries\Release\
    
    # now generate MSI with signed binaries.
    $file = Get-Content release_output.txt
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
                echo $targetdir
    
                cd $targetdir
                
                Invoke-Expression $line
                
                cd ..
            }
        }
    }
    
    $destpath = "$outdir\Release\UnsignedMsi"
    mkdir $destpath
    move $outdir\Release\PythonToolsInstaller.msi "$outdir\Release\UnsignedMsi\PythonToolsInstaller Dev $version.msi"
    move $outdir\Release\PyKinectInstaller.msi "$outdir\Release\UnsignedMsi\PyKinectInstaller Dev $version.msi"
    move $outdir\Release\PyvotInstaller.msi "$outdir\Release\UnsignedMsi\PyvotInstaller Dev $version.msi"
    
    $destpath = "$outdir\Release\SignedBinariesUnsignedMsi"
    mkdir $destpath
    copy  $buildroot\Binaries\Release\PythonToolsInstaller.msi "$prevOutDir\Release\SignedBinariesUnsignedMsi\PythonToolsInstaller Dev $version.msi"
    copy  $buildroot\Binaries\Release\PythonToolsInstaller.msi "$prevOutDir\Release\PythonToolsInstaller Dev $version.msi"
    
    copy  $buildroot\Binaries\Release\PyKinectInstaller.msi "$prevOutDir\Release\SignedBinariesUnsignedMsi\PyKinectInstaller Dev $version.msi"
    copy  $buildroot\Binaries\Release\PyKinectInstaller.msi "$prevOutDir\Release\PyKinectInstaller Dev $version.msi"
    
    copy  $buildroot\Binaries\Release\PyvotInstaller.msi "$prevOutDir\Release\SignedBinariesUnsignedMsi\PyvotInstaller Dev $version.msi"
    copy  $buildroot\Binaries\Release\PyvotInstaller.msi "$prevOutDir\Release\PyvotInstaller Dev $version.msi"
    
    #################################################################
    ### Now submit the MSI for signing
    
    $job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
    $job.Description = "Python Tools for Visual Studio - managed code"
    $job.Keywords = "PTVS; Visual Studio; Python"
    
    $job.SelectCertificate("10006")  # Authenticode
    
    foreach ($approver in $approvers) { $job.AddApprover($approver) }
    
    $job.AddFile($buildroot + "\Binaries\Release\PythonToolsInstaller.msi", "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
    $job.AddFile($buildroot + "\Binaries\Release\PyKinectInstaller.msi", "Python Tools for Visual Studio - PyKinect", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
    $job.AddFile($buildroot + "\Binaries\Release\PyvotInstaller.msi", "Python Tools for Visual Studio - Pyvot", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
    
    $job.Send()
    
    $activity = "Job ID " + $job.JobID + " still processing"
    $percent = 0
    do {
        $files = dir $job.JobCompletionPath
        write-progress -activity $activity -status "Waiting for completion:" -percentcomplete $percent;
        $percent = ($percent + 1) % 100
        sleep -seconds 5
    } while(-not $files);
    
    copy -force "$($job.JobCompletionPath)\PythonToolsInstaller.msi" "$prevOutDir\Release\PTVS $build_name Dev $version.msi"
    copy -force "$($job.JobCompletionPath)\PyKinectInstaller.msi" "$prevOutDir\Release\PTVS $build_name Dev $version - PyKinect Sample.msi"
    copy -force "$($job.JobCompletionPath)\PyvotInstaller.msi" "$prevOutDir\Release\PTVS $build_name Dev $version - Pyvot Sample.msi"
}