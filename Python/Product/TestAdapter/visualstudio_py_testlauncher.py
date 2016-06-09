# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

def main():
    import os
    import sys
    import unittest
    from optparse import OptionParser
    
    parser = OptionParser(prog = 'visualstudio_py_testlauncher', usage = 'Usage: %prog [<option>] <test names>... ')
    parser.add_option('-s', '--secret', metavar='<secret>', help='restrict server to only allow clients that specify <secret> when connecting')
    parser.add_option('-p', '--port', type='int', metavar='<port>', help='listen for debugger connections on <port>')
    parser.add_option('-x', '--mixed-mode', action='store_true', help='wait for mixed-mode debugger to attach')
    parser.add_option('-t', '--test', type='str', dest='tests', action='append', help='specifies a test to run')
    parser.add_option('-m', '--module', type='str', help='name of the module to import the tests from')
    parser.add_option('-c', '--coverage', type='str', help='enable code coverage and specify filename')
    (opts, _) = parser.parse_args()
    
    sys.path[0] = os.getcwd()
    
    if opts.secret and opts.port:
        from ptvsd.visualstudio_py_debugger import DONT_DEBUG, DEBUG_ENTRYPOINTS, get_code
        from ptvsd.attach_server import DEFAULT_PORT, enable_attach, wait_for_attach

        DONT_DEBUG.append(os.path.normcase(__file__))
        DEBUG_ENTRYPOINTS.add(get_code(main))

        enable_attach(opts.secret, ('127.0.0.1', getattr(opts, 'port', DEFAULT_PORT)), redirect_output = True)
        wait_for_attach()
    elif opts.mixed_mode:
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

    cov = None
    try:
        if opts.coverage:
            try:
                import coverage
                cov = coverage.coverage(opts.coverage)
                cov.load()
                cov.start()
            except:
                pass
                    
        __import__(opts.module)
        module = sys.modules[opts.module]
        test = unittest.defaultTestLoader.loadTestsFromNames(opts.tests, module)
        runner = unittest.TextTestRunner(verbosity=0)
    
        result = runner.run(test)
        sys.exit(not result.wasSuccessful())

    finally:
        if cov is not None:
            cov.stop()
            cov.save()
            cov.xml_report(outfile = opts.coverage + '.xml')

if __name__ == '__main__':
    main()
