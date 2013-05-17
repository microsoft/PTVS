# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

"""Pyvot - Pythonic interface for data exploration in Excel

The user-level API for the `xl` package follows. For interactive use, consider running the :ref:`interactive shell <interactive>`::

    python -m xl.shell

**Managing Excel workbooks**:
    - :class:`xl.Workbook() <xl.sheet.Workbook>` opens a new workbook
    - xl.Workbook("filename") attaches to an existing workbook, or opens it
    - :func:`xl.workbooks() <xl.tools.workbooks>` returns a Workbook for each that is currently open

**Excel Ranges**:
    - :class:`xl.Range <xl.range.Range>` is the base type for a contiguous range of Excel cells.
    - :func:`xl.get() <xl.tools.get>` / :meth:`Workbook.get <xl.sheet.Workbook.get>` / etc. return Ranges; namely, subclasses such as
      :class:`xl.RowVector <xl.range.RowVector>`, :class:`xl.ColumnVector <xl.range.ColumnVector>`,
      :class:`xl.Matrix <xl.range.Matrix>`, or :class:`xl.Scalar <xl.range.Scalar>`
    - :meth:`xl.Range.get` / :meth:`xl.Range.set` allow reading from / writing to Excel

**Tools**:
    - :func:`xl.map <xl.tools.map>` / :func:`xl.apply <xl.tools.apply>` / :func:`xl.filter <xl.tools.filter>` operate 
      like their Python counterparts, but read and write from an Excel workbook
      ``from xl import *`` imports :func:`xlmap`, etc. instead, to avoid overriding builtins.
    - :func:`xl.join() <xl.tools.join>` allows joining two Excel tables by a pair of key columns
    - :func:`xl.get() <xl.tools.get>` fetches a Range for a table column (by column name), named Excel range, or for an 
      Excel address (ex. A1:B1). It attempts to guess the active Workbook, and begins looking in the active sheet. 
      See also :meth:`Workbook.get <xl.sheet.Workbook.get>`
    - :func:`xl.view() <xl.tools.view>` splats a list of Python values to an empty column in Excel
    - :func:`xl.selected_range() <xl.tools.selected_range>` / :func:`xl.selected_value() <xl.tools.selected_value>` 
      provide the active sheet's selection"""

try:
    __import__('win32com')
except ImportError as e:
    import ctypes
    import sys
    is_64bit = ctypes.sizeof(ctypes.c_voidp) > 4
    arch_str = "64-bit" if is_64bit else "32-bit"
    ver = "%d.%d" % (sys.version_info.major, sys.version_info.minor)
    raise Exception("pywin32 does not appear to be installed. Visit http://sourceforge.net/projects/pywin32/ and download "
                    "build 216 or above for Python %s (%s)" % (ver, arch_str), e)

from .version import __version__

# Conventions:
# - prefix excel COM objectss with "xl". Apply to field and method names.
# Design conventions:
# - Very low activation energy for users.
#   Layer between "precise (dumb)" operations (which are often not useful) and "guess user intent (smart)" operations
#   (which can be much more useful). 
#   Users start with "smart" general operations and work towards the precise ones. 
#     - Global functions user "current" workbook, which iterates all sheets.

from .range import Range, Vector, Scalar, RowVector, ColumnVector, Matrix, ExcelRangeError
from .cache import CacheManager, enable_caching, cache_result
from .tools import get, view, join, map, apply, filter, selected_range, selected_value, workbooks
from .sheet import Workbook

# We want to allow 'from xl import *' without clobbering builtin map / apply / filter.
# We define these aliases, and exclude map / apply / filter from __all__.
# This way xl.map works, but 'from xl import *' imports xlmap instead
xlmap, xlapply, xlfilter = map, apply, filter

__all__ = ['Range', 'Vector', 'Scalar', 'RowVector', 'ColumnVector', 'Matrix', 'ExcelRangeError',
           'CacheManager', 'enable_caching', 'cache_result',
           'get', 'view', 'join', 'selected_range', 'selected_value', 'workbooks',
           'xlmap', 'xlapply', 'xlfilter', # We omit map / apply / filter from __all__ but include these. See above
           'Workbook']