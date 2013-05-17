# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

import xl._impl.com_utils as com_utils
from xl.cache import CacheManager, cache_result, enable_caching
from xl.range import Range

# Table abstraction. Provides a uniform abstraction over Excel concepts:
#  - Excel ListObject (Ctrl+T), 1st class tables
#  - Excel AutoFilters, but aren't 1st class objects. 
# 
# Services provided:
# - header (column names) 
# - data ranges.
# - visiblity and rows
# - better support for adding new computed columns
class Table(object):
    def __init__(self, name, rHeader, rData, from_auto_filter=False):
        self.rHeader = rHeader # may be null
        self.rData = rData
        self._name = name
        self._from_auto_filter = from_auto_filter

        if (rHeader != None):
            assert not rHeader.intersects(rData)

    @cache_result
    @enable_caching
    def _getTableColumn(self, name):
        """Returns a Range for the data in the given column name.
        None if no column."""
        if self.rHeader == None:
            return None
        name = name.lower()

        for idx, header in enumerate(self.rHeader):
            if header is None: continue # Header cells can be empty
            if header.lower() == name:
                return self.rData.column_vector(idx)

    # get total number of rows in the table. 
    def getRowCount(self):
        return self.rData.num_rows

    def getVisibleRowCount(self):
        return self.rData.getVisibleRowCount()

    @property
    def data_rows(self):
        """Returns s list of data rows in the table. Each row is a list of values"""
        # A small rData may have a vector or scalar shape. However, we wish to
        # always return a list of lists
        return self.rData.as_matrix.get()

    @cache_result
    @property
    def table_range(self):
        """The full Range of this table; encompasses headers (if any) as well as data"""
        assert not self.rData is None
        app = self.rData._full_xlRange.Application
        if self.rHeader is None: return self.rData
        return Range(app.Union(self.rData._full_xlRange, self.rHeader._full_xlRange), with_hidden=False)

    def Name(self):
        return self._name

    def append_empty_columns(self, num_new_cols):
        """Appends the specified number of columns to the right of this table. The columns are empty,
        except for the possibility of Excel-generated default column headers. The inserted range,
        including headers, is returned"""

        # We assume below that at least one column is added
        # $$$ Decide how to represent empty Ranges()
        if num_new_cols == 0: return None

        adjacent = self._adjacent_column_range(num_new_cols)
        self._reserve_column_space(adjacent)
        # The insert has helpfully updated xlRanges from underneath us. That is, adjacent has shifted by num_new_cols
        adjacent = self._adjacent_column_range(num_new_cols)

        # AutoFilter tables are hard to extend, but easy to promote to a 'real' table
        if self._from_auto_filter: self._convert_to_listobject_table()

        # For ListObject tables, putting a value in a column header triggers table-ification magic
        # Removing the value generates a default column name. Neat.
        # This accomplishes nothing if this is an AutoFilter table
        # $$$ update this when slicing is added
        adj_header_range = Range(adjacent._full_xlRange.Rows(1), with_hidden=True)
        adj_header_range.set( [u" "] * num_new_cols )
        adj_header_range.set( [u""] * num_new_cols )

        # adjacent is now a subset of the inserted empty space
        # However, this instance's rData and rHeader attributes are now out of date
        # We have been possibly using hidden cells above, but want to return a safer range to users
        # $$$ investigate if updating rData / rHeader is vital
        return adjacent.excluding_hidden

    def _adjacent_column_range(self, num_cols):
        """Returns a num_cols-wide range right-adjacent to this table. The range shares the same height, incl.
        the header row if applicable. This does not modify the worksheet. The returned range includes hidden cells."""
        # $$$ update this when slicing is added
        # We remove filtering here, because we should insert after any hidden cols
        full_table = self.table_range.including_hidden
        last_existing_col = Range(full_table._full_xlRange.Columns(full_table.num_columns), with_hidden=True)
        # first_new_col_xlRange = last_existing_col_xlRange._offset_unfiltered(0, 1)
        first_new_col = last_existing_col._offset_unfiltered(cols=1)
        # Add additional columns beyond the first
        new_cols = first_new_col._adjust_unfiltered_size(cols=num_cols - 1)
        return new_cols

    def _reserve_column_space(self, range):
        """Reserve at least the requested range for new Table columns. The given range
        is assumed to be adjacent (on the right) of this Table. If unable to insert the given range,
        (e.g. because it would break a table further to the right), full (worksheet) columns are inserted instead."""
        CacheManager.invalidate_all_caches()   
        # xlFormatFromLeftOrAbove encourages consistent formatting with the original table (to the left)
        try:
            range._full_xlRange.Insert(CopyOrigin=com_utils.constants.xlFormatFromLeftOrAbove, Shift=com_utils.constants.xlToRight)
        except com_utils.com_error:
            # Oops, insert failed. This is probably because Excel is refusing to break a right-adjacent table
            # We try again, inserting a whole column. This also breaks things in many cases, but at Excel doesn't complain
            range._full_xlRange.EntireColumn.Insert(CopyOrigin=com_utils.constants.xlFormatFromLeftOrAbove, Shift=com_utils.constants.xlToRight)

    def _convert_to_listobject_table(self):
        """Converts this Table's underlying Excel representation to an Excel ListObject
        This operation can only be applied to Tables backed by a sheet AutoFilter (see tableFromAutoFilter)
        
        AutoFilter state is preserved - i.e., visible rows will not change."""
        assert self._from_auto_filter, "already a ListObject table"
        xlWorksheet = self.rData._full_xlRange.Worksheet
        xlWorksheet.ListObjects.Add(SourceType=com_utils.constants.xlSrcRange, Source=self.table_range._full_xlRange)
        self._from_auto_filter = False


def tableFromListObject(xlListObject):
    """Given an ListObject, return a Table abstraction"""
    # See more about ListObjects: http://msdn.microsoft.com/en-us/library/microsoft.office.interop.excel.listobject_members.aspx
    rHeader = Range(xlListObject.HeaderRowRange, with_hidden=False)
    rData = Range(xlListObject.DataBodyRange, with_hidden=False)
    return Table(xlListObject.Name, rHeader, rData, from_auto_filter=False)


def tableFromAutoFilter(xlSheet):
    """Each excel sheet can have 1 auto-filter. Return it if present. Else return None."""
    a = xlSheet.AutoFilter
    if a == None:
        return None # no autofilter on this sheet
    
    # We have to manually split out the header and range.
    r = a.Range
    # In certain peculiar cases, Worksheet.AutoFilter is set, but
    # actually refers to a ListObject table. See excel_issues.py
    if r.ListObject != None: return None
    (r1,c1,r2,c2) = _getBounds(r)
            
    rHeader = Range(xlSheet.Range(xlSheet.Cells(r1, c1), xlSheet.Cells(r1, c2)), with_hidden=False)
    rData = Range(xlSheet.Range(xlSheet.Cells(r1+1, c1), xlSheet.Cells(r2, c2)), with_hidden=False)
                       
    return Table("AutoFilter " + xlSheet.Name, rHeader, rData, from_auto_filter=True)

# Given an xlRange, get the (1-based) row, column bounds for the range. 
def _getBounds(xlRange):
    x = xlRange.Columns
    c1 = x(1).Column
    c2 = x(len(x)).Column
    
    y =xlRange.Rows
    r1 = y(1).Row
    r2 = y(len(y)).Row

    return (r1, c1, r2, c2)