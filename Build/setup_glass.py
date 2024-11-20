import os
import re
import shutil
import glob
import subprocess
import sys
from typing import Callable, TypedDict
import zipfile

ptvs_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
drop_tools_output_path = os.path.abspath(os.path.join(ptvs_root, "DropTools"))
drop_exe_path = os.path.join(drop_tools_output_path, "lib", "net45", "drop.exe")
glass_dir = os.path.abspath(os.path.join(ptvs_root, "GlassTests"))
glass_debugger_dir = os.path.join(glass_dir, "Glass")
glass_remote_debugger_dir = os.path.join(glass_debugger_dir, "Remote Debugger", "x64")
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

def compute_drop_path(drop_prefix: str, matcher: Callable[[str], bool]) -> str:
    # Compute drop path by querying the drop location for all the drops
    print(f"Computing drop path for {drop_prefix}...")
    drop_list = subprocess.run(
        [drop_exe_path, 
         "list", 
         "-s", 
         "https://devdiv.artifacts.visualstudio.com/DefaultCollection", 
         "-p", 
         drop_prefix], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if drop_list.returncode != 0:
        print(f"Error listing glass drops: {drop_list.stderr}")
        exit(1)
    
    # Parse the output to get the latest drop
    drops = drop_list.stdout.decode("utf-8").split("\n")

    # Remove CR/LF
    drops = [drop.strip() for drop in drops]

    # Remove the timestamps on the front if they exist
    drops = [drop.split(" - ")[1] if " - " in drop else drop for drop in drops]

    # Find the last drop that matches the matcher
    matches = [drop for drop in drops if matcher(drop)]
    line = matches[-1]

    print(f"Picked drop: {line}")
    return line

def get_drop(drop_prefix: str, dest: str, matcher: Callable[[str], bool]) -> None:
    # Get the latest drop that matches the glass prefix
    latest_drop = compute_drop_path(drop_prefix, matcher)

    # Then get the drop
    drop_run = subprocess.run(
        [drop_exe_path, 
         "get", 
         "-s", 
         "https://devdiv.artifacts.visualstudio.com/DefaultCollection",  
         "-a", 
         "-writable", 
         "true", 
         "-n", 
         latest_drop, 
         "-d", 
         dest], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if drop_run.returncode != 0:
        print(f"Error getting drop: {drop_run.stderr}")
        exit(1)

    print(f"Got drop {latest_drop}")

def unzip_vsix(dest: str, vsix_file_name: str) -> None:
    vsix_file = os.path.join(dest, vsix_file_name)
    if not os.path.exists(vsix_file):
        print(f"Error: VSIX file not found at {vsix_file}")
        exit(1)
   
    zipfile.ZipFile(vsix_file, 'r').extractall(dest)


def get_glass():      
    # First remove the glass_dir
    # os.system('taskkill /f /im GlassTestAdminRunner.exe')  This doesn't work because the process is running as admin
    if os.path.exists(glass_dir):
        shutil.rmtree(glass_dir)

    # Get the latest drop that matches the glass prefix
    get_drop("Temp/DevDiv/Concord", glass_dir, lambda d: "GlassStandalone" in d)

    # Copy the Glass.TestAdapter.dll into the extensions directory so vstest.console.exe can find it
    glass_test_adapter = os.path.join(glass_dir, "Glass.TestAdapter.dll")
    if not os.path.exists(glass_test_adapter):
        print(f"Error: Glass.TestAdapter.dll not found at {glass_test_adapter}")
    glass_extensions_dir = os.path.join(glass_dir, "extensions")
    os.makedirs(glass_extensions_dir, exist_ok=True)
    shutil.copy(glass_test_adapter, glass_extensions_dir)


def get_test_console_app():
    # Get the latest drop that matches the nuget prefix. We need some of the assemblies
    # from this location. We're looking for the release-5.11 drop.
    get_drop("Products/DevDiv/NuGet-NuGet.Client-Trusted/release-5.11.x", glass_dir, lambda d: "5.11" in d)

    # It downloads a VSIX file, which we need to extract
    unzip_vsix(glass_dir, "Nuget.tools.vsix")

    # Get the latest drop that matches the vstest prefix
    get_drop("Products/internal/microsoft-vstest/main", glass_dir, lambda d: "main" in d)

    # It downloads a VSIX file, which we need to extract
    unzip_vsix(glass_dir, "Microsoft.VisualStudio.TestTools.TestPlatform.V2.CLI.vsix")

    # Verify that the test console app is in the glass directory
    if not os.path.exists(test_console_app):
        print(f"Error: Test console app not found at {test_console_app}")
        exit(1)

def copy_ptvs_bits():
    # Copy the PythonTests folder into the glass directory
    shutil.copytree(python_tests_source_dir, python_tests_target_dir, dirs_exist_ok=True)

    # Copy the output of the build into the glass debugger directory (where the debuggers are installed)
    build_output = get_build_output()
    for file in glob.glob(os.path.join(build_output, "Microsoft.Python*")):
        shutil.copy(file, glass_debugger_dir)
        shutil.copy(file, glass_remote_debugger_dir)

    # Whenever we copy new bits, we have to regenerate the Python.GlassTestGroup file
    generate_python_version_props()

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
    arch: str
    path: str

def write_python_tests_group(file: str, props_to_add: list[PythonVersionProps]):
    with open(file, "w") as f:
        f.write(f"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
        f.write(f'<GlassTestGroup xmlns="http://schemas.microsoft.com/vstudio/diagnostics/glasstestmanagement/2014">\n')
        f.write(f'  <Configurations>\n')
        for prop in props_to_add:
            f.write(f'    <StandardConfiguration Name="3{prop["minor"]}-{prop["bitness"]}" TargetArchitecture="x{prop["arch"]}">\n')
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
        arch = "64" if bitness == 64 else "86"

        # Skip anything older than 3.9
        if minor < 9:
            continue

        # Skip if we already found an item for this version/bitness
        if any(prop["minor"] == minor and prop["arch"] == arch for prop in props_to_add):
            continue

        # We need to write this to a file in the props folder
        props_file = os.path.join(props_folder, f"Python3{minor}.{bitness}.GlassTestProps")
        with open(props_file, "w") as f:
            f.write(f"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
            f.write(f'<PropertyGroup xmlns="http://schemas.microsoft.com/vstudio/diagnostics/glasstestmanagement/2014">\n')
            f.write(f'  <PythonExe CopyToEnvironment="true">{python_root}</PythonExe>\n')
            f.write(f"</PropertyGroup>\n")

        # Then we need to add this to the list of python versions in the PythonTests.props file
        props_to_add.append({ "minor": minor, "bitness": bitness, "arch": arch, "path": "..\\Python\\" + os.path.basename(props_file) })
        
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
        copy_ptvs_bits()
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
        elif step == "copy-ptvs-bits":
            copy_ptvs_bits()
        elif step == "generate-python-version-props":
            generate_python_version_props()
        elif step == "verify-listing":
            verify_listing()
        else:
            print(f"Error: Unrecognized argument: {step}")
        print(f"Glass setup step: {step}")
    