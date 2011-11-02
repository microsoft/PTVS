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

import xl._impl.com_utils as com_utils
from xl.cache import CacheManager, cache_result, enable_caching
from xl._impl.collapsed_matrix import CollapsedMatrix

class ExcelRangeError(ValueError):
    """Raised when
    - a requested range / named range / table column is invalid or cannot be found
    - usage of a Range instance fails due to its dimensions, e.g. Range.set() with too many values"""
    pass

class Range(object):
    """Represents a contiguous range of cells in Excel, ex. A1:B20. The contents (in Excel) can be read and written to (see the `get`
    and `set` methods).
    
    Ranges are usually obtained using the :meth:`xl.sheet.Workbook.get` or :meth:`xl.sheet.Workbook.range` method. The returned range behaves
    according to its 'shape,' which is reflected in its type as well as the `shape` property::

        >>> type(workbook.get("A:A"))
        <class 'xl.range.ColumnVector'>

    A range's shape may be `ColumnVector`, `RowVector`, `Scalar`, or `Matrix`. The `Vector` type (a base for RowVector
    and ColumnVector) allows detection of either vector variety::

        >>> isinstance(workbook.get("A:A"), xl.Vector)
        True

    The shape subclasses differ in the rules and types involved in accessing the backing Excel data, e.g. the return
    type of `get`. See help(shape class) for specifics.

    By default, a Range excludes 'hidden' cells - those not visible in Excel due to an Excel-level filter,
    or manual hiding by the user. The `including_hidden` and `excluding_hidden` properties permit explicit
    control of this behavior::

        >>> workbook.get("A1:A3")
        <ColumnVector range object for $A$1,$A$3 (visible only)>
        >>> workbook.get("A1:A3").including_hidden
        <ColumnVector range object for $A$1:$A$3 (includes hidden)>
    
    Note that un-filtered dimensions determine shape, e.g. ``workbook.get("A1:B2")`` is a Matrix, even if column B is hidden"""
    
     

    def __init__(self, xlRange, with_hidden, as_matrix=False):
        """Internal usage only. See `xl.Workbook.get` and xl.Workbook.range`"""
        assert xlRange.Areas.Count == 1, "can't wrap a non-contiguous xlRange"
        # We expect the indexing behavior of .Cells, rather than something from .Rows() or .Columns()
        # (from which xlRange may originate)
        self._full_xlRange = xlRange.Cells
        self._with_hidden = with_hidden
        self._as_matrix = as_matrix

        self._init_shape_class(as_matrix=as_matrix)
        
    def _init_shape_class(self, as_matrix):
        """Updates __class__ based on the unfiltered (contiguous) dimensions. After this call,
        the instance is a subclass of Range, such as Matrix."""
        r, c = self._unfiltered_dimensions

        if as_matrix:
            self.__class__ = Matrix
        elif ((r == 1) + (c == 1)) == 1:
            if r == 1:
                self.__class__ = RowVector
            else:
                self.__class__ = ColumnVector
        elif r == 1 and c == 1:
            self.__class__ = Scalar
        elif r > 1 and c > 1:
            self.__class__ = Matrix
        else:
            assert False
        assert isinstance(self, Range)

    @property
    def shape(self):
        """Returns the shape class of this range (e.g. RowVector). Equivalent to type(self)"""
        assert not type(self) is Range
        return type(self)

    @cache_result
    @property
    def as_matrix(self):
        """Returns a version of this range that is always a Matrix (even if shaped differently).
        This is useful for utility functions that wish to avoid a special case per range shape"""
        if self._as_matrix: return self
        return Range(self._full_xlRange, with_hidden=self._with_hidden, as_matrix=True)

    @cache_result
    @property
    def _effective_xlRange(self):
        """The xlRange from which we should draw data; filtered cells are removed, if applicable"""
        return self._apply_filter_to( self._full_xlRange )

    def _apply_filter_to(self, xlRange):
        """Given an xlRange, returns a subset which passes the configured filters.
        A filter may remove entire rows and columns, but NOT arbitrary cells"""

        if self.includes_hidden_cells:
            return xlRange
        else:
            assert not self._with_hidden
            # Filtering single cell ranges with SpecialCells likes to fail. See excel_issues.py
            if xlRange.Cells.Count == 1:
                assert not (xlRange.EntireRow.Hidden or xlRange.EntireColumn.Hidden)
                return xlRange
            return xlRange.SpecialCells(com_utils.constants.xlCellTypeVisible)
    
    @cache_result
    def with_filter(self, include_hidden_cells):
        """Returns a range with the specified inclusion / exclusion of hidden cells.
        See the including_hidden / excluding_hidden properties for less verbose shortcuts"""
        if self.includes_hidden_cells == include_hidden_cells: return self
        return Range(self._full_xlRange, with_hidden=include_hidden_cells, as_matrix=self._as_matrix)

    @property
    def includes_hidden_cells(self):
        return self._with_hidden

    @property
    def excluding_hidden(self):
        """Returns a new Range identical this one, but with hidden cells filtered away.
        This is reversible, e.g. range.exluding_hidden.including_hidden"""
        return self.with_filter(include_hidden_cells=False)
    @property
    def including_hidden(self):
        """Returns a new Range identical this one, but with hidden cells included.
        This is reversible, e.g. range.including_hidden.excluding_hidden"""
        return self.with_filter(include_hidden_cells=True)

    def _with_xlRange(self, xlRange):
        """Returns a Range with the same filtering / shape override, but the specified xlRange"""
        if _xlRanges_equivalent(xlRange, self._full_xlRange):
            return self
        else:
            # $$$ would be nice to cache _with_xlRange for the same xlRange param,
            # but xlRanges are not hashable.
            return Range(xlRange, with_hidden=self._with_hidden, as_matrix=self._as_matrix)

    def __repr__(self):
        try:
            addr = str(self._effective_xlRange.Address)
        except com_utils.com_error:
            addr = "<error getting address from excel>"
        hidden_descr = "visible only" if not self._with_hidden else "includes hidden"
        # $$$ Sheet / workbook?
        return "<%s range object for %s (%s)>" % (type(self).__name__, addr, hidden_descr)

    @cache_result
    @property
    def _trimmed(self):
        trimmedXlRange = _trim_xlRange(self._full_xlRange)
        return self._with_xlRange(trimmedXlRange)

    @cache_result
    @enable_caching
    def _get_2D(self):
        # get() on an un-trimmed range can be accidentally costly.
        # We always trim before fetching values
        # Note that this affects the number of data items returned, but not shape
        cd = self._trimmed._get_collapsed_matrix(populate_values=True).collapsed_data
        return [[com_utils.unmarshal_from_excel_value(c) for c in row] for row in cd]


    @enable_caching
    def _set_2d(self, data):
        """Sets the range's cells using the 2D (list-of-lists) `data` parameter. `data` may be smaller
        than the range"""
        num_rows, num_cols = self.dimensions

        data = list(data)
        num_data_rows = len(data)
        num_data_cols = len(data[0]) if data else 0

        if num_data_rows > num_rows:
            raise ExcelRangeError("Range has %d rows; too many (%d) given" % (num_rows, num_data_rows))
        if num_data_cols > num_cols:
            raise ExcelRangeError("Range has %d columns; too many (%d) given" % (num_cols, num_data_cols))

        if num_data_rows < num_rows or num_data_cols < num_cols:
            # row_limit and column_limit causes matrix to be (at most) a row_limit x column_limit subset of this range,
            # expanding out from the top left
            matrix = self._get_collapsed_matrix(populate_values=False, row_limit=num_data_rows, column_limit=num_data_cols)
        else:
            matrix = self._get_collapsed_matrix(populate_values=False)

        marshalled_data = []
        for row in data:
            if len(row) != num_data_cols:
                raise ExcelRangeError("All rows must be the same size")
            marshalled_data.append( [com_utils.marshal_to_excel_value(v) for v in row] )

        matrix.collapsed_data = marshalled_data
        self._set_from_collapsed_matrix(matrix)

    def _set_from_collapsed_matrix(self, matrix):
        """Sets this range's cell values to reflect the contents of `matrix.` The matrix is sliced
        with the range's sheet-wide indices (not relative to the range). In other words,
        if this range contains sheet cell (r, c), and matrix[r, c] is a valid index,
        the cell (r, c) is set to matrix[r, c]"""
        for area, bounds in self._area_bounds:
            (row_start, row_stop), (col_start, col_stop) = bounds
            val_2d = matrix[row_start:row_stop + 1, col_start:col_stop + 1]

            # matrix may cover a subset of the range, e.g. setting A:A with [1,2,3]
            # This area of the full range may be completely outside the 
            # subset specified by matrix. GetResize fails if 0 is specified
            val_2d_rows = len(val_2d)
            if not val_2d_rows: continue
            val_2d_cols = len(val_2d[0])
            if not val_2d_cols: continue

            if val_2d_rows < (row_stop - row_start + 1) or val_2d_cols < (col_stop - col_start + 1):
                # We resize the area such that the top-left corner stays the same, but its size equals
                # that of the value array. Assigning to the un-resized area would change all cells, rather
                # than just those with a corresponding element in val_2d
                area = area.GetResize(val_2d_rows, val_2d_cols)
            
            CacheManager.invalidate_all_caches()    
            area.Value = val_2d

    def itercells(self):
        """Returns a generator yielding the single cell ranges comprising this scalar / vector.
        Thus, range.get() == [c.get() for c in range.itercells()]"""
        # $$$ move this to the shape classes
        if self.shape is Matrix: assert False, "2D itercells not supported"
        trimmedXlRange = _trim_xlRange(self._full_xlRange)
        trimmedRange = self._with_xlRange(trimmedXlRange)
        for c in trimmedRange._effective_xlRange: yield self._with_xlRange(c)

    def _dim_vector_index_in_sheet(self, dim, idx):
        """Maps relative (effective) indices on a given dimension to sheetwide indices.

        For example, _dim_vector_index_in_sheet(_RowDimension, 0) returns a value i such
        that xlWorksheet.Rows(i) is the sheet-row containing the first row of the effective range"""
        if dim is _RowDimension:
            m = self._get_collapsed_matrix(populate_values=False, column_limit=1)
            ind = m.row_indices
        elif dim is _ColumnDimension:
            m = self._get_collapsed_matrix(populate_values=False, row_limit=1)
            ind = m.column_indices
        return ind[idx]

    @cache_result
    def _get_dim_vector(self, dim, idx):
        """Returns a Range object for a row or column vector comprising the effective xlRange,
        by zero-based index.
        
        For example, _get_dim_vector(_RowDimension, 0) returns a RowVector for the
        first non-filtered row."""
        ws = self._full_xlRange.Worksheet
        app = ws.Application
        idx_sheet = self._dim_vector_index_in_sheet(dim, idx)
        vec_sheet = dim.of(ws)(idx_sheet)
        # Visibility filtering is handled by _with_xlRange,
        # rather than the intersection
        vec = app.Intersect(vec_sheet, self._full_xlRange)
        return self._with_xlRange(vec)

    def row_vector(self, idx):
        """Returns one of the row-vectors comprising this Range
        by index, i.e. row_vector(0) gives the first row"""
        return self._get_dim_vector(_RowDimension, idx)

    def column_vector(self, idx):
        """Returns one of the column-vectors comprising this Range
        by index, i.e. column_vector(0) gives the first column"""
        return self._get_dim_vector(_ColumnDimension, idx)
    
    @cache_result
    @property
    def _areas(self):
        """Returns a list of contigous sub-areas (rows / cols may be missing from the effective range)"""
        return list(self._effective_xlRange.Areas)

    @cache_result
    @property
    def _area_bounds(self):
        """Returns a tuple (contiguous xlRange, ((first row, last row), (first col, last col))) per contiguous sub-area"""
        return [(a, self._single_area_bounds(a)) for a in self._areas]


    def _single_area_bounds(self, xlArea):
        """Returns ((first row, last row), (first column, last column)) for the given xlArea"""
        r, c = xlArea.Row, xlArea.Column
        return ((r, r + xlArea.Rows.Count - 1), (c, c + xlArea.Columns.Count - 1))

    def _limit_index_pairs(self, pairs, count_limit):
        """Given a list of pairs (start, stop) (indicating a range of indices, inclusive),
        returns a list containing at most count_limit indices. The highest indices are dropped as needed.
            list( self._limit_index_pairs([(10,19), (30, 40)], 15)) ->  [(10, 19), (30, 34)]"""
        # $$$ Test this thoroughly
        count = 0
        current_index = None
        for start, stop in sorted(pairs):
            assert stop >= start
            if start > current_index: current_index = start
            contained = stop - current_index + 1
            if contained > 0: # Though sorted, the previous pair may contain this one. ex: (11, 20), (12, 15)
                if count + contained < count_limit:
                    count += contained
                else:
                    stop = (count_limit - count) + current_index - 1
                    assert count + stop - current_index + 1 == count_limit
                    count = count_limit
            current_index = stop + 1 # don't re-count any indices up to and including stop
            yield start, stop

            if count == count_limit: break

    @cache_result
    @enable_caching
    def _get_collapsed_matrix(self, populate_values=True, row_limit=None, column_limit=None):
        """Constructs a CollapsedMatrix representation of this Range.
        The matrix has indices such that matrix[c.Row, c.Column] represents any cell c in the range, subject
        to the row and column limits. If row / column limits are specified, rows and columns are
        included up to the specified limits starting from the top left; e.g. one can request
        'the first 2 rows' of A3:B10 as a CollapsedMatrix.

        If populate_values is True, the range's cell values are fetched into the matrix. In this case,
        matrix[c.Row, c.Column] == c.Value for any c in the range. populate_values may not be specified
        with row / column limits.

        Note that the returned matrix provides a contiguous view on the range via the `collapsed_data`
        property. See the CollapsedMatrix class description"""
        row_pairs = (b[0] for a, b in self._area_bounds)
        col_pairs = (b[1] for a, b in self._area_bounds)
        if not row_limit is None: row_pairs = self._limit_index_pairs(row_pairs, row_limit)
        if not column_limit is None: col_pairs = self._limit_index_pairs(col_pairs, column_limit)

        m = CollapsedMatrix(row_pairs, col_pairs)
        
        if populate_values:
            assert (row_limit is None and column_limit is None), "size limits only allowed when populate_values=False"
            for area, (row_bounds, col_bounds) in self._area_bounds:
                v = area.Value
                # Excel special-cases scalar ranges, but slice assignment to a CollapsedMatrix requires a list-of-lists
                if row_bounds[0] == row_bounds[1] and col_bounds[0] == col_bounds[1]:
                    v = [[v]]
                m[row_bounds[0]:row_bounds[1] + 1, col_bounds[0]:col_bounds[1] + 1] = v
        return m

    # return true iff this range and other range intersect
    def intersects(self, rangeOther):
        # $$$ should we be using full or effective xlRanges for interesections?
        app = self._full_xlRange.Application
        return app.Intersect(self._full_xlRange, rangeOther._full_xlRange) != None

    @property
    @enable_caching
    def dimensions(self):
        """Gives the tuple (num rows, num columns). If applicable for this range, hidden cells are excluded"""
        # num_rows / num_columns can use the same cached
        # collapsed matrix, if caching is on
        return self.num_rows, self.num_columns

    @property
    def _unfiltered_dimensions(self):
        assert self._full_xlRange.Areas.Count == 1
        return self._full_xlRange.Rows.Count, self._full_xlRange.Columns.Count

    @property
    def num_rows(self):
        return self._get_collapsed_matrix(populate_values=False).num_rows

    @property
    def num_columns(self):
        return self._get_collapsed_matrix(populate_values=False).num_columns

    @cache_result
    def normalize(self):
        """Return a normalized version of this range. The returned range is reduced to
        encompass only in-use areas of the worksheet, and (if applicable) the data area of its table"""
        from xl.sheet import Worksheet # $$$ remove this cyclical reference ; take WS on construction
        normalized = _trim_xlRange(self._full_xlRange)
        # If we're in a table, snap to the data range (excludes headers). 
        s = Worksheet(self._full_xlRange.Worksheet)
        t = s._find_table_containing_range(self)    
        if (t != None):
            normalized = self._full_xlRange.Application.Intersect(normalized, t.rData._full_xlRange)
        return self._with_xlRange(normalized)

    def _with_unfiltered_size(self, rows=0, cols=0):
        assert rows > 0 and cols > 0
        r1, c1 = self._full_xlRange.Row, self._full_xlRange.Column
        # A1:A1 is 1x1
        r2 = r1 + int(rows) - 1
        c2 = c1 + int(cols) - 1

        ws = self._full_xlRange.Worksheet
        return self._with_xlRange(_xlRange_from_corners(ws, r1, c1, r2, c2))

    def _adjust_unfiltered_size(self, rows=0, cols=0):
        return self._with_unfiltered_size(self._full_xlRange.Rows.Count + rows, self._full_xlRange.Columns.Count + cols)
    
    def _offset_unfiltered(self, rows=0, cols=0):
        fr = self._full_xlRange
        size_r, size_c = fr.Rows.Count, fr.Columns.Count
        r1, c1 = fr.Row + rows, fr.Column + cols
        # A1:A1 is 1x1
        r2 = r1 + size_r - 1
        c2 = c1 + size_c - 1

        ws = self._full_xlRange.Worksheet
        return self._with_xlRange(_xlRange_from_corners(ws, r1, c1, r2, c2))

    @property
    def row(self):
        """Returns the sheet-wide row index of the top-most unfiltered cells"""
        return self._get_collapsed_matrix(populate_values=False, row_limit=1, column_limit=1).row_indices[0]

    @property
    def column(self):
        """Returns the sheet-wide column index of the left-most unfiltered cells"""
        return self._get_collapsed_matrix(populate_values=False, row_limit=1, column_limit=1).column_indices[0]

    @cache_result
    @property
    def containing_table(self):
        """If this range is partially or fully contained in a Table, returns the table
        Otherwise, returns None"""
        from xl.sheet import Worksheet # $$$ remove this cyclical reference ; take WS on construction
        ws = Worksheet(self._full_xlRange.Worksheet)
        return ws._find_table_containing_range(self)

    def __setitem__(self, index, value):
        raise NotImplementedError("setting ranges")

    def __len__(self):
        # if get() removes None instances, then get().Count < xlRange.Count
        return self._effective_xlRange.Count

