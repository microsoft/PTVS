if ($args.Length -eq 0) {
	echo "Must provide out dir"
	exit 1
}

###################################################################
# Build the actual binaries
echo "Building release to $args ..."
.\BuildRelease.ps1 $args[0] > release_output.txt

###################################################################
# Index symbols

$buildid = $args[0].Substring($args[0].LastIndexOf('\') + 1)
$request = "BuildId=$buildid`n" 
$request += "BuildLabPhone=7058786`n" 
$request += "BuildRemark=beta`n" 
$request += "ContactPeople=$env:username;dinov;smortaz`n" 
$request += "Directory=$args\Release\Symbols`n" 
$request += "Project=TechnicalComputing`n" 
$request += "Recursive=yes`n" 
$request += "StatusMail=$env:username;dinov;smortaz`n" 
$request += "UserName=$env:username`n" 
$request += "SubmitToArchive=ALL`n" 
$request += "SubmitToInternet=Yes`n" 

mkdir -force requests
[System.IO.File]::WriteAllText((get-location).Path + '\request.txt', $request)
\\symbols\tools\createrequest.cmd -i request.txt -d .\requests -c -s

[Reflection.Assembly]::Load("CODESIGN.Submitter, Version=3.0.0.4, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")
[Reflection.Assembly]::Load("CODESIGN.PolicyManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=3d8252bd1272440d, processorArchitecture=MSIL")

#################################################################
# Submit managed binaries

$job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
$job.Description = "Python Tools for Visual Studio - managed code"
$job.Keywords = "PTVS; Visual Studion; Python"

$job.SelectCertificate("10006")  # Authenticode
$job.SelectCertificate("67")     # StrongName key

$job.AddApprover("smortaz")
$job.AddApprover("mradmila");
$job.AddApprover("johncos");
$job.AddApprover("pavaga");

$files = "Microsoft.PythonTools.Analysis.dll", "Microsoft.PythonTools.Analyzer.exe", "Microsoft.PythonTools.Attacher.exe", "Microsoft.PythonTools.AttacherX86.exe", "Microsoft.PythonTools.Debugger.dll", "Microsoft.PythonTools.dll", "Microsoft.PythonTools.Hpc.dll", "Microsoft.PythonTools.IronPython.dll", "Microsoft.PythonTools.MpiShim.exe", "Microsoft.PythonTools.Profiling.dll", "Microsoft.VisualStudio.ReplWindow.dll"

foreach ($filename in $files) {
    $fullpath = [System.IO.Path]::Combine([System.IO.Path]::Combine($args, "Release\Binaries"), $filename)
    $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
}
$job.Send()

$firstjob = $job

#################################################################
### Submit x86 native binaries

$job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
$job.Description = "Python Tools for Visual Studio - managed code"
$job.Keywords = "PTVS; Visual Studion; Python"

$job.SelectCertificate("10006")  # Authenticode

$job.AddApprover("smortaz")
$job.AddApprover("mradmila");
$job.AddApprover("johncos");
$job.AddApprover("pavaga");

$files = "PyDebugAttach.dll", "VsPyProf.dll"

foreach ($filename in $files) {
    $fullpath = [System.IO.Path]::Combine([System.IO.Path]::Combine($args, "Release\Binaries"), $filename)
    $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
}
$job.Send()
$secondjob = $job

#################################################################
### Submit x64 native binaries

$job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
$job.Description = "Python Tools for Visual Studio - managed code"
$job.Keywords = "PTVS; Visual Studion; Python"

$job.SelectCertificate("10006")  # Authenticode

$job.AddApprover("smortaz")
$job.AddApprover("mradmila");
$job.AddApprover("johncos");
$job.AddApprover("pavaga");

$files = "PyDebugAttach.dll", "VsPyProf.dll"

foreach ($filename in $files) {
    $fullpath = [System.IO.Path]::Combine([System.IO.Path]::Combine($args, "Release\Binaries\x64"), $filename)
    $job.AddFile($fullpath, "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)
}

$job.Send()
$thirdjob = $job

# wait for all 3 jobs to finish being signed...
$jobs = $firstjob, $secondjob, $thirdjob
foreach($job in $jobs) {
    [Console]::WriteLine("Waiting for job to complete", $job.JobID)
    do {
        $files = dir $job.JobCompletionPath
        [Console]::Write(".")
        sleep 5
    } while(!$files);
}

# save binaries to release share
$destpath = "$args\Release\SignedBinaries"
mkdir $destpath
# copy files back to binaries
foreach($file in dir $firstjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine($destpath, $file.Name)
}

foreach($file in dir $secondjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine($destpath, $file.Name)
}

foreach($file in dir $thirdjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine($destpath + "\\x64", $file.Name)
}
 
# copy files back to binaries for re-building the MSI
foreach($file in dir $firstjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine("..\..\..\binaries\win32\Release", $file.Name)
}

foreach($file in dir $secondjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine("..\..\..\binaries\win32\Release", $file.Name)
}

foreach($file in dir $thirdjob.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine("..\..\..\binaries\x64\Release", $file.Name)
}

# now generate MSI with signed binaries.
$file = [System.IO.File]::ReadAllLines((get-location).Path + '\release_output.txt')
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
            echo $targetdir

            cd $targetdir
            
            Invoke-Expression $line
            
            cd ..
        }
    }
}

$destpath = "$args\Release\UnsignedMsi"
mkdir $destpath
move $args\Release\PythonToolsInstaller.msi $args\Release\UnsignedMsi\PythonToolsInstaller.msi

$destpath = "$args\Release\SignedBinariesUnsignedMsi"
mkdir $destpath
copy ((get-location).Path + "\..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi") $args\Release\SignedBinariesUnsignedMsi\PythonToolsInstaller.msi
copy ((get-location).Path + "\..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi") $args\Release\PythonToolsInstaller.msi

#################################################################
### Now submit the MSI for signing

$job = [CODESIGN.Submitter.Job]::Initialize("codesign.gtm.microsoft.com", 9556, $True)
$job.Description = "Python Tools for Visual Studio - managed code"
$job.Keywords = "PTVS; Visual Studion; Python"

$job.SelectCertificate("10006")  # Authenticode

$job.AddApprover("smortaz")
$job.AddApprover("mradmila");
$job.AddApprover("johncos");
$job.AddApprover("pavaga");

$job.AddFile((get-location).Path + "\..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi", "Python Tools for Visual Studio", "http://pytools.codeplex.com", [CODESIGN.JavaPermissionsTypeEnum]::None)

$job.Send()

[Console]::WriteLine("Waiting for job to complete", $job.JobID)
do {
    $files = dir $job.JobCompletionPath
    [Console]::Write(".")
    sleep 5
} while(!$files);

foreach($file in dir $job.JobCompletionPath) {
    copy -force $file.FullName [System.IO.Path]::Combine($args, "Release\" + $file.Name)
}