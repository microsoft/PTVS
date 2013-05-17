# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

function Find-Python-InstallPath {
	param ([string[]]$SearchVersions)
	
	$found = $false;
	
	:searchStart foreach ($version in $searchVersions) {
		("HKLM:\Software\WOW6432Node\Python\PythonCore\$version\InstallPath",
		 "HKLM:\Software\Python\PythonCore\$version\InstallPath") | foreach {
			if (test-path $_) {
				Write-Output (get-itemproperty $_)."(default)";
				$found = $true;
				break searchStart;
			}
		};
	};
	
	if (-not $found) { throw "Failed to find Python install path in the registry (version searched: $SearchVersions)"; }
}

function Find-Python3-InstallPath {
	Find-Python-InstallPath ("3.2", "3.1", "3.0");
}