class _Dimension(object): pass

class _RowDimension(_Dimension):
    def position(self, xlRange):
        return xlRange.Row
    def of(self, xlRange):
        return xlRange.Rows
    def entire(self, xlRange):
        return xlRange.EntireRow
    @property
    def other(self): return _ColumnDimension
_RowDimension = _RowDimension()

class _ColumnDimension(_Dimension):
    def position(self, xlRange):
        return xlRange.Column
    def of(self, xlRange):
        return xlRange.Columns
    def entire(self, xlRange):
        return xlRange.EntireColumn
    @property
    def other(self): return _RowDimension
_ColumnDimension = _ColumnDimension()

class Vector(Range):
    @enable_caching
    def set(self, data):
        """Updates this vector's cells. The `data` parameter should be an iterable returning cell values.
        Not all cells need to be specified. Values are filled in from the top-left of the vector, and
        additional cells are left unmodified. For example::

            workbook.get("A:A").set([1,2,3])

        sets the first 3 values of column A"""
        # caching is enabled to allow _set_2d to re-use the result of determining vector dimension
        # (which constructs an empty collapsed matrix)
        if isinstance(data, basestring):
            raise ValueError("Vector-shaped Range requires a list of values (was given a string)")

        is_row_vector = (self.num_rows == 1)
        if is_row_vector: vec_data = [data]
        else: vec_data = [[v] for v in data]

        self._set_2d(vec_data)

    def get(self):
        """Returns a list representation of this vector's values (containing values for low indices first)

        The number of elements returned may be less than num_rows / num_columns; the fetch is clipped to the 'used range' of the worksheet"""
        vals_2d = self._get_2D()
        if len(vals_2d) == 1: return vals_2d[0]
        else: return [v for (v,) in vals_2d] # intentionally breaks on multi-elem row

    def __getitem__(self, index):
        if not CacheManager.is_caching_enabled:
            raise Exception("Repeatedly indexing into a vector has severe performance implications when caching is disabled. " + \
                            "Enable caching while indexing the vector (see CacheManager), or consider using get() instead")
        return self.get()[index]

    def __iter__(self):
        return iter(self.get())

