import os
import re
import shutil
import subprocess
import sys
from typing import TypedDict
import zipfile

ptvs_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
drop_tools_output_path = os.path.abspath(os.path.join(ptvs_root, "DropTools"))
drop_exe_path = os.path.join(drop_tools_output_path, "lib", "net45", "drop.exe")
glass_dir = os.path.abspath(os.path.join(ptvs_root, "GlassTests"))
test_console_app = os.path.join(glass_dir, "vstest.console.exe")
python_tests_source_dir = os.path.abspath(os.path.join(ptvs_root, "Python", "Tests", "GlassTests", "PythonTests"))
python_tests_target_dir = os.path.join(glass_dir, "PythonTests")

def get_drop_exe():
    # First step, get the drop exe installed if not already there. This may query the user for credentials.
    get_drop_exe_path = os.path.join(os.path.dirname(__file__), "GetDropExe.ps1")
    drop_getter = subprocess.run(
        ["powershell", 
         "-File", 
         get_drop_exe_path, 
         "https://devdiv.artifacts.visualstudio.com", 
         "-d", 
         drop_tools_output_path], 
         stdout=sys.stdout, stderr=sys.stderr)
    if drop_getter.returncode != 0:
        print(f"Error getting drop.exe: {drop_getter.stderr}")
        exit(1)

    # Next, install glass in the GlassTests directory
    if not os.path.exists(drop_exe_path):   
        print(f"Error: drop.exe not found at {drop_exe_path}")
        exit(1)

def get_glass():        
    shutil.rmtree(glass_dir)
    glass_installer = subprocess.run(
        [drop_exe_path, 
         "get", 
         "-s", 
         "https://devdiv.artifacts.visualstudio.com/DefaultCollection",  
         "-a", 
         "-writable", 
         "true", 
         "-n", 
         "Temp/DevDiv/Concord/GlassStandalone1559032", # TODO: Figure out how to get a permanent drop location
         "-d", 
         glass_dir], stdout=sys.stdout, stderr=sys.stderr)
    if glass_installer.returncode != 0:
        print(f"Error getting glass: {glass_installer.stderr}")
        exit(1)

    # Copy the Glass.TestAdapter.dll into the extensions directory so vstest.console.exe can find it
    glass_test_adapter = os.path.join(glass_dir, "Glass.TestAdapter.dll")
    glass_extensions_dir = os.path.join(glass_dir, "extensions")
    os.makedirs(glass_extensions_dir, exist_ok=True)
    shutil.copy(glass_test_adapter, glass_extensions_dir)

def get_test_console_app():
    # Next install the test console app that will run the tests.
    test_bits = subprocess.run(
        [drop_exe_path,
        "get",
        "-s",
        "https://devdiv.artifacts.visualstudio.com/DefaultCollection",
        "-a",
        "-writable",
        "true",
        "-n",
        "Products/internal/microsoft-vstest/17.12/20241008.1", # TODO: Figure out how to get a permanent drop location
        "-d",
        glass_dir], stdout=sys.stdout, stderr=sys.stderr)
    if test_bits.returncode != 0:
        print(f"Error getting glass: {test_bits.stderr}")
        exit(1)

    # It downloads a VSIX file, which we need to extract
    vsix_file = os.path.join(glass_dir, "Microsoft.VisualStudio.TestTools.TestPlatform.V2.CLI.vsix")
    if not os.path.exists(vsix_file):
        print(f"Error: VSIX file not found at {vsix_file}")
        exit(1)
    
    # Unzip the VSIX file into the glass directory
    zipfile.ZipFile(vsix_file, 'r').extractall(glass_dir)

    # Verify that the test console app is in the glass directory
    if not os.path.exists(test_console_app):
        print(f"Error: Test console app not found at {test_console_app}")
        exit(1)

def copy_python_tests():
    # Copy the PythonTests folder into the glass directory
    shutil.copytree(python_tests_source_dir, python_tests_target_dir, dirs_exist_ok=True)

def verify_listing():
    # Output the tests found to verify this all worked
    print("Listing tests found in GlassTests/PythonTests:")
    tests = subprocess.run(
        [test_console_app,
        "/lt",
        f"{python_tests_target_dir}/PythonConcord.GlassTestRoot"], stdout=sys.stdout, stderr=sys.stderr)
    if tests.returncode != 0:
        print(f"Error listing tests: {tests.stderr}")
        exit(1)

def get_build_output():
    debug_path = os.path.join(ptvs_root, "BuildOutput", "Debug17.0", "raw", "binaries")
    release_path = os.path.join(ptvs_root, "BuildOutput", "Release17.0", "raw", "binaries")

    # Default to debug because that's the most likely scenario for a local dev box
    if os.path.exists(debug_path):
        return debug_path
    return release_path

class PythonVersionProps(TypedDict):
    minor: int
    bitness: int
    path: str

