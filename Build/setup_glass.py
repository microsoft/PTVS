import os
import shutil
import subprocess
import sys
import zipfile

def setup_glass():
    # First step, get the drop exe installed if not already there. This may query the user for credentials.
    drop_tools_output_path = os.path.join(os.path.dirname(__file__), "..", "DropTools")
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
    drop_exe_path = os.path.join(drop_tools_output_path, "lib", "net45", "drop.exe")
    if not os.path.exists(drop_exe_path):   
        print(f"Error: drop.exe not found at {drop_exe_path}")
        exit(1)

    glass_dir = os.path.join(os.path.dirname(__file__), "..", "GlassTests")
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
        print(f"Error getting glass: {glass_installer.stderr}")
        exit(1)

    # It downloads a VSIX file, which we need to extract
    vsix_file = os.path.join(glass_dir, "Microsoft.VisualStudio.TestTools.TestPlatform.V2.CLI.vsix")
    if not os.path.exists(vsix_file):
        print(f"Error: VSIX file not found at {vsix_file}")
        exit(1)
    
    # Unzip the VSIX file into the glass directory
    zipfile.ZipFile(vsix_file, 'r').extractall(glass_dir)

    # Verify that the test console app is in the glass directory
    test_console_app = os.path.join(glass_dir, "vstest.console.exe")
    if not os.path.exists(test_console_app):
        print(f"Error: Test console app not found at {test_console_app}")
        exit(1)


if __name__ == "__main__":
    setup_glass()
    