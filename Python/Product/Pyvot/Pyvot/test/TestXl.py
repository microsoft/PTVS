# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.


"""Test file for xl.py

Test structure:
- Each test class (deriving from ExcelWorkbookTestCase) gets a fresh excel instance
- Each test class specifies (with workbook_path) the workbook to load
  Use get_workbook_path to expand a filename in the test directory
- Test functions within a class share an excel instance, but are not ordered;
  be wary of test functions that mutate the workbook
"""

import unittest
import datetime
import xl
from xl._impl.collapsed_matrix import CollapsedMatrix
import xl.cache as cache

import sys
running_python3 = sys.version_info.major > 2

def ensure_excel_is_not_running():
    import pythoncom
    import pywintypes
    try:
        if pythoncom.GetActiveObject("Excel.Application") != None:
            raise Exception("Close all instances of Excel  before starting the tests.")
    except pywintypes.com_error: pass # Desired case

def wait_for_excel_exit():
    """Wait for Excel to exit by polling the running object table. 
    This may be useful following Application.Quit
    
    Excel will not exit until all COM clients are released. This function
    encourages garbage collections, to release stale refs held by pythoncom"""
    import pythoncom
    import pywintypes
    import time
    import gc
    global _time_waiting_for_excel_exit
    wait_start = datetime.datetime.now()
    for i in xrange(5):
        try:
            # We are interested only in failure, not the return value
            # The return value - if captured in the future - must not
            # live beyond the gc.collect() call below
            pythoncom.GetActiveObject("Excel.Application")
            # Excel is running
            gc.collect() # Release interface pointers with no python refs before trying again
            time.sleep(1)
        except pywintypes.com_error:
            return
    raise Exception("Excel failed to terminate")

def get_workbook_path(basename):
    import os
    # __file__ refers to the location of this executing file. Since this file is executed rather
    # than imported, we assume that it is a relative path.
    wb_path = os.path.join(os.path.dirname(__file__), basename)
    wb_path = os.path.realpath(wb_path) # $$$ Workbook doesn't like relative paths
    assert os.path.exists(wb_path), "Requested workbook (%s) must be in the same folder as the test script (%s)" % (basename, __file__)
    return wb_path

class ExcelWorkbookTestCase(unittest.TestCase):
    workbook_path = None
    total_time_running_excel_tests = datetime.timedelta(0)
    enable_caching_during_tests = True

    @classmethod
    def setUpClass(cls):
        if cls.workbook_path is None:
            cls.workbook = xl.Workbook()
        else:
            cls.workbook = xl.Workbook(cls.workbook_path)

    def run(self, *args, **kwargs):
        if ExcelWorkbookTestCase.enable_caching_during_tests:
            cache_context = xl.CacheManager.caching_enabled
        else:
            cache_context = xl.CacheManager.caching_disabled

        excel_test_start = datetime.datetime.now()
        with cache_context():
            super(ExcelWorkbookTestCase, self).run(*args, **kwargs)
        excel_test_stop = datetime.datetime.now()
        ExcelWorkbookTestCase.total_time_running_excel_tests += (excel_test_stop - excel_test_start)

    @classmethod
    def tearDownClass(cls):
        # Close excel instance that we opened.
        cls.workbook.xlWorkbook.Saved = True # prevents save prompt
        cls.workbook.xlWorkbook.Application.Quit()
        wait_for_excel_exit()

def use_worksheet(ws_name):
    """Decorator to wrap ExcelWorkbookTestCase tests. Before running the test body,
    the active worksheet is changed to that with the name specified."""
    import functools
    import pywintypes
    def _wrap_set_worksheet(f):

        @functools.wraps(f)
        def _wrapped(self, *args, **kwargs):
            xlwb = self.workbook.xlWorkbook
            try:
                xlSheet = xlwb.Sheets(ws_name)
                xlSheet.Activate()
            except pywintypes.com_error as e:
                raise ValueError("Failed to change active worksheet in @use_worksheet", ws_name, e)
            f(self, *args, **kwargs)

        return _wrapped
    return _wrap_set_worksheet

_timed_test_results = {}
def timed(f):
    import datetime
    import functools

    @functools.wraps(f)
    def _wrapped(self, *args, **kwargs):
        start = datetime.datetime.now()

        f(self, *args, **kwargs)

        # We intentionally don't make it here in the event f fails
        end = datetime.datetime.now()
        elapsed_seconds = (end - start).total_seconds()
        assert not f.__name__ in _timed_test_results
        _timed_test_results[f.__name__] = elapsed_seconds
    return _wrapped

def _print_timed_test_results():
    cols = "{0:<30}{1:>30}"
    headers = ("Timed test", "Elapsed time (s)")
    print cols.format(*headers)
    print cols.format(*["-" * len(h) for h in headers])
    for fname, time in _timed_test_results.iteritems():  print cols.format(fname, time)

