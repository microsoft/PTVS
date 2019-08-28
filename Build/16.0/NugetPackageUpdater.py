from lxml import html
import requests

#How to use this script
#Install xlml and requests
#Run script and type 1 to have comments in the output file stating the packages that were updated. Else 0 for no comments

#Global variables

original_package_config_file = "packages.config"
new_package_config_file = "updated_packages.config"

ignore_packages = ["Microsoft.VisualStudio.Python.LanguageServer", "Microsoft.Internal.VisualStudio.Shell.Embeddable"]
new_config_file_output = []

def process_latest_version(id, old_version, new_version, show_update_comment):
    if old_version == new_version:
        new_config_file_output.append("<package id=\"" + id +"\" version = \"" + new_version + "\" />")
        print("\tNewer version not available")
    else:
        if show_update_comment:
            new_config_file_output.append("<package id=\"" + id +"\" version = \"" + new_version + "\" /> <!-- " + old_version + " -> " + new_version + " -->")
        else:
            new_config_file_output.append("<package id=\"" + id +"\" version = \"" + new_version + "\" />")
            print("-------------------------------------------- Newer version detected --------------------------------------------")
        print("\tOld version = \"" + old_version + "\"")
        print("\tNew version = \"" + new_version + "\"")

def process_package(line, id, version, show_update_comment):
    if id.startswith("Microsoft.") and (id not in ignore_packages):
        latest_version = get_latest_package_version(url = ("https://www.nuget.org/packages/" + id))
        process_latest_version(id = id, old_version = version, new_version = latest_version, show_update_comment = show_update_comment)

    else:
        print("\tEither package ID does not begin with \"Microsoft.\" or it is being ignored: \n")
        new_config_file_output.append(line)

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

    with open(original_package_config_file, 'r') as f:
        for line in f.readlines():

            try:
                line = line.strip()
                print("Input: \"" + line.strip() + "\"")

                if line.strip().startswith('<package id'):
                    elements = html.fromstring(line).xpath("//package")
                    id = elements[0].attrib["id"]
                    version = elements[0].attrib["version"]
                    process_package(line = line, id = id, version = version, show_update_comment = show_update_comment)
                else:
                    print("\tResult: Line does not start with \"<package id\", so it's being ignored. Ignored files are configured in script \n")
                    new_config_file_output.append(line)

                print("\n\n")
            except Exception as e:
                print("UNKNOWN ERROR")
                print(e)

    #Printing the results
    for package in ignore_packages:
        print("Ignored package: " + package + "\n")

    with open(new_package_config_file, 'w') as file_handle:
        for item in new_config_file_output:
            if item.strip().startswith("<package id="):
                item = "  " + item

            file_handle.write(item + "\n")

    print("\n\n")
    print("Results have been written to: " + new_package_config_file)
    print("\n")
    print("-------------------------------------- Finished Exeucuting --------------------------------------")

if __name__ == '__main__':
    main()