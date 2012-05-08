#------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the License.html file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#------------------------------------------------------------------------------
param([string]$pythonVersion, [string]$pyBittedNess)

$pythonExe = $null;
$pipExe    = $null;

if (($pythonVersion -eq $null) -or ($pyBittedNess -eq $null)) {
	echo "Must specify all command-line parameters!"
	exit 1;
}

$dotLessPyVer = $pythonVersion.replace(".", "");
$pythonDotBat = "$env:TMP\python_" + $dotLessPyVer + "_" + "$pyBittedNess.bat";
$pipDotBat    = "$env:TMP\pip_"    + $dotLessPyVer + "_" + "$pyBittedNess.bat";

$isMachine64Bit = (get-itemproperty -path "hklm:System\CurrentControlSet\Control\Session Manager\Environment" PROCESSOR_ARCHITECTURE).PROCESSOR_ARCHITECTURE -eq "AMD64";

#--Use the registry
$regKey = "hklm:SOFTWARE\Python\PythonCore\$pythonVersion\InstallPath";
if (($isMachine64Bit -eq $true) -and ($pyBittedNess -eq "x86")) {
	$regKey = "hklm:SOFTWARE\Wow6432Node\Python\PythonCore\$pythonVersion\InstallPath";
}
if (test-path $regKey) {
	$partialPath = (get-itemproperty -path $regKey '(default)').'(default)';
	if (test-path $partialPath) {
		$pythonExe = "$partialPath\python.exe";
		$pipExe    = "$partialPath\Scripts\pip.exe";
	}
}

#--Sanity check
if (($pythonExe -eq $null) -or (! (test-path $pythonExe))) {
	echo "'$pythonExe' does not exist!";
	exit 1;
}

#--Write out the file
$pythonExe = resolve-path $pythonExe;
$pipExe    = $pipExe.replace("\\", "\");
echo "$pythonExe %*"  | out-file -encoding ascii $pythonDotBat;
echo "$pipExe %*"     | out-file -encoding ascii $pipDotBat;