def _print_cache_info():
    cache_info = xl.CacheManager.cache_info()
    # cache_info = [[str(v) for v in row] for row in cache_info]
    if not cache_info: return # max doesn't like empty lists

    cols = "{0:<80}{1:>10}{2:>10}{3:>10}{4:>30}"
    headers = ("Cache Site Group", "Group Size", "Hits", "Misses", "Misses (caching disabled)")
    print cols.format(*headers)
    print cols.format(*["-" * len(h) for h in headers])

    for site_group, group_size, hits, misses, uncached_misses in cache_info:  print cols.format(site_group, group_size, hits, misses, uncached_misses) 

def _print_time_running_excel_tests():
    print "Time running Excel-based tests (excl. startup / shutdown time):\t", ExcelWorkbookTestCase.total_time_running_excel_tests.total_seconds()

class WorkbookWithTablesTestCase(ExcelWorkbookTestCase):
    # Contains Test Tables + Pivots
    workbook_path = get_workbook_path("TestBook.xlsx")

    def test_get_named_range(self):
        """Test getting a named excel range. Cell references are relative to ActiveSheet, but named ranges are anywhere in workbook"""
        self.assertEqual(self.workbook.get("MyNamedRanged").get(), [u'a', u'b', u'c'])

    # (Filter vs Table vs NoFilter) x (range name vs. Column name) x (visible vs. visibility)

    @use_worksheet("AutoFilter")
    def test_filter_lookup(self):
        self.assertEqual(self.workbook.active_sheet.name, "AutoFilter") # cell references are resolved in context of active sheet
        self.assertEqual(self.workbook.get("A:A").get(), [1,2,4]) # doesn't include non-visible rows
        self.assertEqual(self.workbook.get("A:A").including_hidden.get(), [1,2,3,4]) # even shows visible rows
        self.assertEqual(self.workbook.get("Alpha").get(), [1,2,4])
        self.assertEqual(self.workbook.get("Alpha").including_hidden.get(), [1,2,3,4]) # even shows visible rows
        self.assertEqual(self.workbook.get("ALPHA").get(), [1,2,4]) # case-insensitive
        self.assertEqual(self.workbook.get("A1:A6").get(), [1,2,4]) # clip to data-range within Table

    @use_worksheet("AutoFilter")
    def test_table_lookup(self):
        self.assertEqual(xl.get("TAlpha").get(), [1,3,4]) 
        self.assertEqual(xl.get("TAlpha").including_hidden.get(), [1, 2, 3, 4]) 
        self.assertEqual(xl.get("TBeta").get(), [5,7,8]) 
        self.assertEqual(xl.get("TALPHA_NAME_RANGE").get(), [1,3,4]) # Named range for entire TAlpha column

    @use_worksheet("AutoFilter")
    def test_range_normalization(self):
        # 'smart' get should always call normalize(), which trims to the used range _and_ table column
        self.assertEqual(self.workbook.get("B:B").including_hidden.num_rows, 4) # 4 data points in B, though 1 is hidden
        self.assertEqual(self.workbook.get("B:B").excluding_hidden.num_rows, 3)
        self.assertEqual(self.workbook.get("B:B").get(), [5, 6, 8])
        # range does not call normalize; it can result in large row counts...
        self.assertTrue(self.workbook.range("B:B").num_rows > 10 ** 6)
        # ...but we should get 4 points (header + 3 points above), rather than to the row limit,
        # and extra Nones to row 10 (bounds of UsedRange; didn't snap to table contents)
        self.assertEqual(self.workbook.range("B:B").get(), [u'Beta', 5, 6, 8] + [None] * 5)

    @use_worksheet("AutoFilter")
    def test_missing_range(self):
        """Tests that informative exceptions are thrown when fetching missing / invalid ranges"""
        self.assertRaisesRegexp(xl.ExcelRangeError, "failed to find range or table column: NoSuchRange",
                                lambda: self.workbook.get("NoSuchRange"))
        self.assertRaisesRegexp(xl.ExcelRangeError, "failed to find range: Z100:xxx",
                                lambda: self.workbook.range("Z100:xxx"))
        self.assertRaisesRegexp(xl.ExcelRangeError, "Range is completely outside the used ranged",
                                lambda: self.workbook.get("Z100:Z200"))

    @use_worksheet("ViewToTable")
    def test_view_to_table(self):
        """Tests that the `view` function extends a table, if specified, rather than the first open column.
        The top level map function should deduce destination table from source ranges"""

        tables = list(self.workbook.active_sheet.tables)
        self.assertEqual(len(tables), 1)
        self.workbook.view(['a', 'b', 'c'], to=tables[0])
        # Range here specifies the first added column to the table. _not_ first empty column (A)
        self.assertEqual(self.workbook.get("G9:G11").get(), ['a', 'b', 'c'])

        c1, c2 = self.workbook.get("view_to_table_1"), self.workbook.get("view_to_table_2")
        xl.map(lambda x, y: x + y, c1, c2)
        # Range here specifies the second added column to the table. _not_ first empty column (A)
        self.assertEqual(self.workbook.get("H9:H11").get(), [11.0, 13.0, 15.0])

    @use_worksheet("MapVisibility")
    def test_map_visibility(self):
        """Tests that the 'map' function respects a range's filtering. Given a visibility-filtered range,
        the map function should not be applied to hidden data, etc. The mapped column should align 1:1 with source ranges"""
        c1, c2 = self.workbook.get("mv_x"), self.workbook.get("mv_y")
        
        # Only map visible
        xl.map(lambda x, y: x + y, c1, c2)
        self.assertEqual(self.workbook.get("E3:E8").get(), [22.0, 44.0, 66.0])
        self.assertEqual(self.workbook.get("E3:E8").including_hidden.get(), [None, 22.0, None, 44.0, None, 66.0])

        # Map all
        xl.map(lambda x, y: x + y, c1.including_hidden, c2.including_hidden)
        self.assertEqual(self.workbook.get("F3:F8").including_hidden.get(), [11.0, 22.0, 33.0, 44.0, 55.0, 66.0])


    @use_worksheet("Visibility")
    def test_range_visibility(self):
        r = self.workbook.range("B3:C5") # don't snap to column
        self.assertEqual(r.num_rows, 2)
        self.assertEqual(r.num_columns, 1)
        self.assertEqual(r.including_hidden.num_rows, 3)
        self.assertEqual(r.including_hidden.num_columns, 2)

        r = self.workbook.get("E3:F4")
        self.assertEqual(r.get(), [[u'good1', u'good2']])
        self.assertEqual(r.including_hidden.get(), [[u'good1', u'good2'], [u'bad1', u'bad2']])

        # Hidden DisjointCol2 means that the visible table range is disjoint
        r = self.workbook.get("DisjointCol1")
        self.assertEqual(r.get(), [1.0, 2.0])
        r = self.workbook.get("DisjointCol3")
        self.assertEqual(r.get(), [3.0, 4.0])
        # 2D get on a disjoint area (hidden center col)
        table_range = self.workbook.get("DisjointCol1").containing_table.table_range
        m = table_range.get()
        self.assertEqual(m, [["DisjointCol1", "DisjointCol3"], [1.0, 3.0], [2.0, 4.0]])


    @use_worksheet("RangeGetSet")
    def test_range_get_set(self):
        matrix = self.workbook.get("a1:c2")
        scalar = self.workbook.get("e1:e1")
        col_vector = self.workbook.get("g1:g4")
        row_vector = self.workbook.get("i1:l1")

        def _getset_test(range, dims, pos, val):
            for r in (range, range.including_hidden):
                self.assertEqual(r.num_rows, dims[0])
                self.assertEqual(r.num_columns, dims[1])
                self.assertEqual(r.row, pos[0])
                self.assertEqual(r.column, pos[1])
                self.assertEqual(r.get(), val)
                old_val = r.get()
                r.set(r.get())
                self.assertEqual(old_val, r.get())

        _getset_test(matrix, (2,3), (1,1), [[u'a1', u'b1', u'c1'], [u'a2', u'b2', u'c2']])
        _getset_test(matrix.as_matrix, (2,3), (1,1), [[u'a1', u'b1', u'c1'], [u'a2', u'b2', u'c2']])
        _getset_test(scalar, (1,1), (1,5), u'scalar')
        _getset_test(scalar.as_matrix, (1,1), (1,5), [[u'scalar']])
        _getset_test(col_vector, (4,1), (1,7), [1.0, 2.0, 3.0, 4.0])
        _getset_test(col_vector.as_matrix, (4,1), (1,7), [[1.0], [2.0], [3.0], [4.0]])
        _getset_test(row_vector, (1,4), (1,9), [1.0, 2.0, 3.0, 4.0])
        _getset_test(row_vector.as_matrix, (1,4), (1,9), [[1.0, 2.0, 3.0, 4.0]])

    @use_worksheet("CellTypes")
    def test_cell_types_roundtrip(self):
        """Tests that we can roundtrip cells formatted as currency / dates / text / etc."""
        import decimal
        import datetime
        import pywintypes

        dates = self.workbook.get("Dates")
        currency = self.workbook.get("Currency")
        text = self.workbook.get("Text")
        percent = self.workbook.get("Percent")
        general = self.workbook.get("General")

        def _celltype_test(range, py_types):
            self.assertEqual([type(v) for v in range.get()], py_types)
            original = range.get()
            range.set( range.get() )
            self.assertEqual( range.get(), original )
        
        _celltype_test(dates, [datetime.datetime] * 2)
        _celltype_test(currency, [decimal.Decimal] * 2)
        _celltype_test(text, [unicode] * 2)
        _celltype_test(percent, [float] * 2)
        _celltype_test(general, [float, unicode])

    @use_worksheet("CellTypes")
    def test_cell_types_compute(self):
        """Tests that simple computations on percent and date formatted cells in Python / Excel give
        consistent results"""
        import datetime

        date_orig = self.workbook.get("Date_Original").get()
        date_excel = self.workbook.get("Date_Add_Excel").get()
        date_py_range = self.workbook.get("Date_Add_Python")

        date_py = date_orig + datetime.timedelta(days=1, minutes=30, seconds=10)
        self.assertEqual(date_excel, date_py)
        date_py_range.set( date_py )
        self.assertEqual(date_excel, date_py_range.get())

        percent_orig = self.workbook.get("Percent_2").get()
        percent_excel = self.workbook.get("Percent_2_Mult_Excel").get()
        percent_py_range = self.workbook.get("Percent_2_Mult_Python")

        percent_py = percent_orig * 40.0
        self.assertEqual(percent_excel, percent_py)
        percent_py_range.set( percent_py )
        self.assertEqual(percent_excel, percent_py_range.get())


    @use_worksheet("Filter")
    def test_filter(self):
        """Tests that xl.filter can be applied to a row / column vector, hiding or restoring columns / rows as requested"""
        def _f(v):
            if v == "XXX": self.fail("User-hidden value given to filter function")
            return bool(v)
        xl.filter(_f, self.workbook.get("Filter_Flag"))
        self.assertEqual(self.workbook.get("Filter_Tag").get(), ["keep"] * 3)

        # Should be able to unhide everything, including the user hidden value,
        # so long as we elect to consider hidden things
        xl.filter(lambda v: True, self.workbook.get("Filter_Flag").including_hidden)
        self.assertEqual(self.workbook.get("Filter_Tag").get(), ['keep', 'hidden', 'trash', 'keep', 'keep', 'trash'])

    @use_worksheet("AppendEmptyColumns")
    def test_append_empty_columns(self):
        """Tests that empty columns can be appended to a table or AutoFilter, 
        and that (in supported layouts) doing so doesn't break adjacent tables"""

        # Autofilter case
        aec_autofilter = self.workbook.get("AEC_Autofilter")
        # Used to verify that rows aren't unhidden after append
        aec_autofilter_table = aec_autofilter.containing_table
        aec_autofilter_original_rowcount = aec_autofilter_table.table_range.num_rows
        aec_autofilter_appended = aec_autofilter_table.append_empty_columns(2)
        # Range / table may have changed
        aec_autofilter = self.workbook.get("AEC_Autofilter")
        aec_autofilter_table = aec_autofilter.containing_table
        # Rows should not have become unhidden
        self.assertEqual(aec_autofilter_table.table_range.num_rows, aec_autofilter_original_rowcount)

        self.assertEqual(aec_autofilter_appended.column, 2)
        self.assertEqual(aec_autofilter_appended.row, 1)
        self.assertEqual(aec_autofilter_appended.num_columns, 2)
        self.assertEqual(aec_autofilter_appended.num_rows, aec_autofilter_table.table_range.num_rows)
        # Columns should have become part of the table
        self.assertEqual(aec_autofilter_table.table_range.num_columns, 3)
        # Table to the right should exist in the expected (shifted) column
        self.assertEqual(self.workbook.get("AEC_Top1_1").column, 6)
        self.assertEqual(self.workbook.get("AEC_Top1_1").get(), [1.0,2.0,3.0])

        # ListObject case
        # Here we check that adding a column to AEC_Top1
        # - fails to insert minimal space (would break AEC_Right1)
        # - falls back to inserting an entire sheet column (which unfortunately alters AEC_Bottom1)
        aec_top = self.workbook.get("AEC_Top1_1").containing_table
        aec_top_appended = aec_top.append_empty_columns(1)
        # Check returned range
        self.assertEqual(aec_top_appended.num_columns, 1)
        self.assertEqual(aec_top_appended.num_rows, 3 + 1) # don't forget header
        # Column should have become part of the table
        aec_top = self.workbook.get("AEC_Top1_1").containing_table
        self.assertEqual(aec_top.table_range.num_columns, 3)
        # AEC_Right1 should be intact
        self.assertEqual(self.workbook.get("AEC_Right1").get(), [float(x) for x in range(1,8+1)])
        # Should have (as a last resort) also added a column to AEC_Bottom1
        aec_bottom = self.workbook.get("AEC_Bottom1_1").containing_table
        self.assertEqual(aec_bottom.table_range.num_columns, 4)

        # Now, try the same thing with AEC_Top2. It should succeed in inserting minimal space, not affecting AEC_Bottom2
        aec_top = self.workbook.get("AEC_Top2_1").containing_table
        aec_top_appended = aec_top.append_empty_columns(1)
        # Check returned range
        self.assertEqual(aec_top_appended.num_columns, 1)
        self.assertEqual(aec_top_appended.num_rows, 3 + 1) # don't forget header
        # Column should have become part of the table
        aec_top = self.workbook.get("AEC_Top2_1").containing_table
        self.assertEqual(aec_top.table_range.num_columns, 3)
        # Shouldn't have added a column to AEC_Bottom2
        aec_bottom = self.workbook.get("AEC_Bottom2_1").containing_table
        self.assertEqual(aec_bottom.table_range.num_columns, 3)

        # Filtered ListObject case
        # Check that adding a column to a filtered table does not unhide rows
        aec_filtered = self.workbook.get("AEC_Filtered")
        aec_filtered_original_rowcount = aec_filtered.num_rows
        aec_filtered_table = aec_filtered.containing_table
        aec_filtered_appended = aec_filtered_table.append_empty_columns(1)
        aec_filtered = self.workbook.get("AEC_Filtered")
        self.assertEqual(aec_filtered.num_rows, aec_filtered_original_rowcount)

    def test_uncached_range_indexing_exception(self):
        with xl.CacheManager.caching_disabled():
            try:
                r = self.workbook.range("A:A")
                self.assertEqual(r.shape, xl.ColumnVector)
                r[123]
            except Exception as e:
                self.assertTrue("severe performance implications" in e.args[0])
                return
        self.fail("expected exception due to uncached indexing")

