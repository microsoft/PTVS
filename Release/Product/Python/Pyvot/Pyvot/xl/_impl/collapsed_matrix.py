# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

class cached_property(object):
    def __init__(self, f):
        self._wrapped = f
        self._attr = f.__name__ + '__computed'
    def __get__(self, obj, cls):
        if obj is None: return self
        try:
            return getattr(obj, self._attr)
        except AttributeError:
            v = self._wrapped(obj)
            setattr(obj, self._attr, v)
            return v

def _gen_indices_from_pairs(pairs):
    m = None
    for start, end in pairs:
        for i in xrange(start, end + 1): 
            if i > m: yield i ; m = i

def _count_indices_in_pairs(pairs):
    count = 0
    current_index = None
    for start, stop in pairs:
        assert stop >= start
        if start > current_index: current_index = start
        contained = stop - current_index + 1
        # Though sorted, the previous pair may contain this one. ex: (11, 20), (12, 15)
        if contained > 0: count += contained
        # don't re-count any indices up to and including stop
        current_index = stop + 1
    return count

class CollapsedMatrix(object):
    """Represents a 2D array of Python values where rows and columns may be 'collapsed.' A collapsed row or
    column contains no values; it is erroneous to attempt to index into one. The underlying data is also available
    with the collapsed rows / columns absent altogether, as a list of lists (`collapsed_data` property). Since CollapsedMatrix
    presents both of these (read/write) views of the data, it serves as an adapter between their indexing schemes.

        1 1 1 # 2       1 1 1 2
        1 1 1 # 2   --> 1 1 1 2
        # # # # #       3 3 3 4
        3 3 3 # 4

    The above example shows the relationship between a CollapsedMatrix (left) and its `collapsed_data` property (right).
    Collapsed cells are noted with a hash mark.
        
        matrix[3, 4]  -> 4
        matrix.collapsed_data[2][3] -> 4
        matrix[0, 3]  -> Error; column is collapsed
        matrix[0:2, 0:3]  -> [[1,1,1], [1,1,1]]
        matrix[0:2, 0:]  -> Error; intersects a collapsed column

    Note that indexing is not the same between the two representations. The matrix has logically present but inaccessible values;
    those values do not exist at all in the `collapsed_data` form."""

    def __init__(self, row_range_pairs, column_range_pairs):
        """Returns a CollapsedMatrix with uncollapsed indices taken from the given dimension range pairs.
        A range pair is a tuple such as (0, 10) (meaning indices 0 through 10, inclusive).

        E.g., row_range_pairs=[(0,2), (1,3), (5,5)] specifies row indices [0, 1, 2, 3, 5], and similarly
        for column_range_pairs."""

        # Pairs are stored sorted to prevent a bigger sort on generated indices,
        # and to make counting indices easier (two million element range-pairs -> two subtractions)
        self._row_pairs = sorted(list(row_range_pairs))
        self._column_pairs = sorted(list(column_range_pairs))

        # row_indices, _row_map, etc. can now be calculated lazily

        self._num_rows = _count_indices_in_pairs(self._row_pairs)
        self._num_columns = _count_indices_in_pairs(self._column_pairs)
        self._data = None # Initialized by collapsed_data property on first access

    @property
    def num_rows(self): return self._num_rows

    @property
    def num_columns(self): return self._num_columns

    @cached_property
    def row_indices(self):
        return tuple(_gen_indices_from_pairs(self._row_pairs))

    @cached_property
    def column_indices(self):
        return tuple(_gen_indices_from_pairs(self._column_pairs))

    @cached_property
    def _row_map(self):
        return self._dimension_map(self.row_indices)

    @cached_property
    def _column_map(self):
        return self._dimension_map(self.column_indices)

    def _dimension_map(self, sorted_indices):
        """Returns a list such that, for any i in [0..max(sorted_indices)]:
            list[idx] == None if i isn't in sorted_indices
            list[idx] == the index of i in sorted_indices, if this applies
        This describes a mapping from present -> collapsed indices"""
        dim_max_index = sorted_indices[-1] if sorted_indices else -1
        dim_map = [None] * (dim_max_index + 1) # [0, 1, 4] means we need dim_map to have indices 0..4
        for collapsed_idx, present_idx in enumerate(sorted_indices):
            dim_map[present_idx] = collapsed_idx
        return dim_map

    def _allocate_empty_data_array(self):
        # For any r in row_indices, c in column_indices, we should have exactly one storage location for (r, c)
        self._data = [[None] * self._num_columns for idx in xrange(self._num_rows)]

    @property
    def collapsed_data(self):
        """Gets or sets the underlying data as a list of rows, each row being a list itself.
        The returned list-of-lists allows access to the data _as if it were contiguous_ (collapsed vectors removed).
        See class description"""
        if self._data is None:
            self._allocate_empty_data_array()
        return self._data

    @collapsed_data.setter
    def collapsed_data(self, val):
        if not len(val) == self.num_rows:
            raise ValueError("Number of rows given must be equal to the number of present rows")
        for row in val:
            if not len(row) == self.num_columns: raise ValueError("All rows given must have a length equal to the number of present columns")

        self._data = val

    def _iterslice(self, row_slice, col_slice):
        if not (isinstance(row_slice, slice) and isinstance(col_slice, slice)):
            raise TypeError("If slicing, must slice both dimensions")
        if not (row_slice.step is None and col_slice.step is None):
            raise TypeError("Striding not supported")

        col_map_sliced = self._column_map[col_slice]
        row_map_sliced = self._row_map[row_slice]

        if None in col_map_sliced:
            raise IndexError("Slice area overlaps a collapsed column", col)
        if None in row_map_sliced:
            raise IndexError("Slice area overlaps a collapsed row", row)
        
        # Since col_map_sliced is sorted and doesn't contain None, we can use its first and last
        # elements to specify a contiguous range of collapsed column indices to include.
        # A column slice such as 1:1 makes col_map_sliced empty. In this case, we should produce
        # empty rows in the loop below
        col_collapsed_first = col_map_sliced[0] if col_map_sliced else 0
        col_collapsed_last = col_map_sliced[-1] if col_map_sliced else 0
        col_collapsed_slice = slice(col_collapsed_first, col_collapsed_last + 1)

        data = self.collapsed_data
        for row_collapsed in row_map_sliced:
            r = data[row_collapsed]
            yield (r, col_collapsed_slice)

    def _get_area(self, row_slice, col_slice):
        return [ row[s] for row, s in self._iterslice(row_slice, col_slice) ]

    def _set_area(self, row_slice, col_slice, val):
        from itertools import izip
        row_count = 0
        for (row, s), row_val in izip(self._iterslice(row_slice, col_slice), val):
            row[s] = row_val
            row_count += 1
        if not row_count == len(val):
            raise ValueError("Wrong number of rows given to slice assignment")

    def _index_single(self, row, col):
        row_collapsed = self._row_map[row]
        col_collapsed = self._column_map[col]

        if row_collapsed is None or col_collapsed is None:
            raise IndexError("Attempting to index into a collapsed part of the matrix", (row, col))
        return row_collapsed, col_collapsed
    
    def _get_single(self, row, col):
        row_collapsed, col_collapsed = self._index_single(row, col)
        return self.collapsed_data[row_collapsed][col_collapsed]

    def _set_single(self, row, col, val):
        row_collapsed, col_collapsed = self._index_single(row, col)
        self.collapsed_data[row_collapsed][col_collapsed] = val

    def __getitem__(self, obj):
        if not (isinstance(obj, tuple) and len(obj) == 2):
            raise TypeError("Must specify a row and column index")

        row_obj, col_obj = obj
        if isinstance(row_obj, slice) or isinstance(col_obj, slice):
            return self._get_area(row_obj, col_obj)
        else:
            return self._get_single(row_obj, col_obj)

    def __setitem__(self, obj, val):
        if not (isinstance(obj, tuple) and len(obj) == 2):
            raise TypeError("Must specify a row and column index")

        row_obj, col_obj = obj
        if isinstance(row_obj, slice) or isinstance(col_obj, slice):
            self._set_area(row_obj, col_obj, val)
        else:
            self._set_single(row_obj, col_obj, val)