class RowVector(Vector):
    _vector_dim = _RowDimension
class ColumnVector(Vector):
    _vector_dim = _ColumnDimension

class Scalar(Range):
    def set(self, data):
        """Updates this scalar range's cell. The `data` parameter
        may be a single value, or a (non-string) iterable that returns one item."""   
        # Strings are iterable, but we want to keep them in one piece as a special case
        if not isinstance(data, basestring):
            # Non-string iterables are okay as long as they have one element.
            # Non-iterables (list fails) are always okay
            try:
                l = list(data)
                if len(l) != 1: raise ValueError("set() for a scalar Range must be given a scalar value, or an iterable giving one value")
                data = l[0]
            except TypeError: pass
        self._set_2d( [[data]] )

    def get(self):
        """Returns the single value in the scalar range."""
        vals_2d = self._get_2D()
        assert not len(vals_2d) == 0, "Scalar range has no value. Filtered?"
        assert len(vals_2d) == 1 and len(vals_2d[0]) == 1
        return vals_2d[0][0]

    def __getitem__(self, index):
        if not index == 0:
            raise IndexError("Scalar range. Only index 0 is allowed")
        return self.get()

    def __iter__(self):
        """Returns an iterator yielding a single value (that of the single cell)"""
        # get() returns a single value in this case
        return iter( [self.get()] )