class EmptyWorkbookTestCase(ExcelWorkbookTestCase):
    workbook_path = None # ExcelWorkbookTestCase will open an empty workbook

    def test_workbook_name(self):
        """Tests that the correct workbook name is returned for workbook string conversion,
        and the explicit Name() method"""
        self.assertEqual('Book1', str(self.workbook))
        self.assertEqual('Book1', self.workbook.name)

    def test_write_and_read_view(self):
        """Tests that a python list can be inserted as a column (with xl.view), and then read
        back. A header is expected"""
        x = range(1,10)
        r = xl.view(x)
        self.assertEqual(x, r.get())

        v = self.workbook.get("A2").get()
        self.assertEqual(1, v)

        for i in range(len(x)):
            val = r[i] # range index operator
            self.assertEqual(val, x[i])

        self.assertEqual(r[-1], x[-1]) # negative indices!
        self.assertEqual(r[2:5], x[2:5]) # slice indexing

        for i, val in enumerate(r):  # iterating over range
            self.assertEqual(val, x[i])

        # Should be able to get the new workbook by name
        wb2 = xl.Workbook('Book1')
        self.assertEqual(1, wb2.range("A2").get())

        # Basic map pattern
        def MyDouble(x):
            return x * 2
        r2 = xl.map(MyDouble, r)
        self.assertEqual(map(MyDouble, x), r2.get())

    def test_range_subclass_identity(self):
        """Tests that new Range instances adopt subclasses based on their shapes"""
        def _check_range_shape(r, expected_subclass):
            self.assertEqual(r.shape, type(r))
            self.assertEqual(r.shape, expected_subclass)
            self.assertTrue(r.shape is expected_subclass)
            self.assertTrue(isinstance(r, xl.Range))
        _check_range_shape(self.workbook.range("A:A"), xl.ColumnVector)
        _check_range_shape(self.workbook.range("A1:B1"), xl.RowVector)
        _check_range_shape(self.workbook.range("Z20:Z20"), xl.Scalar)
        _check_range_shape(self.workbook.range("B10:C11"), xl.Matrix)

