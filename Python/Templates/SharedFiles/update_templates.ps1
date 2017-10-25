#
# Overwrites the common files in certain templates based on the source files
# in this directory. Modifications should be checked in.
#

pushd (split-path $MyInvocation.MyCommand.Path)

try {
    $web_role_common = @('ps.cmd', 'ConfigureCloudService.ps1') | %{ gi $_ }
    $worker_role_common = @('ps.cmd', 'ConfigureCloudService.ps1', 'LaunchWorker.ps1') | %{ gi $_ }

    $web_role_targets = @(
        "..\Web\ItemTemplates\Python\AzureCSWebRole",
        "..\Web\ProjectTemplates\Python\Web\WebRoleBottle",
        "..\Django\ProjectTemplates\Python\Web\WebRoleDjango",
        "..\Web\ProjectTemplates\Python\Web\WebRoleEmpty",
        "..\Web\ProjectTemplates\Python\Web\WebRoleFlask"
    ) | %{ gi $_ }

    $worker_role_targets = @(
        "..\Web\ItemTemplates\Python\AzureCSWorkerRole",
        "..\Web\ProjectTemplates\Python\Web\WorkerRoleProject"
    ) | %{ gi $_ }

    $web_role_targets | %{ copy $web_role_common $_ }
    $worker_role_targets | %{ copy $worker_role_common $_ }
} finally {
    popd
}