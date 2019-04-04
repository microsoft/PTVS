#
# Overwrites the readme files in certain templates based on the source files
# in this directory. Modifications should be checked in.
#

$pollsDjangoTargets = @("..\Django\ProjectTemplates\Python\Web\PollsDjango")
$starterDjangoProjectTargets = @("..\Django\ProjectTemplates\Python\Web\StarterDjangoProject")

$pollsDjangoTargets | %{ copy -Force PollsDjango\readme.html $_ }
$starterDjangoProjectTargets | %{ copy -Force StarterDjangoProject\readme.html $_ }
