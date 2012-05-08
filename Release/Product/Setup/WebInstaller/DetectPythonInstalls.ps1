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

#Remove any trace of previous runs of this script
rm -recurse -force "$env:TMP\python_*" 2> $null;
rm -recurse -force "$env:TMP\pip_*"    2> $null;

$isMachine64Bit = (get-itemproperty -path "hklm:System\CurrentControlSet\Control\Session Manager\Environment" PROCESSOR_ARCHITECTURE).PROCESSOR_ARCHITECTURE -eq "AMD64";

$pyBittedNesses = @("x86", "x64");
$pythonVersions = @("2.6", "2.7", "3.0", "3.1", "3.2", "3.3", "3.4");

foreach($pyBittedNess in $pyBittedNesses) {
	foreach($pythonVersion in $pythonVersions) {
		$dotLessPyVer = $pythonVersion.replace(".", "");
	
		$pythonDotBat = "$env:TMP\python_" + $dotLessPyVer + "_" + "$pyBittedNess.bat";
		$pipDotBat    = "$env:TMP\pip_"    + $dotLessPyVer + "_" + "$pyBittedNess.bat";

		#--Use the registry
		$regKey = "hklm:SOFTWARE\Python\PythonCore\$pythonVersion\InstallPath";
		if (($isMachine64Bit -eq $true) -and ($pyBittedNess -eq "x86")) {
			$regKey = "hklm:SOFTWARE\Wow6432Node\Python\PythonCore\$pythonVersion\InstallPath";
		}
		if (test-path $regKey) {
			$partialPath = (get-itemproperty -path $regKey '(default)').'(default)';
			if (test-path $partialPath) {
				$pythonExe = resolve-path "$partialPath\python.exe";
				$pipExe    = "$partialPath\Scripts\pip.exe".replace("\\", "\");
				echo "$pythonExe %*"  | out-file -encoding ascii $pythonDotBat;
				echo "$pipExe %*"     | out-file -encoding ascii $pipDotBat;
			}
		}
	}
}