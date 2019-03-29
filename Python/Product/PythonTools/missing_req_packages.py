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

# This script determines if a python interpreter is missing any packages 
# As defined in a text file (usually requirements.txt) which goes into pip

# Exit code = 1 means atleast 1 package is missing or version conflict. Show install package bar
# Exit code = 0 means that packages may or may not be missing or file not find or other problems. Do not show install bar
    # If exit code = 0 and git_req >= 1, then a package may or may not be missing

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import codecs
import locale
import re
import sys
import traceback
import pkg_resources

BOMS = [
    (codecs.BOM_UTF8, 'utf-8'),
    (codecs.BOM_UTF16, 'utf-16'),
    (codecs.BOM_UTF16_BE, 'utf-16-be'),
    (codecs.BOM_UTF16_LE, 'utf-16-le'),
    (codecs.BOM_UTF32, 'utf-32'),
    (codecs.BOM_UTF32_BE, 'utf-32-be'),
    (codecs.BOM_UTF32_LE, 'utf-32-le'),
]  # type: List[Tuple[bytes, Text]]
ENCODING_RE = re.compile(br'coding[:=]\s*([-\w.]+)')

def auto_decode(data):
    # type: (bytes) -> Text
    """Check a bytes string for a BOM to correctly detect the encoding
    Fallback to locale.getpreferredencoding(False) like open() on Python3"""
    for bom, encoding in BOMS:
        if data.startswith(bom):
            return data[len(bom):].decode(encoding)
    # Lets check the first two lines as in PEP263
    for line in data.split(b'\n')[:2]:
        if line[0:1] == b'#' and ENCODING_RE.search(line):
            encoding = ENCODING_RE.search(line).groups()[0].decode('ascii')
            return data.decode(encoding)
    return data.decode(locale.getpreferredencoding(False) or sys.getdefaultencoding())

try:
    with open(sys.argv[1], "rb") as file_handle:
        req_txt_lines = auto_decode(file_handle.read()).splitlines()
except Exception:
    traceback.print_exc()
    sys.exit(0)

req_met = 0
git_req = 0
not_found = 0
version_conflict = 0
invalid_req = 0

for line in req_txt_lines:
    try:
        if line == "":
            continue            

        # "git+..." is a valid requirements.txt instruction, but it's not supported by pkg_resource
        if line.lower().startswith("git+"):
            print("Git : {}".format(line))
            git_req += 1
            continue

        pkg_resources.require(line)
        print("Installed : {}".format(line))
        req_met += 1
    except pkg_resources.DistributionNotFound:
        print("NotFound : {}".format(line))
        not_found += 1
    except pkg_resources.VersionConflict:
        print("VersionConflict : {}".format(line))
        version_conflict += 1
    except Exception:
        print("Invalid : {}".format(line))
        invalid_req += 1

print("InstalledCount : {}".format(req_met))
print("GitCount : {}".format(git_req))
print("NotFoundCount : {}".format(not_found))
print("VersionConflictCount : {}".format(version_conflict))
print("InvalidCount : {}".format(invalid_req))

if not_found or version_conflict:
    sys.exit(1)

sys.exit(0)
