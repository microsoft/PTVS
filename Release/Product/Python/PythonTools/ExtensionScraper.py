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

import imp
import sys
from os import path

try:
    # disable error reporting in our process, bad extension modules can crash us, and we don't
    # want a bunch of Watson boxes popping up...
    import ctypes 
    ctypes.windll.kernel32.SetErrorMode(3)  # SEM_FAILCRITICALERRORS /  SEM_NOGPFAULTERRORBOX
except:
    pass

# Expects either:
# scrape [filename] [output_path]
#       Scrapes the file and saves the analysis to the specified filename, exits w/ nonzero exit code if anything goes wrong.
if len(sys.argv) == 4:
    if sys.argv[1] == 'scrape':
        filename = sys.argv[2]
        mod_name = path.splitext(path.basename(filename))[0]
        try:
            module = imp.load_dynamic(mod_name, filename)
        except ImportError, e:
            print e
            sys.exit(1)

        import PythonScraper
        analysis = PythonScraper.generate_module(module)
        PythonScraper.write_analysis(sys.argv[3], analysis)

 