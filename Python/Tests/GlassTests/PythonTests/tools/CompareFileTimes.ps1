<# 
.SYNOPSIS
Returns 1 if the timestamp of path1 is newer than path2, and 0 otherwise (if they are same or if path1 is older than path2).
Callers must ensure that path1 and path2 are valid files.
  
.EXAMPLE
./CompareFileTimes.ps1 "a.cpp" "b.exe"
#>

[CmdletBinding()]
Param(
	[Parameter(Mandatory=$True,Position=1)]
	[string]$path1,
	
	[Parameter(Mandatory=$True,Position=2)]
	[string]$path2
)

$date1=(ls $path1).LastWriteTime
$date2=(ls $path2).LastWriteTime
    
if ($date1 -gt $date2)
{
    exit 1
}