class WorkbookWithJoinsTestCase(ExcelWorkbookTestCase):
    workbook_path = get_workbook_path("TestJoin.xlsx")

    @use_worksheet("SingleColumn")
    def test_join_single_column(self):
        """Tests that a single column can be joined to an existing table"""
        zip_key_a = self.workbook.get("B:B")
        zip_key_b = self.workbook.get("D1:D50")
        zip_cities = self.workbook.get("E1:E50")  
        
        self.assertEqual(zip_cities.get(), [u'Redmond', u'Austin', None])
        self.assertEqual(zip_key_a.get(), [78705.0, 98052.0, 0.0])
        self.assertEqual(zip_key_b.get(), [98052.0, 78705.0, None])

        xl.join(zip_key_a, zip_key_b)
        # We fetch these ranges here, because they aren't durable under insertion
        # (the join would have shifted them to the right)
        # We use range so that we don't snap to the table data
        joined_cities_header = self.workbook.range("C1:C1")
        self.assertEqual(joined_cities_header.get(), "CityName")
        # Should snap to joined column data
        joined_cities_data = self.workbook.get("C:C")
        self.assertEqual(joined_cities_data.get(), [u'Austin', u'Redmond', None])

    @use_worksheet("MultiColumn")
    def test_join_multi_column(self):
        """Tests that multiple columns can be joined to an existing table"""
        key_a = self.workbook.get("D3:D5")
        key_b = self.workbook.get("H3:H5")

        xl.join(key_a, key_b)
        # We fetch these ranges here, because they aren't durable under insertion
        # (the join would have shifted them to the right)
        # We use range so that we don't snap to the table data

        # First, check that both columns (excl. key) from table b made it to table a, and table a headers are intact
        a_cols_headers = self.workbook.range("C2:G2")
        self.assertEqual(a_cols_headers.get(), [u'a_1', u'key', u'a_2', u'b_1', u'b_2'])
        
        # Data in the joined columns
        a_joined_data = self.workbook.get("F3:G5")
        # $$$ get() is inconsistent, as of writing, about using tuples or lists. normalize
        a_joined_data_lists = [list(r) for r in a_joined_data.get()]
        self.assertEqual(a_joined_data_lists, [[None, None], [u'b_1_1', u'b_2_1'], [u'b_1_4', u'b_2_4']])

    @use_worksheet("Tiny")
    def test_join_tiny(self):
        """Tests that join properly handles table data ranges that look like vectors / scalars"""
        key_a = self.workbook.get("tiny_k_2")
        key_b = self.workbook.get("tiny_k_1")
        xl.join(key_a, key_b)

        a_joined = self.workbook.range("E1:E2")
        self.assertEqual(a_joined.get(), [u"tiny_v_1", 1.0])

