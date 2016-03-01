param([switch] $clean)

$web_role_common = @('ps.cmd', 'ConfigureCloudService.ps1', 'WebRoleConfiguration.mht') | %{ gi $_ }
$worker_role_common = @('ps.cmd', 'ConfigureCloudService.ps1', 'LaunchWorker.ps1', 'WorkerRoleConfiguration.mht') | %{ gi $_ }

$web_role_targets = @(
    "..\ItemTemplates\CloudService\NETFramework4\Web Role\Python\CloudServiceBottleWebRole",
    "..\ItemTemplates\CloudService\NETFramework4\Web Role\Python\CloudServiceDjangoWebRole",
    "..\ItemTemplates\CloudService\NETFramework4\Web Role\Python\CloudServiceEmptyWebRole",
    "..\ItemTemplates\CloudService\NETFramework4\Web Role\Python\CloudServiceFlaskWebRole",
    "..\ItemTemplates\Python\AzureCSWebRole",
    "..\ProjectTemplates\WebRoleBottle",
    "..\ProjectTemplates\WebRoleDjango",
    "..\ProjectTemplates\WebRoleEmpty",
    "..\ProjectTemplates\WebRoleFlask"
) | %{ gi $_ }

$worker_role_targets = @(
    "..\ItemTemplates\CloudService\NETFramework4\Worker Role\Python\CloudServiceWorkerRole",
    "..\ItemTemplates\Python\AzureCSWorkerRole",
    "..\ProjectTemplates\WorkerRoleProject"
) | %{ gi $_ }

if ($clean) {
    $web_role_targets | %{ $root = "$_"; $web_role_common.Name | %{ del "$root\$_" } }
    $worker_role_targets | %{ $root = "$_"; $worker_role_common.Name | %{ del "$root\$_" } }
} else {
    $web_role_targets | %{ copy $web_role_common $_ }
    $worker_role_targets | %{ copy $worker_role_common $_ }
}
