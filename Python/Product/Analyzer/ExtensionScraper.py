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

import sys
import PythonScraper

try:
    # disable error reporting in our process, bad extension modules can crash us, and we don't
    # want a bunch of Watson boxes popping up...
    import ctypes 
    ctypes.windll.kernel32.SetErrorMode(3)  # SEM_FAILCRITICALERRORS /  SEM_NOGPFAULTERRORBOX
except:
    pass

# Scrapes the file and saves the analysis to the specified filename, exits w/ nonzero exit code if anything goes wrong.
# Usage: ExtensionScraper.py scrape [mod_name or '-'] [mod_path or '-'] [output_path]

if len(sys.argv) != 5 or sys.argv[1].lower() != 'scrape':
    raise ValueError('Expects "ExtensionScraper.py scrape [mod_name|'-'] [mod_path|'-'] [output_path]"')

mod_name, mod_path, output_path = sys.argv[2:]
module = None

if mod_name and mod_name != '-':
    remove_sys_path_0 = False
    try:
        if mod_path and mod_path != '-':
            import os.path
            if os.path.exists(mod_path):
                sys.path.insert(0, mod_path)
                remove_sys_path_0 = True
        __import__(mod_name)
        module = sys.modules[mod_name]
    finally:
        if remove_sys_path_0:
            del sys.path[0]

        if not module:
            print('__import__("' + mod_name + '")')
            PythonScraper.write_analysis(output_path, {"members": {}, "doc": "Could not import compiled module"})
elif mod_path and mod_path != '-':
    try:
        import os.path
        mod_name = os.path.split(mod_path)[1].partition('.')[0]
        try:
            import importlib
            module = importlib.import_module(mod_name)
        except ImportError:
            # Don't really care which import failed - we'll try imp
            pass
        if not module:
            import imp
            module = imp.load_dynamic(mod_name, mod_path)
    finally:
        if not module:
            print('imp.load_dynamic("' + mod_name + '", "' + mod_path + '")')
            PythonScraper.write_analysis(output_path, {"members": {}, "doc": "Could not import compiled module", "filename": mod_path})
else:
    raise ValueError('No module name or path provided')

if module:
    analysis = PythonScraper.generate_module(module)
    PythonScraper.write_analysis(output_path, analysis)
