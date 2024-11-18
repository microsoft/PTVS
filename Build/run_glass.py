import os
import subprocess
import sys


def run_glass():
    glass_dir = os.path.join(os.path.dirname(__file__), "..", "GlassTests")
    test_console = os.path.join(glass_dir, "vstest.console.exe")
    tests_dir = os.path.join(glass_dir, "PythonTests")
    tests_source = os.path.join(tests_dir, "PythonConcord.GlassTestRoot")
    test_filter = f"/Tests:{sys.argv[1]}" if len(sys.argv) > 1 else ""
    
    # Verify test_console exists
    if not os.path.exists(test_console):
        print(f"Error: Test console app not found at {test_console}. Make sure you run setup_glass.py first.")
        exit(1)

    # Run the tests
    print(f"Running glass tests with test filter: {test_filter}")
    tests = subprocess.run(
        [test_console,
        tests_source,
        test_filter], stdout=sys.stdout, stderr=sys.stderr)
    
    if tests.returncode != 0:
        print(f"Error running tests: {tests.stderr}")
        exit(1)



if __name__ == "__main__":
    run_glass()