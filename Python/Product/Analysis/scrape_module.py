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
__version__ = "3.2"

import io, sys

SEARCH_PATH = sys.argv[1]
MODULE_NAME = sys.argv[2]

sys.path.append(SEARCH_PATH)
root_mod = mod = __import__(MODULE_NAME)

IMPORTED = set()

DEF_TYPES = frozenset([
    'builtins.builtin_function_or_method'
])

CLASS_TYPES = frozenset([
    'builtins.type'
])

for bit in MODULE_NAME.split('.')[1:]:
    mod = getattr(mod, bit)

for n in dir(mod):
    try:
        v = getattr(mod, n)
        t_v = type(v)
        t_v_n = t_v.__name__
        t_v_p = getattr(t_v, '__module__', '')
    except Exception:
        print(n + " = object()")
    else:
        if t_v_p:
            if t_v_p not in IMPORTED:
                print("import " + t_v_p)
                IMPORTED.add(t_v_p)
            t_v_p += '.'
        if t_v_p + t_v_n in DEF_TYPES:
            print("def " + n + "(): pass")
        elif t_v_p + t_v_n in CLASS_TYPES:
            print("class " + n + ": pass")
        else:
            print(n + " = " + t_v_p + t_v_n + "()")

