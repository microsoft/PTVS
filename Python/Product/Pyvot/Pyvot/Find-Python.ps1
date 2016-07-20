# PyVot
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

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