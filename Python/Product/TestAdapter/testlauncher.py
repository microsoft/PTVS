# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
import sys


def parse_argv():
    """Parses arguments for use with the test launcher.
    Arguments are:
    1. Working directory.
    2. Test runner, `pytest` or `nose`
    3. debugSecret
    4. debugPort
    5. Rest of the arguments are passed into the test runner.
    """

    return (sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4]), sys.argv[5:])


def exclude_current_file_from_debugger(secret, port):
    # Load the debugger package
    try:
        import ptvsd
        
        if secret and port:
            # Allow other computers to attach to ptvsd at this IP address and port.
            ptvsd.enable_attach(secret, address=('127.0.0.1', port), redirect_output=True)

            # Pause the program until a remote debugger is attached
            ptvsd.wait_for_attach()

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


def run(cwd, testRunner, args):
    """Runs the test
    cwd -- the current directory to be set
    testRunner -- test runner to be used `pytest` or `nose`
    args -- arguments passed into the test runner
    """
    sys.path[0] = os.getcwd()
    os.chdir(cwd)

    try:
        if testRunner == 'pytest':
            import pytest
            pytest.main(args)
        else:
            import nose
            nose.run(argv=args)
        sys.exit(0)
    finally:
        pass


if __name__ == '__main__':
    cwd, testRunner, secret, port, args = parse_argv()
    exclude_current_file_from_debugger(secret, port)
    run(cwd, testRunner, args)