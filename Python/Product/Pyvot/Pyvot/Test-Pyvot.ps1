# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

<#
.SYNOPSIS
    Builds Pyvot and runs its tests in the requested interpreter version(s)
.EXAMPLE
    Test-Pyvot.ps1 3.2
.EXAMPLE
	Test-Pyvot.ps1 (3.2, 2.7)
.NOTES
    This script must reside alongside Find-Python.ps1 inside the Pyvot source root
	The current working directory does not matter.
#>

param ([string[]] $interpreterVersions = ("3.2", "2.7"))

$ErrorActionPreference = "Stop";

function Test-Pyvot() {
	param ( [ValidateScript({Test-Path $_})] 
			[string] $pythondir,
			[ValidatePattern('[23].\d')]
			[string] $version)

	function log() { process { write-host $_ -foregroundcolor green; } }

	$python = Join-Path $pythondir "python.exe";
	"Using Python interpreter at $python" | log;
	
	$is_py3 = $version -match "3.\d";
	"`tPython 3: $is_py3" | log;
	
	if ($is_py3) { $build_dir = ".\build-py3"; } else { $build_dir = ".\build-py2"; }
	"Building python source into $build_dir\lib" | log
	if (-not (test-path -PathType leaf "setup.py")) { throw "This script must be located in the Pyvot source root"; }
	& $python setup.py build_py
	if (-not $?) { throw "setup.py failed with code $LastExitCode; check output above"; }
	if (-not (test-path -PathType container "$build_dir\lib\xl")) { throw "setup.py should have put the 'xl' package in $build_dir\lib"; }

	"Copying test suite to $build_dir" | log
	if (test-path -PathType container "$build_dir\test") { rm -recurse -force "$build_dir\test" }
	copy-item -Recurse -Container .\test $build_dir;

	if ($is_py3) {
		"Running 2to3 on tests" | log
		& $python $pythondir"\Tools\Scripts\2to3.py" -w "$build_dir\test\TestXl.py";
		if (-not $?) { throw "2to3 conversion failed"; }
	} else {
		"Skipping 2to3 conversion (2.x interpreter)" | log;
	}

	"Starting tests" | log
	$env:PYTHONPATH = ".\build-py3\lib\";
	& $python "$build_dir\test\TestXl.py";
	if (-not $?) { throw "Tests failed"; }

	"Tests passed (interpreter: $python)" | log

}

Set-Location (Split-Path ($MyInvocation.MyCommand.Path))
. .\Find-Python.ps1
$interpreterVersions |% { Test-Pyvot (Find-Python-InstallPath $_) $_ };