class Matrix(Range):
    def set(self, data):
        """Updates the matrix's cells. `data` should be of the form::

            [[row values, ...], [row values,...], ...]
        
        Not all rows and columns need to be specified; the given values fill
        the top-left corner of the matrix, and the remaining cells are unchanged. For example::

            workbook.get("A:C").set([[1,2], [3,4]])

        only modifies A1:B2"""   
        self._set_2d(data)

    def get(self):
        """Returns a list-of-lists representation in the form::

            [[row values,...], [row values,...], ...]

        All row lists have the same length. The number of rows / columns returned may be less than num_rows / num_columns;
        the fetch is clipped to the 'used range' of the worksheet"""
        return self._get_2D()

    def __getitem__(self, index):
        """Not supported for Matrix"""
        raise NotImplementedError("2D indexing is not supported. Call get() instead")

    def __iter__(self):
        """Not supported for Matrix"""
        raise NotImplementedError("2D iteration is not supported. Call get() instead")


def _trim_xlRange(xlRange):
    """Trim an xlRange to be within the range actually used. This prevents, for example, a range such as A:A
    from including empty cells down to the row limit
    
    A new xlRange is returned"""
    xlApp = xlRange.Application

    # If we select an entire column (or row), then excel runs it to infinity. 
    # So intersect with the "UsedRange" to make it finite.
    xlRangeUsed = xlRange.Worksheet.UsedRange
    xlRange = xlApp.Intersect(xlRange, xlRangeUsed)
    if xlRange == None:
        # Selected a range that doesn't contain any data
        # So this would return all 0s anyways. Probably a user error here. 
        # return xlRange(1)
        raise ExcelRangeError("Range is completely outside the used ranged")
    return xlRange


def _xlRange_from_corners(xlWorksheet, r1, c1, r2, c2):
    """Get excel Range for (row1,column1) to (row2, column2), inclusive."""
    return xlWorksheet.Range(xlWorksheet.Cells(r1,c1), xlWorksheet.Cells(r2,c2))

def _xlRange_parse(xlWorksheet, obj):
    """Get range on the worksheet by the given string 'A1', 'A1:D4'"""
    assert isinstance(obj, basestring), "Expected a range string"
    try:
        return xlWorksheet.Range(obj)
    except com_utils.com_error as e:
        # $$$ make sure it's the right kind of com error?
        raise ExcelRangeError("failed to find range: " + str(obj))

def _xlRanges_equivalent(xlRange_a, xlRange_b):
    if xlRange_a.Count != xlRange_b.Count: return False
    inter = xlRange_a.Application.Intersect(xlRange_a, xlRange_b)
    if inter is None: return False
    # Did intersection remove any cells?
    return inter.Count == xlRange_a.Count