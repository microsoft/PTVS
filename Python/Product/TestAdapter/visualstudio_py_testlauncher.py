 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

if __name__ == '__main__':
    import os
    import sys
    import unittest
    from optparse import OptionParser
    
    parser = OptionParser(prog = 'visualstudio_py_testlauncher', usage = 'Usage: %prog [<option>] <test names>... ')
    parser.add_option('-s', '--secret', metavar = '<secret>', help = 'restrict server to only allow clients that specify <secret> when connecting')
    parser.add_option('-p', '--port', type='int', metavar = '<port>', help = 'listen for debugger connections on <port>')
    parser.add_option('-t', '--test', type='str', dest = 'tests', action = 'append', help = 'specifies a test to run')
    parser.add_option('-m', '--module', type='str', help = 'name of the module to import the tests from')
    parser.add_option('--search', type='str', action='append')
    
    (opts, _) = parser.parse_args()
    
    sys.path[0] = os.getcwd()
    
    if opts.secret and opts.port:
        from ptvsd.visualstudio_py_debugger import DONT_DEBUG
        from ptvsd.attach_server import DEFAULT_PORT, enable_attach, wait_for_attach

        DONT_DEBUG.append(__file__)
        enable_attach(opts.secret, ('127.0.0.1', getattr(opts, 'port', DEFAULT_PORT)), redirect_output = True)
        wait_for_attach()
    
    # Temporarily insert --search values at the start of sys.path while we are
    # importing the test. Then we remove them while we run tests. This allows us
    # to import load tests from packages without affecting tests that want to
    # import from explicit search paths.
    sys.path[:0] = opts.search
    try:
        __import__(opts.module)
    finally:
        del sys.path[:len(opts.search)]
    module = sys.modules.pop(opts.module)
    test = unittest.defaultTestLoader.loadTestsFromNames(opts.tests, module)
    runner = unittest.TextTestRunner(verbosity=0)
    
    result = runner.run(test)
    sys.exit(not result.wasSuccessful())
