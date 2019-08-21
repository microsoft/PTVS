
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import absolute_import, print_function

import sys
import unittest
import inspect
import types

def discover(pytestargs=None, hidestdio=False,
             _pytest_main=None, _plugin=None, **_ignored):
    
    suites = unittest.defaultTestLoader.discover(pytestargs[0], pytestargs[1])
    
    #Print errors if not Python 2.7 because .errors wont exist
    if ((sys.version_info[0] >= 3) and 
        unittest.defaultTestLoader.errors):
        for error in unittest.defaultTestLoader.errors:
            print(error)

    root={}

    for suite in suites._tests:
        for cls in suite._tests:
            try:
                for test in cls._tests:
                    try:    
                        #Find source, lineno from TestId and add them to test object
                        parts = test.id().split('.')
                        error_case, error_message = None, None
                    
                        #Used loadTestsFromName(..) as an example of finding source of a function
                        #https://github.com/python/cpython/blob/master/Lib/unittest/loader.py

                        parts_copy = parts[:]
                        while parts_copy:
                            try:
                                module_name = '.'.join(parts_copy)
                                module = __import__(module_name)
                                break
                            except ImportError:
                                next_attribute = parts_copy.pop()
                            
                        parts = parts[1:]
                    
                        obj = module
                        for part in parts:
                            try:
                                parent, obj = obj, getattr(obj, part)
                            except AttributeError as e:
                                pass

                        if (isinstance(obj, types.FunctionType) or
                            ((sys.version_info[0] < 3) and isinstance(obj, types.UnboundMethodType))):
                        
                            #workaround for decorators on functions return the source of decorator and not the actual function
                            #We return the finename of our parent which should be the class, works even if class also has a decorator
                            filename = inspect.getsourcefile(parent)
                            setattr(test, 'source', filename)

                            _, lineno = inspect.getsourcelines(obj)
                            setattr(test, 'lineno', lineno)
                    except:
                        pass
            except:
                pass
    return (
            {},
            suites,
            )