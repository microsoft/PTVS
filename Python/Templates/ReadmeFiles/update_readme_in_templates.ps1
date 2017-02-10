#
# Overwrites the readme files in certain templates based on the source files
# in this directory. Modifications should be checked in.
#

$kinectGameTargets = @("..\Core\ProjectTemplates\Python\KinectGame")
$pollsDjangoTargets = @("..\Django\ProjectTemplates\Python\Web\PollsDjango")
$pyvotProjectTargets = @("..\Core\ProjectTemplates\Python\PyvotProject")
$starterDjangoProjectTargets = @("..\Django\ProjectTemplates\Python\Web\StarterDjangoProject")
$webRoleConfigurationTargets = @(
    "..\Web\ItemTemplates\Python\AzureCSWebRole",
    "..\Web\ProjectTemplates\Python\Web\WebRoleBottle",
    "..\Django\ProjectTemplates\Python\Web\WebRoleDjango",
    "..\Web\ProjectTemplates\Python\Web\WebRoleEmpty",
    "..\Web\ProjectTemplates\Python\Web\WebRoleFlask"
)
$workerRoleConfigurationTargets = @(
    "..\Web\ItemTemplates\Python\AzureCSWorkerRole",
    "..\Web\ProjectTemplates\Python\Web\WorkerRoleProject"
)

$kinectGameTargets | %{ copy -Force KinectGame\readme.html $_ }
$pollsDjangoTargets | %{ copy -Force PollsDjango\readme.html $_ }
$pyvotProjectTargets | %{ copy -Force PyvotProject\readme.html $_ }
$starterDjangoProjectTargets | %{ copy -Force StarterDjangoProject\readme.html $_ }
$webRoleConfigurationTargets | %{ copy -Force WebRoleConfiguration\readme.html $_ }
$workerRoleConfigurationTargets | %{ copy -Force WorkerRoleConfiguration\readme.html $_ }