def write_python_tests_group(file: str, props_to_add: list[PythonVersionProps]):
    with open(file, "w") as f:
        f.write(f"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
        f.write(f'<GlassTestGroup xmlns="http://schemas.microsoft.com/vstudio/diagnostics/glasstestmanagement/2014">\n')
        f.write(f'  <Configurations>\n')
        for prop in props_to_add:
            f.write(f'    <StandardConfiguration Name="3{prop["minor"]}" TargetArchitecture="x{prop["bitness"]}">\n')
            f.write(f'      <Setup>\n')
            f.write(f'        <ImportPropertyGroup>{prop["path"]}</ImportPropertyGroup>\n')
            f.write(f'        <ImportPropertyGroup>..\\..\\Native\\InProcPdb.GlassTestProps</ImportPropertyGroup>\n')
            f.write(f'        <ImportPropertyGroup>..\\..\\Sync.GlassTestProps</ImportPropertyGroup>\n')
            f.write(f'      </Setup>\n')
            f.write(f'      <TestSetup>\n')
            f.write(f'        <ImportPropertyGroup>{prop["path"]}</ImportPropertyGroup>\n')
            f.write(f'        <RunScript Condition="Exists(\'$(TestDir)\\setup.py\')" RunAs="VSUser">..\\tools\\RunPythonSetupPy.cmd "$(PythonExe)" "$(TestDir)\\$(OutDir)"</RunScript>\n')
            f.write(f'      </TestSetup>\n')
            f.write(f'    </StandardConfiguration>\n')
        f.write(f'  </Configurations>\n')
        f.write(f'</GlassTestGroup>\n')


def generate_python_version_props():
    # This will generate the Python<Major><Bitness>.GlassTestProps files based
    # on where python is installed on this machine.
    props_folder = os.path.join(python_tests_target_dir, "Python")

    # Run the EnvironmentDiscoverer exe to find the installed versions of python.
    discover_path = os.path.join(get_build_output(), "EnvironmentDiscover.exe")
    if not os.path.exists(discover_path):
        print(f"Error: EnvironmentDiscover.exe not found at {discover_path}. Make sure you build the project first.")
        exit(1)

    discover_output = subprocess.run(
        [discover_path], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if discover_output.returncode != 0:
        print(f"Error finding python versions: {discover_output.stderr}")
        exit(1)

    props_to_add: list[PythonVersionProps] = []
    # Parse the output to get the installed versions of python
    installed_versions = discover_output.stdout.strip().decode("utf-8").split("\n")
    for version in installed_versions:
        # Version should be in the format 3.<minor>:<bitness>-bit = '<path>'
        # Use a regex to parse this
        match = re.match(r"3\.(\d+):(\d+)-bit = '(.*)'", version)
        if match is None:
            print(f"Error parsing version: {version}")
            exit(1)
        minor = int(match.group(1))
        bitness = int(match.group(2))
        python_root = match.group(3)

        # Skip anything older than 3.9
        if minor < 9:
            continue

        # Skip if we already found an item for this version/bitness
        if any(prop["minor"] == minor and prop["bitness"] == bitness for prop in props_to_add):
            continue

        # We need to write this to a file in the props folder
        props_file = os.path.join(props_folder, f"Python3{minor}.{bitness}.GlassTestProps")
        with open(props_file, "w") as f:
            f.write(f"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
            f.write(f'<PropertyGroup xmlns="http://schemas.microsoft.com/vstudio/diagnostics/glasstestmanagement/2014">\n')
            f.write(f'  <PythonExe CopyToEnvironment="true">{python_root}</PythonExe>\n')
            f.write(f"</PropertyGroup>\n")

        # Then we need to add this to the list of python versions in the PythonTests.props file
        props_to_add.append({ "minor": minor, "bitness": bitness, "path": "..\\Python\\" + os.path.basename(props_file) })
        
    # Now we need to update the PythonTests.GlassTestGroup file with the new versions
    python_tests_group = os.path.join(props_folder, "Python.GlassTestGroup")
    write_python_tests_group(python_tests_group, props_to_add)

    # Do the same for the PythonTests3.x.GlassTestGroup file
    python_tests_3x_group = os.path.join(python_tests_target_dir, "Python3x", "Python3x.GlassTestGroup")
    write_python_tests_group(python_tests_3x_group, props_to_add)

if __name__ == "__main__":
    # Process the command line arguments and run the appropriate steps
    if len(sys.argv) < 2:
        # All the steps
        get_drop_exe()
        get_glass()
        get_test_console_app()
        copy_python_tests()
        generate_python_version_props()
        verify_listing()
    else:
        step = sys.argv[1]
        if step == "get-drop-exe":
            get_drop_exe()
        elif step == "get-glass":
            get_glass()
        elif step == "get-test-console-app":
            get_test_console_app()
        elif step == "copy-python-tests":
            copy_python_tests()
        elif step == "generate-python-version-props":
            generate_python_version_props()
        elif step == "verify-listing":
            verify_listing()
        else:
            print(f"Error: Unrecognized argument: {step}")
        print(f"Glass setup step: {step}")
    