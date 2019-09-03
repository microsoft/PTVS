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

from lxml import html
import requests

#How to use this script
#Install xlml and requests
#Run script and type 1 to have comments in the output file stating the packages that were updated. Else 0 for no comments

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
        latest_version = get_latest_package_version(url=("https://www.nuget.org/packages/" + package_id))
        process_latest_version(package_id=package_id, old_version=version, new_version=latest_version, show_update_comment=show_update_comment)

    else:
        print("\tEither package ID does not begin with \"Microsoft.\" or it is being ignored: \n")
        NEW_CONFIG_FILE_OUTPUT.append(line)

def get_latest_package_version(url):
    html_page = requests.get(url)
    xml_tree = html.fromstring(html_page.content)
    version_elements = xml_tree.xpath('//div[@class="version-history panel-collapse collapse in"]/table/tbody[@class="no-border"]/tr/td/a')

    version = version_elements[0].attrib["title"]
    return version

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
