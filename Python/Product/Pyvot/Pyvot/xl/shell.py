# PyVot
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

"""Interactive shell for the `xl` package. It can be invoked as:

    python -m xl.shell

The shell mimics the normal Python REPL, but performs additional set-up for convenient usage of `xl`.
    - `xl` is imported
    - An empty Excel workbook is opened, if no others are present
    - Open Excel workbooks are assigned variables (workbook, workbook_1, etc.).
      Each such variable and its workbook's name are printed in a table"""

import code
import xl
import itertools
import sys
import textwrap

def workbook_table_str(workbook_map):
    col_format = "{0:<20}{1:>20}\n"
    s = col_format.format( "Workbook", "Name")
    s += col_format.format("========", "====")
    for wb_var, wb in workbook_map.iteritems():
        s += col_format.format(wb_var, wb.name)
    return s

def make_banner(workbook_map):
    banner_format = """
    Entering Pyvot Shell

    %s

    Imported the `xl` module. Run `help(xl)` for a usage summary.

    Python %s
    Type "help", "copyright", "credits" or "license" for more information."""
    banner_format = textwrap.dedent(banner_format) # remove leading indentation

    banner = banner_format % (workbook_table_str(workbook_map), sys.version)
    return banner

def ensure_open_workbook():
    if not xl.workbooks():
        xl.Workbook()
    assert xl.workbooks()

def make_workbook_map():
    def _names():
        yield "workbook"
        for c in itertools.count(1): yield "workbook_%d" % c
    return dict(zip( _names(), xl.workbooks() ))

def shell_input(*args, **kwargs):
    # 2to3 only notices raw_input used as a function
    return raw_input(*args, **kwargs)

def run_shell():
    ensure_open_workbook()
    workbook_vars = make_workbook_map()
    banner = make_banner(workbook_vars)

    locals = {'__name__' : '__console__', 
              '__doc__': None,
              'license' : license,
              'copyright' : copyright,
              'xl' : xl}
    locals.update( workbook_vars )

    code.interact(banner, shell_input, locals)

if __name__ == '__main__': run_shell()
