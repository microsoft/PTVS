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

#Sanity checks...
$CURRPATH = split-path -parent $MyInvocation.MyCommand.Definition;
pushd $CURRPATH;

$stagedWebsite = "\\pytools\demos\hosted\tmp\";
$stagedWebprodList = "http://pytools/demos/tmp/webproductlist.xml";


if (test-path "$env:ProgramFiles\Microsoft\Web Platform Installer\WebpiCmd.exe") {
    set-alias webpicmd "$env:ProgramFiles\Microsoft\Web Platform Installer\WebpiCmd.exe";
} elseif (test-path "$env:ProgramFiles (x86)\Microsoft\Web Platform Installer\WebpiCmd.exe") {
    set-alias webpicmd "$env:ProgramFiles (x86)\Microsoft\Web Platform Installer\WebpiCmd.exe";
} else {
    echo "Install WebPI 4.0 from microsoft.com and try again!";
    exit 1;
}

echo "Copying local feeds from '$PWD' to the staged web site '$stagedWebsite'.";
#Copy over our Python/Node.js/etc. subfeed to the staging website
copy -force .\toolsproductlist.xml $stagedWebsite

#Copy over the main feed to the staging website
copy -force .\webproductlist.xml $stagedWebsite
echo "Done!"
echo ""

#Override the registry location pointing at the MAIN WebPI feed to our staging website
echo "Modifying the registry so that WebPI uses '$stagedWebprodList' for the main feed."
reg add HKLM\SOFTWARE\Microsoft\WebPlatformInstaller /f /v ProductXmlLocation /d $stagedWebprodList
echo "Done!"
echo ""
 
#Cleanup the cache by hand
echo "Forcefully cleaning up WebPI's cache...";
pushd $env:UserProfile;
rm -recurse -force '.\AppData\Local\Microsoft\Web Platform Installer' 2> $null;
rm -recurse -force '.\AppData\Roaming\Microsoft\Web Platform Installer' 2> $null;
rm -recurse -force '.\AppData\LocalLow\Microsoft\Web Platform Installer' 2> $null;
popd;
echo "Done!"
echo ""

popd;
 
#Sanity check 1
echo "If you have 32-bit Python 2.7 installed in the 'normal' location, you should see a line"
echo "starting with 'Python27' now..."
webpicmd /list /ListOption:INSTALLED  | select-string -pattern "Python27"
echo "Done!"
echo ""

#Sanity check 2
echo "Please check that the listed feeds below include:"
echo "  'toolsproductlist http://pytools/demos/tmp/toolsproductlist.xml'"
echo "now:"
webpicmd /list /ListOption:FEEDS
echo "Done!"
echo ""

echo "Now run:"
echo '  &"$env:ProgramFiles\Microsoft\Web Platform Installer\WebPlatformInstaller.exe"'
echo "to verify your changes;)"