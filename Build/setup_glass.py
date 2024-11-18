import os
import shutil
import subprocess
import sys
import zipfile

drop_tools_output_path = os.path.join(os.path.dirname(__file__), "..", "DropTools")
drop_exe_path = os.path.join(drop_tools_output_path, "lib", "net45", "drop.exe")
glass_dir = os.path.join(os.path.dirname(__file__), "..", "GlassTests")
test_console_app = os.path.join(glass_dir, "vstest.console.exe")
python_tests_source_dir = os.path.join(os.path.dirname(__file__), "..", "Python", "Tests", "GlassTests", "PythonTests")
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

def install_pyenv_windows():
    # Install pyenv-windows
    install_pyenv_exe_path = os.path.join(os.path.dirname(__file__), "install-pyenv-win.ps1")
    pyenv_getter = subprocess.run(
        ["powershell", 
         "-File", 
         install_pyenv_exe_path, 
        ], 
         stdout=sys.stdout, stderr=sys.stderr)

    if pyenv_getter.returncode != 0:
        print(f"Error getting pyenv for windows: {pyenv_getter.stderr}")
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


if __name__ == "__main__":
    # Process the command line arguments and run the appropriate steps
    if len(sys.argv) < 2:
        # All the steps
        get_drop_exe()
        install_pyenv_windows()
        get_glass()
        get_test_console_app()
        copy_python_tests()
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
        elif step == "verify-listing":
            verify_listing()
        else:
            print(f"Error: Unrecognized argument: {step}")
        print(f"Glass setup step: {step}")
    