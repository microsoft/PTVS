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
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

from __future__ import absolute_import, print_function

"""Automatically selects REPL support for Jupyter/IPython"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.1.0"

def is_version_at_least(ver_str, *version):
    try:
        for v1, v2 in zip(version, ver_str.split('.')):
            i1, i2 = int(v1), int(v2)
            if i1 != i2:
                return i1 < i2
    except ValueError:
        # Versions matched as far as we could go
        return True
    return True

USE_JUPYTER_CLIENT = False

try:
    import jupyter_client
    if is_version_at_least(jupyter_client.__version__, 5, 1):
        USE_JUPYTER_CLIENT = True
except ImportError:
    pass

if USE_JUPYTER_CLIENT:
    from .jupyter_client import JupyterClientBackend
    IPythonBackend = IPythonBackendWithoutPyLab = JupyterClientBackend

else:
    from .ipython_client import IPythonBackend, IPythonBackendWithoutPyLab

