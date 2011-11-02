 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################/

from xl.cache import cache_result, enable_caching
import xl._impl.com_utils as com_utils
import xl._impl.table as table
from xl.range import Range, ExcelRangeError, _xlRange_from_corners, _xlRange_parse

# Worksheet
class Worksheet(object):
    def __init__(self, xlSheet):
        self.xlWorksheet = xlSheet
        pass

    # return 1-based index of empty column (column without content that we can write to)
    def _findOpenColumn(self):
        xlRange = self.xlWorksheet.UsedRange
        # excel has a bug where if the sheet is completely empty, it still returns 1 column
        # Check explicitly
        if (xlRange.Count == 1):
            if (self.xlWorksheet.Cells(1,1).Value == None):
                return 1

        # Else, pick the next column after the used range
        cs = [c.Column for c in xlRange.Columns]
        x = max(cs) # edge of used area
        return x+1 # +1, move past edge

    def __str__(self):
        return self.name

    @property
    def name(self):
        return self.xlWorksheet.Name

    @cache_result
    @enable_caching
    def _getTableColumn(self, name):
        """Search through this worksheets for the given table column
        Return a Range if found, else None."""
        for t in self.tables:
            rData = t._getTableColumn(name)
            if rData != None:
                return rData
        return None

    @cache_result
    @property
    @enable_caching
    def tables(self):
        """Returns a list of all table-like things on the sheet"""
        l = []
        t = table.tableFromAutoFilter(self.xlWorksheet)
        if t != None:
            l.append(t)
        los = self.xlWorksheet.ListObjects    
        for lo in los:
            t = table.tableFromListObject(lo)
            l.append(t)
        return l


    def _find_table_containing_range(self, range):     
        """Search all Tables on the sheet for any that contain the given range.
        Return None if not found.""" 
        for t in self.tables:
            if (t.rData.intersects(range)):
                return t
        return None


_default_workbook = None

# Top level object for Excel
class Workbook(object):
    
    # Workbook() - creates a new excel instance
    # Workbook(filename) - attaches to existing excel instance, throws error if file not open
    def __init__(self, *args):
        import win32com.client as win32
        com_utils.ensure_excel_dispatch_support()
        if (len(args) == 0):
            # Create a new empty instance
            excel = win32.gencache.EnsureDispatch('Excel.Application')
            excel.Visible = True
            self.xlWorkbook = excel.Workbooks.Add()
            assert not self.xlWorkbook is None
        elif (len(args) == 1):
            if isinstance(args[0], basestring):
                filename = args[0]
                self.xlWorkbook = com_utils.get_running_xlWorkbook_for_filename(filename)
                if (self.xlWorkbook == None):
                    self.xlWorkbook = com_utils.open_xlWorkbook(filename)
            else:
                assert hasattr(args[0], "CLSID"), "Expected workbook name or xlWorkbook"
                self.xlWorkbook = args[0]
        # $$$ fix this behavior
        self.set_default_workbook(self)

    @classmethod
    def default_workbook(cls):
        global _default_workbook
        if _default_workbook == None:
            cls.set_default_workbook( Workbook() )
        return _default_workbook

    @classmethod
    def set_default_workbook(cls, workbook):
        if (workbook == None):
            raise ValueError("Can't set active workbook instance to None")
        global _default_workbook
        _default_workbook = workbook

    @cache_result
    @property
    def active_sheet(self):
        return Worksheet(self.xlWorkbook.ActiveSheet)

    @cache_result
    @property
    def worksheets(self):
        return [Worksheet(xlSheet) for xlSheet in self.xlWorkbook.Worksheets]

    def view(self, obj, name=None, to=None):
        """Writes a Python iterable to an available location in the workbook, with an optional header (name).
        The optional `to` argument specifies a location hint. 
        
        If None, the values are written to an empty column on the active sheet.
        If `to` is a Range, the values are written to it (like Range.set, but with the header prepended)
        If `to` is a Table, the values are written to a new column in the table."""

        # Python version of splatting to cells.
        if to is None:
            ws = self.active_sheet
            # $$$ is this where with_hidden should come from?
            c = Range(ws.xlWorksheet.Columns(ws._findOpenColumn()), with_hidden=False)
        elif isinstance(to, table.Table):
            c = to.append_empty_columns(num_new_cols=1)
        elif isinstance(to, Range):
            c = to
        else:
            raise ValueError("'to' argument must be a Range, Table, or None")
        
        # write a header, this will will cooperate with autofilters.
        if (name == None):
            name = "values"
        
        if isinstance(obj, basestring):
            obj = [ obj ]

        obj = list(obj)
        vals = [ name ] + obj
        c.set(vals)

        data_only = c._adjust_unfiltered_size(rows=-1)._offset_unfiltered(rows=1)
        return data_only
        
    def __str__(self):
        return self.name

    def __repr__(self):
        return 'Workbook(%s)' % repr(self.name)

    @property
    def name(self):
        return self.xlWorkbook.Name

    @cache_result
    @enable_caching
    def get(self, object):
        """Returns a Range for the requested table column, named Excel range, or Excel address (ex. A1:B20)

        The returned Range has been normalized (see Range.normalize()); if possible, it is clipped to an overlapping table's data area,
        as well as the worksheet's `used range`."""
        # First look for table names. 
        if type(object) is str:
            r = self._getTableColumn(object)
            if r != None:
                return r
        # Now look for excel ranges.
        # Since this is the "smart" function, we normalize the result,
        # i.e. A:A snaps to table data within column A
        try:
            r = self.range(object)
        except ExcelRangeError:
            msg = "failed to find range or table column: %s. " + \
                  "Note that table columns must be part of an AutoFilter or Table (Ctrl+T) in Excel in order to be found."
            msg = msg % str(object)
            raise ExcelRangeError(msg)
        # normalize() may fail with an exception if the r doesn't intersect the used range
        return r.normalize()

    @cache_result
    @enable_caching
    def range(self, object):
        """Returns a Range for the requested named Excel range or Excel address.

        The returned range is not normalized, e.g. range("A:A") returns a Range containing ~1mil rows,
        rather than clipping to the 'used range' / table areas. See also `get`"""
        # Named ranges are workbook wide, but we don't have a workbook lookup function. So explicitly 
        # check for them now.
        r = self._get_named_range(object)
        if r != None:
            return r
        # $$$ Is there a better way (avoid needing the sheet), especially for sheet qualified ranges?
        xlSheet = self.xlWorkbook.ActiveSheet
        # _xlRange_parse throws an ExcelRangeError if it fails
        xlRange = _xlRange_parse(xlSheet, object)
        # Un-normalized range is returned; if the user specifies A2:D10, they probably meant it
        # $$$ what should with_hidden be here
        return Range(xlRange, with_hidden=False)

    # Get a Range by Name, or none
    def _get_named_range(self, name):
        name = name.lower()
        for n in self.xlWorkbook.Names:
            if n.Name.lower() == name:
                r = n.RefersToRange
                # excel allows Names that are bound directly to Values and not ranges on the spreadsheet
                if r == None:
                    raise NotImplementedError("Name " + name + " is not backed by a range")
                # $$$ what should with_hidden be here
                return Range(r, with_hidden=False)
        return None

    @property
    def named_ranges(self):
        return [n.Name for n in self.xlWorkbook.Names]            

    def _getTableColumn(self, name):
        """Search through all worksheets for the given column
        Return a Range if found, else None."""
        active = self.active_sheet
        r = active._getTableColumn(name)
        if r != None: return r
        for s in self.worksheets:
            r = s._getTableColumn(name)
            if r != None:
                return r
        return None

