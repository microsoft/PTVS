#
# Overwrites the readme files in certain templates based on the source files
# in this directory. Modifications should be checked in.
#

$starterDjangoProjectTargets = @("..\Django\ProjectTemplates\Python\Web\StarterDjangoProject")

$starterDjangoProjectTargets | %{ copy -Force StarterDjangoProject\readme.html $_ }
