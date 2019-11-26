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

import sys

# HACK: macOS sets __cached__ to None for __main__
# but site.main() cannot handle it. So we force it to a str.
__cached__ = ''

if 'site' in sys.modules:
    raise RuntimeError('script must be run with -S')

BEFORE_SITE = list(sys.path)

import site
try:
    site.main()
except:
    import traceback
    traceback.print_exc(file=sys.stderr)
AFTER_SITE = list(sys.path)

import os
def clean(path):
    if path:
        return os.path.normcase(os.path.abspath(path).rstrip(os.sep))
    return None

BEFORE_SITE = set(clean(p) for p in BEFORE_SITE)
AFTER_SITE = set(clean(p) for p in AFTER_SITE)
SCRIPT_DIR = clean(os.path.dirname(os.path.realpath(__file__)))

try:
    SITE_PKGS = set(clean(p) for p in site.getsitepackages())
except AttributeError:
    SITE_PKGS = set()

for prefix in [
    sys.prefix,
    sys.exec_prefix,
    getattr(sys, 'real_prefix', ''),
    getattr(sys, 'base_prefix', '')
]:
    if not prefix:
        continue

    BEFORE_SITE.add(clean(prefix))
    
    for subdir in ['DLLs', 'Lib', 'Scripts']:
        d = clean(os.path.join(prefix, subdir))
        BEFORE_SITE.add(d)

BEFORE_SITE.discard(None)
AFTER_SITE.discard(None)

import zipfile

for p in sys.path:
    p = clean(p)
    
    if p == SCRIPT_DIR or p.startswith(SCRIPT_DIR + os.sep):
        continue

    if not os.path.isdir(p) and not (os.path.isfile(p) and zipfile.is_zipfile(p)):
        continue

    if p in BEFORE_SITE:
        print("%s|stdlib|" % p)
    elif p in AFTER_SITE:
        if p in SITE_PKGS:
            print("%s|site|" % p)
        else:
            print("%s|pth|" % p)
