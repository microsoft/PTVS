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
    
    try:
        import ptvsd
    except ImportError:
        ptvsd = None
    else:
        from ptvsd.visualstudio_py_debugger import DONT_DEBUG
        from ptvsd.attach_server import PTVS_VER, DEFAULT_PORT, enable_attach, wait_for_attach
    
    parser = OptionParser(prog = 'visualstudio_py_testlauncher', usage = 'Usage: %prog [<option>] <test names>... ', version = '%prog ' + PTVS_VER)
    parser.add_option('-s', '--secret', metavar = '<secret>', help = 'restrict server to only allow clients that specify <secret> when connecting')
    parser.add_option('-p', '--port', type='int', default = DEFAULT_PORT, metavar = '<port>', help = 'listen for debugger connections on <port>')
    parser.add_option('-t', '--test', type='str', dest = 'tests', action = 'append', help = 'specifies a test to run')
    parser.add_option('-m', '--module', type='str', help = 'name of the module to import the tests from')
    
    (opts, _) = parser.parse_args()
    
    sys.path[0] = os.getcwd()
    
    if ptvsd and opts.secret and opts.port:
        DONT_DEBUG.append(__file__)
        enable_attach(opts.secret, ('127.0.0.1', opts.port), redirect_output = True)
        wait_for_attach()
    
    __import__(opts.module)
    module = sys.modules[opts.module]
    test = unittest.defaultTestLoader.loadTestsFromNames(opts.tests, module)
    runner = unittest.TextTestRunner(verbosity=0)
    
    result = runner.run(test)
    sys.exit(not result.wasSuccessful())
