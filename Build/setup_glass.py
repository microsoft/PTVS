import os
import subprocess
import sys

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

    # Next, install glass in the GlassStandalone directory
    drop_exe_path = os.path.join(drop_tools_output_path, "lib", "net45", "drop.exe")
    if not os.path.exists(drop_exe_path):   
        print(f"Error: drop.exe not found at {drop_exe_path}")
        exit(1)

    glass_standalone_dir = os.path.join(os.path.dirname(__file__), "..", "GlassStandalone")
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
         glass_standalone_dir], stdout=sys.stdout, stderr=sys.stderr)
    if glass_installer.returncode != 0:
        print(f"Error getting glass: {glass_installer.stderr}")
        exit(1)


if __name__ == "__main__":
    setup_glass()
    