# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

from xl.range import Range, Vector, RowVector, ColumnVector, Scalar
from xl.sheet import Workbook, Worksheet
from xl.cache import CacheManager, enable_caching, cache_result

from operator import isSequenceType

def workbooks():
    """Returns a list of open workbooks"""
    import xl._impl.com_utils
    return [Workbook(x) for x in xl._impl.com_utils.get_open_xlWorkbooks()]


# XL operations
def view(x, name=None, to=None):
    # if it's an array, load into excel, return the range
    return Workbook.default_workbook().view(x, name, to)


# Get the range from excel
def get(r):
    """Returns a Range for the given table column name, named range, or Excel address (ex. A1:B4).
    `get` guesses the active workbook, and begins its search on the active sheet.
    
    See also: xl.Workbook.get and xl.Workbook.range"""
    return Workbook.default_workbook().get(r)

def selected_range():
    """Gets the currently selected range. The returned range filters
    hidden cells by default"""
    wb = Workbook.default_workbook()
    xlApp = wb.xlWorkbook.Application
    return Range(xlApp.Selection, with_hidden=False).normalize() 

def selected_value():
    """Gets the values in the currently selected range. See xl.selected_range()"""
    return selected_range().get()

def filter(func, range):
    """Filters rows or columns by applying `func` to the given range.
    `func` is called for each value in the range. If it returns False,
    the corresponding row / column is hidden. Otherwise, the row / column is
    made visible.

    `range` must be a row or column vector. If it is a row vector, columns are hidden, and vice versa.
    
    Note that, to unhide rows / columns, `range` must include hidden cells. For example, to unhide a range:
       xl.filter(lambda v: True, some_vector.including_hidden)"""
    # $$$ maybe we should kill scalar ranges
    if not (range.shape in (Scalar, RowVector, ColumnVector)):
        raise ValueError("range must be a vector or scalar")

    hide_dim = range._vector_dim.other
    with CacheManager.caching_disabled():
        for cell in range.itercells():
            assert cell.shape is Scalar
            visible = bool( func(cell.get()) )
            hide_dim.entire(cell._full_xlRange).Hidden = not visible


def map(func, *rangeIn):
    """Excel equivalent to the built-in map().

    ColumnVector ranges as well as Python iterables are accepted.
    The result list is written back to Excel as a column. A ColumnVector
    representing the stored results is returned"""

    import __builtin__    
    xs = (_to_value(r) for r in rangeIn) 
    name = getattr(func, '__name__', "<callable>")
    y = __builtin__.map(func, *xs)
    r = _dest_for_source_ranges(rangeIn)
    return view(y, name, to=r)

def apply(func, *rangeIn):
    """Excel equivalent to the built-in apply().

    Ranges as well as Python iterables are accepted. Ranges
    are converted to lists of Python values (with Range.get()).
    
    The value returned by `func` is then passed to xl.view"""
    import __builtin__
    xs = (_to_value(r) for r in rangeIn)    
    name = getattr(func, '__name__', "<callable>")
    y = __builtin__.apply(func, xs)
    r = _dest_for_source_ranges(rangeIn)
    return view(y, name, to=r)

# accept excel range or value. 
# if it's a range, convert to a value
def _to_value(obj):
    r = _tryToRange(obj)
    if r is not None:
        return r.get()
    if isSequenceType(obj):
        return obj
    raise ValueError("Expected range or value")

# Convert a variety or ranges to a Range object. Good for normalizing inputs
def _tryToRange(obj):
    if obj is None:
        raise ValueError("range object can't be None")
    if hasattr(obj, "xlRange"): # assume xl.Range instance
        return obj
    t = type(obj)    
    # $$$ is it an xlRange?
    if t is str:
        return get(obj)
    return None

def _toRange(obj):
    r = _tryToRange(obj)
    if r is None:
        raise ValueError("Unrecognized range object:%s" % str(obj))
    return r


def _dest_for_source_ranges(ranges):
    """Given a set of source ranges/values (for map or apply), attempts to find a sensible target range
    If a source is found that is both a range and part of a table, returns a new column range in that table
    If no such range exists, None is returned"""
    rs = [r for r in ranges if not r is None
                            if isinstance(r, Range)
                            if not r.containing_table is None]
    if rs:
        r = rs[0]
        # $$$ do something about partial column selections...
        dest_col = r.containing_table.append_empty_columns(1)
        # map / apply respect range filtering when fetching values
        # We inserted a full column, but we must return a range with indices that align visually
        dest_col = dest_col.with_filter(include_hidden_cells=r.includes_hidden_cells)
        return dest_col
    else: return None

def join(key_range_a, key_range_b):
    """Joins the table associated with key range B to the table for key range A.
    Each key in range A must have zero or one matching keys in range B (i.e. rows will not be added to table A)"""
    b_headers, b_key_map = _join_map(key_range_b)
    assert not b_headers is None, "Headerless tables not supported yet"

    # Number of columns being added to table A
    num_joined_cols = len(b_headers)
    if num_joined_cols == 0:
        raise ValueError("key_range_b indicates the source table; there must be at least one value column in addition to the key column")
    
    new_rows = [ b_headers ]
    for a_key in key_range_a:
        v = b_key_map.get(a_key, ("",) * num_joined_cols)
        assert len(v) == num_joined_cols
        new_rows.append(v)

    ws_a = Worksheet(key_range_a._full_xlRange.Worksheet)
    tb_a = ws_a._find_table_containing_range(key_range_a)
    # We may have appended a single column
    joined_cols = tb_a.append_empty_columns(num_joined_cols)
    # If num_joined_cols is 1, may behave as a vector or scalar. However,
    # new_rows is constructed for a 2D range
    joined_cols.as_matrix.set( new_rows )

def _join_map(r):
    ws = Worksheet(r._full_xlRange.Worksheet)
    tb = ws._find_table_containing_range(r)

    key_col_idx = r.column - tb.rData.column
    headers = None
    if tb.rHeader:
        assert not tb.rHeader.shape is Scalar
        headers = list(tb.rHeader.get())
        del headers[key_col_idx]
    
    m = {}
    for r in tb.data_rows:
        assert not r[key_col_idx] in m, "Duplicate key during join"
        m[r[key_col_idx]] = r[:key_col_idx] + r[key_col_idx + 1:]

    return (headers, m)

# Decorators
# Used to provide metadata to Tooling about exposed function types

def tool_map(func):
    return func

def tool_apply(func):
    return func

def tool_workbook(func):
    return func