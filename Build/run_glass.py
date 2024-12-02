import os
import subprocess
import sys
from setup_glass import copy_ptvs_output


def run_glass():
    # Ensure the PTVS output is copied to the GlassTests directory
    copy_ptvs_output()

    # Get the test console app and test source directories
    glass_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "GlassTests"))
    test_console = os.path.join(glass_dir, "vstest.console.exe")
    tests_dir = os.path.join(glass_dir, "PythonTests")
    tests_source = os.path.join(tests_dir, "PythonConcord.GlassTestRoot")
    test_filter = f"/Tests:{sys.argv[1]}" if len(sys.argv) > 1 else "*"

    # Verify the tests are there.
    if not os.path.exists(tests_source):
        print(f"Error: Test source not found at {tests_source}. Make sure you run setup_glass.py first.")
        # List the directory contents to help diagnose the issue
        print(f"Contents of {tests_dir}:")
        for f in os.listdir(tests_dir):
            print(f)
        exit(1)    
    
    # Verify test_console exists
    if not os.path.exists(test_console):
        print(f"Error: Test console app not found at {test_console}. Make sure you run setup_glass.py first.")
        exit(1)

    # Run the tests
    print(f"Running glass tests with args: {test_console} {tests_source} {test_filter}")
    tests = subprocess.run(
        [test_console,
         "/Parallel",
        tests_source,
        "/logger:trx;LogFileName=PythonTests.trx",
        test_filter], stdout=sys.stdout, stderr=sys.stderr)
    
    if tests.returncode != 0:
        print(f"Error running tests: {tests.stderr}")
        exit(1)



if __name__ == "__main__":
    run_glass()