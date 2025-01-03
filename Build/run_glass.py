import argparse
import os
import subprocess
import sys
from setup_glass import copy_ptvs_output


def run_glass(build_output_dir: str | None, other_args: list[str]) -> None:
    # Ensure the PTVS output is copied to the GlassTests directory
    copy_ptvs_output(build_output_dir)

    # Get the test console app and test source directories
    glass_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "GlassTests"))
    test_console = os.path.join(glass_dir, "vstest.console.exe")
    tests_dir = os.path.join(glass_dir, "PythonTests")
    tests_source = os.path.join(tests_dir, "PythonConcord.GlassTestRoot")
    test_filter = f"/Tests:{other_args[0]}" if len(other_args) > 0 else "*"

    # Verify the tests are there.
    if not os.path.exists(tests_source) or not os.path.exists(os.path.join(tests_dir, "PythonEngine.regdef")):
        print(f"Error: Test source or PythonEngine.regdef not found at {tests_source}. Make sure you run setup_glass.py first.")
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
    arg_parser = argparse.ArgumentParser(description="Use Glass to run Python mixed mode debugger tests")
    arg_parser.add_argument("--buildOutput", type=str, help="The path to the build output directory")
    arg_parser.add_argument("--?", action='store_true', help="Show help")
    args, unknown_args = arg_parser.parse_known_args()
    build_output = args.buildOutput if args.buildOutput else None
    args_dict = vars(args)

    if args_dict.get("?"):
       arg_parser.print_help()
    else:
        run_glass(build_output, unknown_args)