class PublicAPITestCase(unittest.TestCase):
    def test_import_all(self):
        """Tests that 'from xl import *' doesn't clobber map/apply/filter, but instead provides xlmap, etc."""
        
        globals = {} ; locals = {}
        exec "from xl import *" in globals, locals
        all_vars = globals ; all_vars.update(locals)

        import __builtin__
        self.assertTrue(all_vars['xlmap'] is xl.xlmap)
        self.assertTrue(all_vars['xlapply'] is xl.xlapply)
        self.assertTrue(all_vars['xlfilter'] is xl.xlfilter)

        self.assertFalse('map' in all_vars)
        self.assertFalse('apply' in all_vars)
        self.assertFalse('filter' in all_vars)

class PerformanceTestCase(ExcelWorkbookTestCase):
    workbook_path = get_workbook_path("TestPerf.xlsx")

    @use_worksheet("HalfSparseColumn")
    @timed
    def test_get_half_sparse_column(self):
        col = self.workbook.get("A:A").get()
        self.assertEqual(len(col), 4000)

    @use_worksheet("DenseMatrix")
    @timed
    def test_get_dense_matrix(self):
        m = self.workbook.get("A1:Z4000").get()
        self.assertEqual(len(m), 4000) # len(m) is #rows

    @use_worksheet("BigJoin")
    @timed
    def test_big_join(self):
        source_key = self.workbook.get("bigjoin_key_1")
        dest_key = self.workbook.get("bigjoin_key_2")
        xl.join(dest_key, source_key)
        self.assertEqual( self.workbook.get("G2:H2").get(), [u"key_3222", u"val_3222"] )

