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

"""
Bootstrap loader for ptvsd, used only for local attach scenarios. See PyDebugAttach
project for usage details.
"""

import sys
import os.path

was_threading_loaded = 'threading' in sys.modules

ptvs_lib_path = os.path.dirname(__file__)
sys.path.insert(0, ptvs_lib_path)
try:
    # PyDebugAttach will look for these identifiers in globals after executing this script.    
    from ptvsd.debugger import attach_process, new_thread, new_external_thread, set_debugger_dll_handle
finally:
    sys.path.remove(ptvs_lib_path)

# If threading was not loaded before ptvsd imports, it must still be unloaded afterwards.
# It assumes that when it's loaded, that happens on the main thread, and there are no other
# threads in the process - and saves the current thread ID as that of the main thread. In
# local attach, however, this code runs on the injected debugger thread; and if threading
# is loaded on that thread, it will misidentify it as a main thread, breaking things later.
assert(was_threading_loaded or 'threading' not in sys.modules)
