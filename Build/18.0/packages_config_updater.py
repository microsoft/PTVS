# Python Tools for Visual Studio
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
# MERCHANTABILITY OR NON-INFRINGEMENT.
#
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

import lxml.html as html
import subprocess
import os

#How to use this script
#Install xlml and requests
#Run script and type 1 to have comments in the output file stating the packages that were updated. Else 0 for no comments
#When done, compare the update_packages.config to the original and copy over the appropriate entries (not all should be updated)

#Global variables

ORIGINAL_PACKAGE_CONFIG_FILE = "packages.config"
NEW_PACKAGE_CONFIG_FILE = "updated_packages.config"

IGNORE_PACKAGES = ["Microsoft.VisualStudio.Python.LanguageServer", "Microsoft.Internal.VisualStudio.Shell.Embeddable"]
NEW_CONFIG_FILE_OUTPUT = []

def process_latest_version(package_id, old_version, new_version, show_update_comment):
    if old_version == new_version:
        NEW_CONFIG_FILE_OUTPUT.append("<package id=\"" + package_id +"\" version = \"" + new_version + "\" />")
        print("\tNewer version not available")
    else:
        if show_update_comment:
            NEW_CONFIG_FILE_OUTPUT.append("<package id=\"" + package_id +"\" version = \"" + new_version + "\" /> <!-- " + old_version + " -> " + new_version + " -->")
        else:
            NEW_CONFIG_FILE_OUTPUT.append("<package id=\"" + package_id +"\" version = \"" + new_version + "\" />")
            print("-------------------------------------------- Newer version detected --------------------------------------------")
        print("\tOld version = \"" + old_version + "\"")
        print("\tNew version = \"" + new_version + "\"")

def process_package(line, package_id, version, show_update_comment):
    if package_id.startswith("Microsoft.") and (package_id not in IGNORE_PACKAGES):
        latest_version = get_latest_package_version(package_id)
        process_latest_version(package_id=package_id, old_version=version, new_version=latest_version, show_update_comment=show_update_comment)

    else:
        print("\tEither package ID does not begin with \"Microsoft.\" or it is being ignored: \n")
        NEW_CONFIG_FILE_OUTPUT.append(line)

def get_latest_package_version(package_id):
    # Our path should tell us what VS version we want
    split = os.path.split(__file__)
    version = os.path.split(split[0])[1]

    # Use nuget.exe to retrieve the version list
    command = f'nuget list {package_id} -Source "https://pkgs.dev.azure.com/azure-public/vside/_packaging/msft_consumption/nuget/v3/index.json" -Prerelease'
    completed = subprocess.run(command, capture_output=True, encoding="utf8", shell=True)

    # Make sure that worked0
    if (completed.returncode == 0):
        # Search for the string that starts with the package id and has the version number in it
        matches = [s for s in completed.stdout.split('\n') if package_id in s]

        # Might be more than one match. Take the one which has the version in it if possible
        if (len(matches) > 1):
            hasVersion = [s for s in matches if version in s]
            if (len(hasVersion)):
                hasVersion.sort(reverse=True)
                return hasVersion[0].split(' ')[1]
        
        if (len(matches) > 0):
            return matches[0].split(' ')[1]
        
    raise Exception("Nuget not found or could not find a match")

def main():
    show_update_comment = False #if true, it will put comments in the output file stating the packages that were updated
    if input("Show comments in output file (type 1 for true and 0 for false): ") == "1":
        show_update_comment = True

    with open(ORIGINAL_PACKAGE_CONFIG_FILE, 'r') as f:
        for line in f.readlines():

            try:
                line = line.strip()
                print("Input: \"" + line.strip() + "\"")

                if line.strip().startswith('<package id'):
                    elements = html.fromstring(line).xpath("//package")
                    package_id = elements[0].attrib["id"]
                    version = elements[0].attrib["version"]
                    process_package(line=line, package_id=package_id, version=version, show_update_comment= show_update_comment)
                else:
                    print("\tResult: Line does not start with \"<package id\", so it's being ignored. Ignored files are configured in script \n")
                    NEW_CONFIG_FILE_OUTPUT.append(line)

                print("\n\n")
            except Exception as exception:
                print("UNKNOWN ERROR")
                print(exception)

    #Printing the results
    for package in IGNORE_PACKAGES:
        print("Ignored package: " + package + "\n")

    with open(NEW_PACKAGE_CONFIG_FILE, 'w') as file_handle:
        for item in NEW_CONFIG_FILE_OUTPUT:
            if item.strip().startswith("<package id="):
                item = "  " + item

            file_handle.write(item + "\n")

    print("\n\n")
    print("Results have been written to: " + NEW_PACKAGE_CONFIG_FILE)
    print("\n")
    print("-------------------------------------- Finished Exeucuting --------------------------------------")

if __name__ == '__main__':
    main()