class CollapsedMatrixTestCase(unittest.TestCase):
    def _CollapsedMatrix_from_index_sequence(self, row_indices, column_indices):
        # CollapsedMatrix always takes range pairs; we want to deal in single-element pairs most of the time
        return CollapsedMatrix( ((r,r) for r in row_indices), ((c,c) for c in column_indices) )
    
    def _assert_can_roundtrip_all_indices(self, matrix):
        for r in matrix.row_indices:
            for c in matrix.column_indices:
                matrix[r, c] = (r, c)
        for r in matrix.row_indices:
            for c in matrix.column_indices:
                self.assertEqual(matrix[r, c], (r, c))

    def _assert_can_roundtrip_collapsed_data(self, matrix):
        expected = []
        for r in matrix.row_indices:
            row = []
            expected.append(row)
            for c in matrix.column_indices:
                row.append( (r, c) )
                matrix[r, c] = (r, c)
        self.assertEqual(matrix.collapsed_data, expected)
        matrix.collapsed_data = expected
        self.assertEqual(matrix.collapsed_data, expected)

    def _assert_can_roundtrip(self, matrix):
        self._assert_can_roundtrip_all_indices(matrix)
        self._assert_can_roundtrip_collapsed_data(matrix)

    def _assert_dimension_sizes_consistent(self, matrix):
        self.assertEqual(matrix.num_rows, len(matrix.row_indices))
        self.assertEqual(matrix.num_columns, len(matrix.column_indices))

    def test_collapsed_first_index(self):
        c = self._CollapsedMatrix_from_index_sequence(row_indices=[1,2], column_indices=[1,2])
        self.assertRaises(IndexError, lambda: c[0,0])
        self.assertRaises(IndexError, lambda: c[3,3])
        self.assertRaises(IndexError, lambda: c[0,2])
        self._assert_can_roundtrip(c)

    def test_scalar(self):
        c = self._CollapsedMatrix_from_index_sequence(row_indices=[0], column_indices=[0])
        self._assert_can_roundtrip(c)
        self._assert_dimension_sizes_consistent(c)

    def test_create_from_range_pairs(self):
        c = CollapsedMatrix([(0,2), (1,3), (5,5)], [(1,2)])
        self.assertEqual(c.row_indices, (0, 1, 2, 3, 5))
        self.assertEqual(c.column_indices, (1, 2))
        self._assert_can_roundtrip(c)
        self._assert_dimension_sizes_consistent(c)


        # Note that the given pairs overlap - some are fully contained in others
        c = CollapsedMatrix([(1,4), (2,3), (0,2)], [(0,0), (0, 1)])
        self.assertEqual(c.row_indices, (0, 1, 2, 3, 4))
        self.assertEqual(c.column_indices, (0, 1))
        self._assert_can_roundtrip(c)
        self._assert_dimension_sizes_consistent(c)

    def test_lazy_allocation_of_data(self):
        """Tests that CollapsedMatrix can be used for calculating large sizes, without allocating the corresponding data / mapping arrays"""
        c = CollapsedMatrix([(0,0), (1, 10**6 - 1)], [(1,1)])
        self.assertEqual(c.num_rows, 10 ** 6)
        self.assertEqual(c.num_columns, 1)

        # _attr is defined by collapsed_matrix.cached_property
        self.assertFalse(hasattr(c, CollapsedMatrix._column_map._attr))
        self.assertFalse(hasattr(c, CollapsedMatrix._row_map._attr))
        self.assertEqual(c._data, None)
    
    def test_empty(self):
        c = CollapsedMatrix([], [])
        self._assert_can_roundtrip(c)
        self._assert_dimension_sizes_consistent(c)

    def test_slice_area(self):
        matrix = self._CollapsedMatrix_from_index_sequence(row_indices=[1,2,3,5], column_indices=[0,1,2,6,7])
        for r in matrix.row_indices:
            for c in matrix.column_indices:
                matrix[r, c] = (r, c)

        def _test_slice_roundtrip(row_slice, col_slice, expected):
            sliced = matrix[row_slice, col_slice]
            self.assertEqual(sliced, expected)
            matrix[row_slice, col_slice] = sliced
            self.assertEqual(sliced, matrix[row_slice, col_slice])

        _test_slice_roundtrip(slice(1,2), slice(1,2), [[(1,1)]])
        _test_slice_roundtrip(slice(1,0), slice(1,0), [])
        _test_slice_roundtrip(slice(1,3+1), slice(0,2+1), [[(1,0), (1,1), (1,2)], [(2,0), (2,1), (2,2)], [(3,0), (3,1), (3,2)]])

