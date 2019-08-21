# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.


import io
import os
import sys
import traceback

def main():
    cwd, testRunner, secret, port, debugger_search_path, mixed_mode, coverage_file, test_file, args = parse_argv()
    sys.path[0] = os.getcwd()
    os.chdir(cwd)
    load_debugger(secret, port, debugger_search_path, mixed_mode)
    run(testRunner, coverage_file, test_file, args)

def parse_argv():
    """Parses arguments for use with the test launcher.
    Arguments are:
    1. Working directory.
    2. Test runner, `pytest` or `nose`
    3. debugSecret
    4. debugPort
    5. Debugger search path
    6. Mixed-mode debugging (non-empty string to enable, empty string to disable)
    7. Enable code coverage and specify filename
    8. TestFile, with a list of testIds to run
    9. Rest of the arguments are passed into the test runner.
    """

    return (sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4]), sys.argv[5], sys.argv[6], sys.argv[7], sys.argv[8], sys.argv[9:])

def load_debugger(secret, port, debugger_search_path, mixed_mode):
    # Load the debugger package
    try:
        if debugger_search_path:
            sys.path.append(debugger_search_path)
        
        if secret and port:
            # Start tests with legacy debugger
            import ptvsd
            from ptvsd.debugger import DONT_DEBUG, DEBUG_ENTRYPOINTS, get_code
            from ptvsd import enable_attach, wait_for_attach

            DONT_DEBUG.append(os.path.normcase(__file__))
            DEBUG_ENTRYPOINTS.add(get_code(main))
            enable_attach(secret, ('127.0.0.1', port), redirect_output = True)
            wait_for_attach()
        elif port:
            # Start tests with new debugger
            from ptvsd import enable_attach, wait_for_attach
            
            enable_attach(('127.0.0.1', port), redirect_output = True)
            wait_for_attach()
        elif mixed_mode:
            # For mixed-mode attach, there's no ptvsd and hence no wait_for_attach(), 
            # so we have to use Win32 API in a loop to do the same thing.
            from time import sleep
            from ctypes import windll, c_char
            while True:
                if windll.kernel32.IsDebuggerPresent() != 0:
                    break
                sleep(0.1)
            try:
                debugger_helper = windll['Microsoft.PythonTools.Debugger.Helper.x86.dll']
            except WindowsError:
                debugger_helper = windll['Microsoft.PythonTools.Debugger.Helper.x64.dll']
            isTracing = c_char.in_dll(debugger_helper, "isTracing")
            while True:
                if isTracing.value != 0:
                    break
                sleep(0.1)
    except:
        traceback.print_exc()
        print('''
Internal error detected. Please copy the above traceback and report at
https://github.com/Microsoft/vscode-python/issues/new

Press Enter to close. . .''')
        try:
            raw_input()
        except NameError:
            input()
        sys.exit(1)

def run(testRunner, coverage_file, test_file, args):
    """Runs the test
    testRunner -- test runner to be used `pytest` or `nose`
    args -- arguments passed into the test runner
    """

    if test_file and os.path.exists(test_file):
        with io.open(test_file, 'r', encoding='utf-8') as tests:
            args.extend(t.strip() for t in tests)

    cov = None
    try:
        if coverage_file:
            try:
                import coverage
                cov = coverage.coverage(coverage_file)
                cov.load()
                cov.start()
            except:
                pass

        if testRunner == 'pytest':
            import pytest
            pytest.main(args)
        else:
            import nose
            nose.run(argv=args)
        sys.exit(0)
    finally:
        pass
        if cov is not None:
            cov.stop()
            cov.save()
            cov.xml_report(outfile = coverage_file + '.xml', omit=__file__)

if __name__ == '__main__':
    main()