class CacheDecoratorsTestCase(unittest.TestCase):

    def test_call_with_caching_disabled(self):
        outside_state = [ 0 ]
        
        class C(object):
            @xl.cache_result
            def _f(self, arg1, arg2):
                outside_state[0] += 1
                return outside_state[0]
        c = C()
        self.assertEqual(c._f(1, 2), 1)
        self.assertEqual(c._f(3, 4), 2)
        self.assertEqual(c._f(1, 2), 3)

    def test_function_caches_isolated(self):
        subtract = 0
        class C(object):
            @xl.cache_result
            def _add(self, arg1, arg2):
                return arg1 + arg2 - subtract
            @xl.cache_result
            def _mul(self, arg1, arg2):
                return arg1 * arg2 - subtract
        c = C()
        
        with xl.CacheManager.caching_enabled():
            self.assertEqual(c._add(2,3), 5)
            self.assertEqual(c._mul(2,3), 6)
            # Now, we change the (would be) result with outside state.
            # We should get the cached results instead
            sub = 10
            self.assertEqual(c._add(2,3), 5)
            self.assertEqual(c._mul(2,3), 6)

    def test_instance_level_caches_isolated(self):
        class Lol(object):
            @xl.cache_result
            def get_x(self): return self.x

        l1, l2 = Lol(), Lol()
        l1.x = 1 ; l2.x = 2
        with xl.CacheManager.caching_enabled():
            self.assertEqual(l1.get_x(), 1)
            self.assertEqual(l2.get_x(), 2)
            # Now, poison the outside state ; should get cached results
            l1.x = l2.x = None
            self.assertEqual(l1.get_x(), 1)
            self.assertEqual(l2.get_x(), 2)

    def test_cache_keying_by_arguments(self):
        miss_counter = [ 0 ]
        
        class C(object):
            @xl.cache_result
            def _f(self, arg1, arg2, kwarg=None):
                miss_counter[0] += 1
                return miss_counter[0]
        c = C()

        # When caching is activated, shouldn't remember anything from when not activated
        self.assertEqual(c._f(1, 2), 1)
        with xl.CacheManager.caching_enabled():
            self.assertEqual(c._f(1, 2), 2)
            self.assertEqual(c._f(1, 2), 2)
            self.assertEqual(c._f(3, 4), 3)
            # Explicitly giving default value makes a different key; could be optimized
            self.assertEqual(c._f(1, 2, kwarg=None), 4)
            self.assertEqual(c._f(1, 2, kwarg=None), 4)
            # By equality rather than identity
            self.assertEqual(c._f("fob", 0), 5)
            self.assertEqual(c._f("fo" + "b", 0), 5)

    def test_cache_property(self):
        """Tests that @cache_property gives property-like behavior"""
        miss_counter = [ 0 ]
        
        class C(object):
            @xl.cache_result
            @property
            def _f(self):
                miss_counter[0] += 1
                return miss_counter[0]
        c = C()

        self.assertEqual(c._f, 1)
        with xl.CacheManager.caching_enabled():
            self.assertEqual(c._f, 2)
            self.assertEqual(c._f, 2)

    def test_nested_cache_activation(self):
        outside_state = True

        class C(object):
            @xl.cache_result
            def _f(self, ignored=None): return outside_state
        c = C()

        with xl.CacheManager.caching_enabled():
            self.assertEqual(c._f(), True)
            outside_state = False

            self.assertEqual(c._f(), True)
            self.assertEqual(c._f("new_args"), False)
            with xl.CacheManager.caching_enabled():
                self.assertEqual(c._f(), True)
            self.assertEqual(c._f(), True)

        self.assertEqual(c._f(), False)

    def test_cache_context_isolation(self):
        """Test that all caches are cleared when the cache-activation context is exited"""
        class Lol(object):
            @xl.cache_result
            def get_x(self): return self.x

        lol = Lol()
        lol.x = 1
        with xl.CacheManager.caching_enabled():
            self.assertEqual(lol.get_x(), 1)
            lol.x = 2
            self.assertEqual(lol.get_x(), 1)
        # Cleared
        with xl.CacheManager.caching_enabled():
            self.assertEqual(lol.get_x(), 2)
            lol.x = 3
            self.assertEqual(lol.get_x(), 2)

    def test_cache_introspection(self):
        class Adder(object):
            @xl.cache_result
            def add_one(self, arg):
                """DS"""
                return arg + 1

        a = Adder()
        self.assertEqual(a.add_one(5), 6)
        self.assertTrue(isinstance(a.add_one, xl.cache.CacheSite))

        # Descriptor and CacheSite each provide __name__, __doc__, etc.
        self.assertEqual(a.add_one.__name__, "add_one")
        self.assertEqual(a.add_one.__doc__, "DS")
        self.assertEqual(a.add_one.__name__, Adder.add_one.__name__)
        self.assertEqual(a.add_one.__doc__, Adder.add_one.__doc__)
        self.assertEqual(xl.cache._ResultCachingDescriptor.__name__, "_ResultCachingDescriptor")

        self.assertEqual(a.add_one.stats.hits, 0)
        self.assertEqual(a.add_one.stats.misses, 0)
        self.assertEqual(a.add_one.stats.uncached_misses, 1)
        self.assertTrue(a.add_one.site_name.endswith("(instance of %s at %x)" % (str(type(a)), id(a))))

        with xl.CacheManager.caching_enabled():
            self.assertEqual(a.add_one(10), 11)
            self.assertEqual( (a.add_one.stats.hits, a.add_one.stats.misses), (0, 1) )
            for i in xrange(10): self.assertEqual(a.add_one(10), 11)
            self.assertEqual( (a.add_one.stats.hits, a.add_one.stats.misses), (10, 1) )

            b = Adder()
            self.assertEqual(b.add_one(10), 11)
            self.assertEqual( (b.add_one.stats.hits, b.add_one.stats.misses), (0, 1) )

        
        # We now verify that cache_info() aggregates instance-level counters ;
        # further, we check that cache_info() returns the correct result event after
        # instances have been garbage collected (cache_info() shouldn't be using weakrefs to the instances)
        expected_stat = ((Adder.add_one._wrapped, Adder), 2, 10 + 0, 1 + 1, 1 + 0)
        self.assertTrue( expected_stat in xl.CacheManager.cache_info() )
        import gc
        del a ; del b
        gc.collect()
        self.assertTrue( expected_stat in xl.CacheManager.cache_info(), "stat disappeared after a gc" )

    def test_enable_caching_decorator(self):
        @xl.enable_caching
        def _f():
            return xl.CacheManager.is_caching_enabled

        self.assertTrue(_f())

    def test_exception_on_direct_call(self):
        @xl.cache_result
        def _f(): return 5

        self.assertRaises(TypeError, lambda: _f())

if __name__ == '__main__':
    ensure_excel_is_not_running()

    # $$$ quick option parsing hack to still use unittest.main
    # replace with optparse in the future
    enable_cache = True
    import sys
    if '--uncached' in sys.argv:
        sys.argv.remove('--uncached')
        enable_cache = False
    ExcelWorkbookTestCase.enable_caching_during_tests = enable_cache

    try:
        unittest.main(verbosity=2)
    except SystemExit:
        # We wish to print the collected test timings after tests are run, but unittest.main()
        # always calls sys.exit (if targetting 2.7, we could instead pass exit=False)
        print
        _print_timed_test_results()
        print
        _print_cache_info()
        print
        print "Cache status during Excel tests:\t", "Enabled" if enable_cache else "Disabled"
        _print_time_running_excel_tests()
        print
        raise