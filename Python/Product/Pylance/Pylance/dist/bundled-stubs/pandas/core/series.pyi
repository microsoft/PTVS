import numpy as np
from datetime import time, date
from matplotlib.axes import Axes as PlotAxes, SubplotBase as SubplotBase
import sys
from .base import IndexOpsMixin
from .generic import NDFrame
from .indexing import _iLocIndexer, _LocIndexer
from .frame import DataFrame
from pandas.core.arrays.base import ExtensionArray
from pandas.core.groupby.generic import SeriesGroupBy
from pandas.core.indexes.base import Index
from pandas._typing import AxisType as AxisType, Dtype as Dtype, DtypeNp as DtypeNp, \
    FilePathOrBuffer as FilePathOrBuffer, Level as Level, MaskType as MaskType, S1 as S1, Scalar as Scalar, \
    SeriesAxisType as SeriesAxisType, num as num, Label
from typing import Any, Callable, Dict, Generic, Hashable, Iterable, List, Optional, Sequence, Tuple, Type, Union, overload
if sys.version_info >= (3, 8):
    from typing import Literal
else:
    from typing_extensions import Literal

_bool = bool
_str = str

class _iLocIndexerSeries(_iLocIndexer, Generic[S1]):
    # get item
    @overload
    def __getitem__(self, idx: int) -> S1: ...
    @overload
    def __getitem__(self, idx: Index) -> Series[S1]: ...
    # set item
    @overload
    def __setitem__(self, idx: int, value: S1) -> None: ...
    @overload
    def __setitem__(self, idx: Index, value: Union[S1, Series[S1]]) -> None: ...


class _LocIndexerSeries(_LocIndexer, Generic[S1]):
    @overload
    def __getitem__(self, idx: Union[MaskType, Sequence[str]],) -> Series[S1]: ...
    @overload
    def __getitem__(self, idx: Union[int, str],) -> S1: ...
    @overload
    def __setitem__(self, idx: MaskType, value: Union[S1, np.ndarray, Series[S1]], ) -> None: ...
    @overload
    def __setitem__(self, idx: str, value: S1, ) -> None: ...
    @overload
    def __setitem__(self, idx: List[str], value: Union[S1, np.ndarray, Series[S1]], ) -> None: ...


class Series(IndexOpsMixin, NDFrame, Generic[S1]):

    _ListLike = Union[np.ndarray, Dict[_str, np.ndarray], Sequence, Index]
    def __init__(
        self,
        data: Optional[Union[_ListLike, Series[S1], Dict[int, S1], Dict[_str, S1]]] = ...,
        index: Union[_str, int, Series, List] = ...,
        dtype = ...,
        name: _str = ...,
        copy: bool = ...,
        fastpath: bool = ...
    ): ...
    @property
    def hasnans(self) -> bool: ...
    def div(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[float]: ...
    def rdiv(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    @property
    def dtype(self) -> S1: ...
    @property
    def dtypes(self) -> S1: ...
    @property
    def name(self) -> Optional[Hashable]: ...
    @name.setter
    def name(self, value: Optional[Hashable]) -> None: ...
    @property
    def values(self): ...
    @property
    def array(self) -> ExtensionArray: ...
    def ravel(self, order: _str = ...) -> np.ndarray: ...
    def __len__(self) -> int: ...
    def view(self, dtype = ...) -> Series[S1]: ...
    def __array_ufunc__(self, ufunc: Callable, method: _str, *inputs, **kwargs) : ...
    def __array__(self, dtype=...) -> np.ndarray: ...
    def __float__(self) -> Series[np.float]: ...
    def __long__(self) -> Series[np.long]: ...
    def __int__(self) -> Series[np.int]: ...
    @property
    def axes(self) -> List: ...
    def take(self, indices: Sequence, axis: SeriesAxisType = ..., is_copy: Optional[_bool] = ..., **kwargs) -> Series[S1]:
        """Return the elements in the given *positional* indices along an axis.

This means that we are not indexing according to actual values in
the index attribute of the object. We are indexing according to the
actual position of the element in the object.

Parameters
----------
indices : array-like
    An array of ints indicating which positions to take.
axis : {0 or 'index', 1 or 'columns', None}, default 0
    The axis on which to select elements. ``0`` means that we are
    selecting rows, ``1`` means that we are selecting columns.
is_copy : bool
    Before pandas 1.0, ``is_copy=False`` can be specified to ensure
    that the return value is an actual copy. Starting with pandas 1.0,
    ``take`` always returns a copy, and the keyword is therefore
    deprecated.

    .. deprecated:: 1.0.0
**kwargs
    For compatibility with :meth:`numpy.take`. Has no effect on the
    output.

Returns
-------
taken : same type as caller
    An array-like containing the elements taken from the object.

See Also
--------
DataFrame.loc : Select a subset of a DataFrame by labels.
DataFrame.iloc : Select a subset of a DataFrame by positions.
numpy.take : Take elements from an array along an axis.

Examples
--------
>>> df = pd.DataFrame([('falcon', 'bird', 389.0),
...                    ('parrot', 'bird', 24.0),
...                    ('lion', 'mammal', 80.5),
...                    ('monkey', 'mammal', np.nan)],
...                   columns=['name', 'class', 'max_speed'],
...                   index=[0, 2, 3, 1])
>>> df
     name   class  max_speed
0  falcon    bird      389.0
2  parrot    bird       24.0
3    lion  mammal       80.5
1  monkey  mammal        NaN

Take elements at positions 0 and 3 along the axis 0 (default).

Note how the actual indices selected (0 and 1) do not correspond to
our selected indices 0 and 3. That's because we are selecting the 0th
and 3rd rows, not rows whose indices equal 0 and 3.

>>> df.take([0, 3])
     name   class  max_speed
0  falcon    bird      389.0
1  monkey  mammal        NaN

Take elements at indices 1 and 2 along the axis 1 (column selection).

>>> df.take([1, 2], axis=1)
    class  max_speed
0    bird      389.0
2    bird       24.0
3  mammal       80.5
1  mammal        NaN

We may take elements using negative integers for positive indices,
starting from the end of the object, just like with Python lists.

>>> df.take([-1, -2])
     name   class  max_speed
1  monkey  mammal        NaN
3    lion  mammal       80.5
"""
        pass
    @overload
    def __getitem__(self, idx: Union[List[_str], Index[int], Series[S1], slice]) -> Series: ...
    @overload
    def __getitem__(self, idx: Union[int, _str]) -> S1: ...
    def __setitem__(self, key, value) -> None: ...
    def repeat(self, repeats: Union[int, List[int]], axis: Optional[SeriesAxisType] = ...) -> Series[S1]: ...
    @property
    def index(self) -> Index: ...
    def reset_index(
        self, level: Optional[Level] = ..., drop: _bool = ..., name: Optional[object] = ..., inplace: _bool = ...,
    ) -> Series[S1]: ...
    @overload
    def to_string(
        self,
        buf: Optional[FilePathOrBuffer],
        na_rep: _str = ...,
        formatters = ...,
        float_format = ...,
        sparsify: Optional[_bool] = ...,
        index_names: _bool = ...,
        justify: Optional[_str] = ...,
        max_rows: Optional[int] = ...,
        min_rows: Optional[int] = ...,
        max_cols: Optional[int] = ...,
        show_dimensions: _bool = ...,
        decimal: _str = ...,
        line_width: Optional[int] = ...,
        max_colwidth: Optional[int] = ...,
        encoding: Optional[_str] = ...,
    ) -> None: ...
    @overload
    def to_string(
        self,
        na_rep: _str = ...,
        formatters = ...,
        float_format = ...,
        sparsify: Optional[_bool] = ...,
        index_names: _bool = ...,
        justify: Optional[_str] = ...,
        max_rows: Optional[int] = ...,
        min_rows: Optional[int] = ...,
        max_cols: Optional[int] = ...,
        show_dimensions: _bool = ...,
        decimal: _str = ...,
        line_width: Optional[int] = ...,
        max_colwidth: Optional[int] = ...,
        encoding: Optional[_str] = ...,
    ) -> _str: ...
    @overload
    def to_markdown(self, buf: Optional[FilePathOrBuffer], mode: Optional[_str] = ..., **kwargs) -> None:
        """Print Series in Markdown-friendly format.

.. versionadded:: 1.0.0

Parameters
----------
buf : writable buffer, defaults to sys.stdout
    Where to send the output. By default, the output is printed to
    sys.stdout. Pass a writable buffer if you need to further process
    the output.
mode : str, optional
    Mode in which file is opened.
**kwargs
    These parameters will be passed to `tabulate`.

Returns
-------
str
    Series in Markdown-friendly format.

        Examples
        --------
        >>> s = pd.Series(["elk", "pig", "dog", "quetzal"], name="animal")
        >>> print(s.to_markdown())
        |    | animal   |
        |---:|:---------|
        |  0 | elk      |
        |  1 | pig      |
        |  2 | dog      |
        |  3 | quetzal  |
"""
        pass
    @overload
    def to_markdown(self, mode: Optional[_str] = ...,) -> _str: ...
    def items(self) -> Iterable[Tuple[Union[int, _str], S1]]: ...
    def iteritems(self) -> Iterable[Tuple[Label, S1]]:
        """Lazily iterate over (index, value) tuples.

This method returns an iterable tuple (index, value). This is
convenient if you want to create a lazy iterator.

Returns
-------
iterable
    Iterable of tuples containing the (index, value) pairs from a
    Series.

See Also
--------
DataFrame.items : Iterate over (column name, Series) pairs.
DataFrame.iterrows : Iterate over DataFrame rows as (index, Series) pairs.

Examples
--------
>>> s = pd.Series(['A', 'B', 'C'])
>>> for index, value in s.items():
...     print(f"Index : {index}, Value : {value}")
Index : 0, Value : A
Index : 1, Value : B
Index : 2, Value : C
"""
        pass
    def keys(self) -> List: ...
    def to_dict(self, into: Hashable = ...) -> Dict[_str, Any]: ...
    def to_frame(self, name: Optional[object] = ...) -> DataFrame: ...
    def groupby(
        self,
        by = ...,
        axis: SeriesAxisType = ...,
        level: Optional[Level] = ...,
        as_index: _bool = ...,
        sort: _bool = ...,
        group_keys: _bool = ...,
        squeeze: _bool = ...,
        observed: _bool = ...,
    ) -> SeriesGroupBy:
        """Group Series using a mapper or by a Series of columns.

A groupby operation involves some combination of splitting the
object, applying a function, and combining the results. This can be
used to group large amounts of data and compute operations on these
groups.

Parameters
----------
by : mapping, function, label, or list of labels
    Used to determine the groups for the groupby.
    If ``by`` is a function, it's called on each value of the object's
    index. If a dict or Series is passed, the Series or dict VALUES
    will be used to determine the groups (the Series' values are first
    aligned; see ``.align()`` method). If an ndarray is passed, the
    values are used as-is determine the groups. A label or list of
    labels may be passed to group by the columns in ``self``. Notice
    that a tuple is interpreted as a (single) key.
axis : {0 or 'index', 1 or 'columns'}, default 0
    Split along rows (0) or columns (1).
level : int, level name, or sequence of such, default None
    If the axis is a MultiIndex (hierarchical), group by a particular
    level or levels.
as_index : bool, default True
    For aggregated output, return object with group labels as the
    index. Only relevant for DataFrame input. as_index=False is
    effectively "SQL-style" grouped output.
sort : bool, default True
    Sort group keys. Get better performance by turning this off.
    Note this does not influence the order of observations within each
    group. Groupby preserves the order of rows within each group.
group_keys : bool, default True
    When calling apply, add group keys to index to identify pieces.
squeeze : bool, default False
    Reduce the dimensionality of the return type if possible,
    otherwise return a consistent type.
observed : bool, default False
    This only applies if any of the groupers are Categoricals.
    If True: only show observed values for categorical groupers.
    If False: show all values for categorical groupers.

    .. versionadded:: 0.23.0

Returns
-------
SeriesGroupBy
    Returns a groupby object that contains information about the groups.

See Also
--------
resample : Convenience method for frequency conversion and resampling
    of time series.

Notes
-----
See the `user guide
<https://pandas.pydata.org/pandas-docs/stable/groupby.html>`_ for more.

Examples
--------
>>> ser = pd.Series([390., 350., 30., 20.],
...                 index=['Falcon', 'Falcon', 'Parrot', 'Parrot'], name="Max Speed")
>>> ser
Falcon    390.0
Falcon    350.0
Parrot     30.0
Parrot     20.0
Name: Max Speed, dtype: float64
>>> ser.groupby(["a", "b", "a", "b"]).mean()
a    210.0
b    185.0
Name: Max Speed, dtype: float64
>>> ser.groupby(level=0).mean()
Falcon    370.0
Parrot     25.0
Name: Max Speed, dtype: float64
>>> ser.groupby(ser > 100).mean()
Max Speed
False     25.0
True     370.0
Name: Max Speed, dtype: float64

**Grouping by Indexes**

We can groupby different levels of a hierarchical index
using the `level` parameter:

>>> arrays = [['Falcon', 'Falcon', 'Parrot', 'Parrot'],
...           ['Captive', 'Wild', 'Captive', 'Wild']]
>>> index = pd.MultiIndex.from_arrays(arrays, names=('Animal', 'Type'))
>>> ser = pd.Series([390., 350., 30., 20.], index=index, name="Max Speed")
>>> ser
Animal  Type
Falcon  Captive    390.0
        Wild       350.0
Parrot  Captive     30.0
        Wild        20.0
Name: Max Speed, dtype: float64
>>> ser.groupby(level=0).mean()
Animal
Falcon    370.0
Parrot     25.0
Name: Max Speed, dtype: float64
>>> ser.groupby(level="Type").mean()
Type
Captive    210.0
Wild       185.0
Name: Max Speed, dtype: float64
"""
        pass
    @overload
    def count(self, level: None = ...) -> int: ...
    @overload
    def count(self, level: Level) -> Series[S1]: ...
    def mode(self, dropna) -> Series[S1]: ...
    def unique(self) -> np.ndarray: ...
    def drop_duplicates(self, keep: Union[_str, Literal["first", "last"]] = ..., inplace: _bool = ...) -> Series[S1]: ...
    def duplicated(self, keep: Union[_str, Literal["first", "last"]] = ...) -> Series[_bool]: ...
    def idxmax(self, axis: SeriesAxisType = ..., skipna: _bool = ..., *args, **kwargs) -> Union[int, _str]: ...
    def idxmin(self, axis: SeriesAxisType = ..., skipna: _bool = ..., *args, **kwargs) -> Union[int, _str]: ...
    def round(self, decimals: int = ..., *args, **kwargs) -> Series[S1]: ...
    @overload
    def quantile(
        self, q: float = ..., interpolation: Union[_str, Literal["linear", "lower", "higher", "midpoint", "nearest"]] = ...,
    ) -> float: ...
    @overload
    def quantile(
        self, q: _ListLike = ..., interpolation: Union[_str, Literal["linear", "lower", "higher", "midpoint", "nearest"]] = ...,
    ) -> Series[S1]: ...
    def corr(
        self, other: Series[S1], method: Literal["pearson", "kendall", "spearman"] = ..., min_periods: int = ...,
    ) -> float: ...
    def cov(self, other: Series[S1], min_periods: Optional[int] = ...) -> float: ...
    def diff(self, periods: int = ...) -> Series[S1]: ...
    def autocorr(self, lag: int = ...) -> float: ...
    @overload
    def dot(self, other: Union[DataFrame, Series[S1]]) -> Series[S1]: ...
    @overload
    def dot(self, other: _ListLike) -> np.ndarray: ...
    def __matmul__(self, other): ...
    def __rmatmul__(self, other): ...
    @overload
    def searchsorted(
        self, value: _ListLike, side: Union[_str, Literal["left", "right"]] = ..., sorter: Optional[_ListLike] = ...,
    ) -> Union[int, List[int]]:
        """Find indices where elements should be inserted to maintain order.

Find the indices into a sorted Series `self` such that, if the
corresponding elements in `value` were inserted before the indices,
the order of `self` would be preserved.

.. note::

    The Series *must* be monotonically sorted, otherwise
    wrong locations will likely be returned. Pandas does *not*
    check this for you.

Parameters
----------
value : array_like
    Values to insert into `self`.
side : {'left', 'right'}, optional
    If 'left', the index of the first suitable location found is given.
    If 'right', return the last such index.  If there is no suitable
    index, return either 0 or N (where N is the length of `self`).
sorter : 1-D array_like, optional
    Optional array of integer indices that sort `self` into ascending
    order. They are typically the result of ``np.argsort``.

Returns
-------
int or array of int
    A scalar or array of insertion points with the
    same shape as `value`.

    .. versionchanged:: 0.24.0
        If `value` is a scalar, an int is now always returned.
        Previously, scalar inputs returned an 1-item array for
        :class:`Series` and :class:`Categorical`.

See Also
--------
sort_values
numpy.searchsorted

Notes
-----
Binary search is used to find the required insertion points.

Examples
--------

>>> x = pd.Series([1, 2, 3])
>>> x
0    1
1    2
2    3
dtype: int64

>>> x.searchsorted(4)
3

>>> x.searchsorted([0, 4])
array([0, 3])

>>> x.searchsorted([1, 3], side='left')
array([0, 2])

>>> x.searchsorted([1, 3], side='right')
array([1, 3])

>>> x = pd.Categorical(['apple', 'bread', 'bread',
                        'cheese', 'milk'], ordered=True)
[apple, bread, bread, cheese, milk]
Categories (4, object): [apple < bread < cheese < milk]

>>> x.searchsorted('bread')
1

>>> x.searchsorted(['bread'], side='right')
array([3])

If the values are not monotonically sorted, wrong locations
may be returned:

>>> x = pd.Series([2, 1, 3])
>>> x.searchsorted(1)
0  # wrong result, correct would be 1
"""
        pass
    @overload
    def searchsorted(
        self, value: Scalar, side: Union[_str, Literal["left", "right"]] = ..., sorter: Optional[_ListLike] = ...,
    ) -> int: ...
    def append(
        self,
        to_append: Union[Series, Sequence[Series]],
        ignore_index: _bool = ...,
        verify_integrity: _bool = ...,
    ) -> Series[S1]: ...
    def combine(
        self, other: Series[S1], func: Callable, fill_value: Optional[Scalar] = ...
    ) -> Series[S1]: ...
    def combine_first(self, other: Series[S1]) -> Series[S1]: ...
    def update(self, other: Series[S1]) -> None: ...
    def sort_values(
        self,
        axis: SeriesAxisType = ...,
        ascending: _bool = ...,
        inplace: _bool = ...,
        kind: Union[_str, Literal["quicksort", "heapsort", "mergesort"]] = ...,
        na_position: Union[_str, Literal["first", "last"]] = ...,
        ignore_index: _bool = ...,
    ) -> Series[S1]: ...
    def sort_index(
        self,
        axis: SeriesAxisType = ...,
        level: Optional[Level] = ...,
        ascending: _bool = ...,
        inplace: _bool = ...,
        kind: Union[_str, Literal["quicksort", "heapsort", "mergesort"]] = ...,
        na_position: Union[_str, Literal["first", "last"]] = ...,
        sort_remaining: _bool = ...,
        ignore_index: _bool = ...,
    ) -> Series[S1]: ...
    def argsort(
        self,
        axis: SeriesAxisType = ...,
        kind: Union[_str, Literal["mergesort", "quicksort", "heapsort"]] = ...,
        order: None = ...,
    ) -> Series[int]: ...
    def nlargest(self, n: int = ..., keep: Union[_str, Literal["first", "last", "all"]] = ...) -> Series[S1]: ...
    def nsmallest(self, n: int = ..., keep: Union[_str, Literal["first", "last", "all"]] = ...) -> Series[S1]: ...
    def swaplevel(self, i: Level = ..., j: Level = ..., copy: _bool = ...) -> Series[S1]: ...
    def reorder_levels(self, order: List) -> Series[S1]: ...
    def explode(self) -> Series[S1]: ...
    def unstack(self, level: Level = ..., fill_value: Optional[Union[int, _str, Dict]] = ...,) -> DataFrame: ...
    def map(self, arg, na_action: Optional[Union[_str, Literal["ignore"]]] = ...) -> Series[S1]: ...
    def aggregate(
        self,
        func: Union[Callable, _str, List[Union[Callable, _str]], Dict[SeriesAxisType, Union[Callable, _str]],],
        axis: SeriesAxisType = ...,
        *args,
        **kwargs
    ) -> None:
        """Aggregate using one or more operations over the specified axis.

.. versionadded:: 0.20.0

Parameters
----------
func : function, str, list or dict
    Function to use for aggregating the data. If a function, must either
    work when passed a Series or when passed to Series.apply.

    Accepted combinations are:

    - function
    - string function name
    - list of functions and/or function names, e.g. ``[np.sum, 'mean']``
    - dict of axis labels -> functions, function names or list of such.
axis : {0 or 'index'}
        Parameter needed for compatibility with DataFrame.
*args
    Positional arguments to pass to `func`.
**kwargs
    Keyword arguments to pass to `func`.

Returns
-------
scalar, Series or DataFrame

    The return can be:

    * scalar : when Series.agg is called with single function
    * Series : when DataFrame.agg is called with a single function
    * DataFrame : when DataFrame.agg is called with several functions

    Return scalar, Series or DataFrame.

See Also
--------
Series.apply : Invoke function on a Series.
Series.transform : Transform function producing a Series with like indexes.

Notes
-----
`agg` is an alias for `aggregate`. Use the alias.

A passed user-defined-function will be passed a Series for evaluation.

Examples
--------
>>> s = pd.Series([1, 2, 3, 4])
>>> s
0    1
1    2
2    3
3    4
dtype: int64

>>> s.agg('min')
1

>>> s.agg(['min', 'max'])
min   1
max   4
dtype: int64
"""
        pass
    def agg(
        self,
        func: Union[Callable, _str, List[Union[Callable, _str]], Dict[SeriesAxisType, Union[Callable, _str]],],
        axis: SeriesAxisType = ...,
        *args,
        **kwargs
    ) -> None: ...
    def transform(
        self, func: Union[List[Callable], Dict[_str, Callable]], axis: SeriesAxisType = ..., *args, **kwargs
    ) -> Series[S1]:
        """Call ``func`` on self producing a Series with transformed values.

Produced Series will have same axis length as self.

Parameters
----------
func : function, str, list or dict
    Function to use for transforming the data. If a function, must either
    work when passed a Series or when passed to Series.apply.

    Accepted combinations are:

    - function
    - string function name
    - list of functions and/or function names, e.g. ``[np.exp. 'sqrt']``
    - dict of axis labels -> functions, function names or list of such.
axis : {0 or 'index'}
    Parameter needed for compatibility with DataFrame.
*args
    Positional arguments to pass to `func`.
**kwargs
    Keyword arguments to pass to `func`.

Returns
-------
Series
    A Series that must have the same length as self.

Raises
------
ValueError : If the returned Series has a different length than self.

See Also
--------
Series.agg : Only perform aggregating type operations.
Series.apply : Invoke function on a Series.

Examples
--------
>>> df = pd.DataFrame({'A': range(3), 'B': range(1, 4)})
>>> df
   A  B
0  0  1
1  1  2
2  2  3
>>> df.transform(lambda x: x + 1)
   A  B
0  1  2
1  2  3
2  3  4

Even though the resulting Series must have the same length as the
input Series, it is possible to provide several input functions:

>>> s = pd.Series(range(3))
>>> s
0    0
1    1
2    2
dtype: int64
>>> s.transform([np.sqrt, np.exp])
       sqrt        exp
0  0.000000   1.000000
1  1.000000   2.718282
2  1.414214   7.389056
"""
        pass
    def apply(
        self, func: Callable, convertDType: _bool = ..., args: Tuple = ..., **kwds
    ) -> Union[Series, DataFrame]: ...
    def align(
        self,
        other: Union[DataFrame, Series],
        join: Union[_str, Literal["inner", "outer", "left", "right"]] = ...,
        axis: Optional[AxisType] = ...,
        level: Optional[Level] = ...,
        copy: _bool = ...,
        fill_value = ...,
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill"]]] = ...,
        limit: Optional[int] = ...,
        fill_axis: SeriesAxisType = ...,
        broadcast_axis: Optional[SeriesAxisType] = ...,
    ) -> Tuple[Series, Series]:
        """Align two objects on their axes with the specified join method.

Join method is specified for each axis Index.

Parameters
----------
other : DataFrame or Series
join : {'outer', 'inner', 'left', 'right'}, default 'outer'
axis : allowed axis of the other object, default None
    Align on index (0), columns (1), or both (None).
level : int or level name, default None
    Broadcast across a level, matching Index values on the
    passed MultiIndex level.
copy : bool, default True
    Always returns new objects. If copy=False and no reindexing is
    required then original objects are returned.
fill_value : scalar, default np.NaN
    Value to use for missing values. Defaults to NaN, but can be any
    "compatible" value.
method : {'backfill', 'bfill', 'pad', 'ffill', None}, default None
    Method to use for filling holes in reindexed Series:

    - pad / ffill: propagate last valid observation forward to next valid.
    - backfill / bfill: use NEXT valid observation to fill gap.

limit : int, default None
    If method is specified, this is the maximum number of consecutive
    NaN values to forward/backward fill. In other words, if there is
    a gap with more than this number of consecutive NaNs, it will only
    be partially filled. If method is not specified, this is the
    maximum number of entries along the entire axis where NaNs will be
    filled. Must be greater than 0 if not None.
fill_axis : {0 or 'index'}, default 0
    Filling axis, method and limit.
broadcast_axis : {0 or 'index'}, default None
    Broadcast values along this axis, if aligning two objects of
    different dimensions.

Returns
-------
(left, right) : (Series, type of other)
    Aligned objects.
"""
        pass
    def rename(
        self,
        index = ...,
        *,
        axis: Optional[SeriesAxisType] = ...,
        copy: _bool = ...,
        inplace: _bool = ...,
        level: Optional[Level] = ...,
        errors: Union[_str, Literal["raise", "ignore"]] = ...
    ) -> Series: ...
    def reindex_like(
        self,
        other: Series[S1],
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill", "nearest"]]] = ...,
        copy: _bool = ...,
        limit: Optional[int] = ...,
        tolerance: Optional[float] = ...,
    ) -> Series: ...
    def drop(
        self,
        labels: Optional[Union[_str, List]] = ...,
        axis: SeriesAxisType = ...,
        index: Optional[Union[List[_str], List[int], Index]] = ...,
        columns: Optional[Union[_str, List]] = ...,
        level: Optional[Level] = ...,
        inplace: _bool = ...,
        errors: Literal["ignore", "raise"] = ...,
    ) -> Series: ...
    @overload
    def fillna(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill"]]] = ...,
        axis: SeriesAxisType = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
        *,
        inplace: Literal[True]
    ) -> None:
        """Fill NA/NaN values using the specified method.

Parameters
----------
value : scalar, dict, Series, or DataFrame
    Value to use to fill holes (e.g. 0), alternately a
    dict/Series/DataFrame of values specifying which value to use for
    each index (for a Series) or column (for a DataFrame).  Values not
    in the dict/Series/DataFrame will not be filled. This value cannot
    be a list.
method : {'backfill', 'bfill', 'pad', 'ffill', None}, default None
    Method to use for filling holes in reindexed Series
    pad / ffill: propagate last valid observation forward to next valid
    backfill / bfill: use next valid observation to fill gap.
axis : {0 or 'index'}
    Axis along which to fill missing values.
inplace : bool, default False
    If True, fill in-place. Note: this will modify any
    other views on this object (e.g., a no-copy slice for a column in a
    DataFrame).
limit : int, default None
    If method is specified, this is the maximum number of consecutive
    NaN values to forward/backward fill. In other words, if there is
    a gap with more than this number of consecutive NaNs, it will only
    be partially filled. If method is not specified, this is the
    maximum number of entries along the entire axis where NaNs will be
    filled. Must be greater than 0 if not None.
downcast : dict, default is None
    A dict of item->dtype of what to downcast if possible,
    or the string 'infer' which will try to downcast to an appropriate
    equal type (e.g. float64 to int64 if possible).

Returns
-------
Series or None
    Object with missing values filled or None if ``inplace=True``.

See Also
--------
interpolate : Fill NaN values using interpolation.
reindex : Conform object to new index.
asfreq : Convert TimeSeries to specified frequency.

Examples
--------
>>> df = pd.DataFrame([[np.nan, 2, np.nan, 0],
...                    [3, 4, np.nan, 1],
...                    [np.nan, np.nan, np.nan, 5],
...                    [np.nan, 3, np.nan, 4]],
...                   columns=list('ABCD'))
>>> df
     A    B   C  D
0  NaN  2.0 NaN  0
1  3.0  4.0 NaN  1
2  NaN  NaN NaN  5
3  NaN  3.0 NaN  4

Replace all NaN elements with 0s.

>>> df.fillna(0)
    A   B   C   D
0   0.0 2.0 0.0 0
1   3.0 4.0 0.0 1
2   0.0 0.0 0.0 5
3   0.0 3.0 0.0 4

We can also propagate non-null values forward or backward.

>>> df.fillna(method='ffill')
    A   B   C   D
0   NaN 2.0 NaN 0
1   3.0 4.0 NaN 1
2   3.0 4.0 NaN 5
3   3.0 3.0 NaN 4

Replace all NaN elements in column 'A', 'B', 'C', and 'D', with 0, 1,
2, and 3 respectively.

>>> values = {'A': 0, 'B': 1, 'C': 2, 'D': 3}
>>> df.fillna(value=values)
    A   B   C   D
0   0.0 2.0 2.0 0
1   3.0 4.0 2.0 1
2   0.0 1.0 2.0 5
3   0.0 3.0 2.0 4

Only replace the first NaN element.

>>> df.fillna(value=values, limit=1)
    A   B   C   D
0   0.0 2.0 2.0 0
1   3.0 4.0 NaN 1
2   NaN 1.0 NaN 5
3   NaN 3.0 NaN 4
"""
        pass
    @overload
    def fillna(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill"]]] = ...,
        axis: SeriesAxisType = ...,
        *,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Series[S1]: ...
    @overload
    def fillna(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill"]]] = ...,
        axis: SeriesAxisType = ...,
        inplace: _bool = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...
    ) -> Union[Series[S1], None]: ...
    def replace(
        self,
        to_replace: Optional[Union[_str, List, Dict, Series[S1], int, float]] = ...,
        value: Optional[Union[Scalar, Dict, List, _str]] = ...,
        inplace: _bool = ...,
        limit: Optional[int] = ...,
        regex = ...,
        method: Optional[Union[_str, Literal["pad", "ffill", "bfill"]]] = ...,
    ) -> Series[S1]:
        """Replace values given in `to_replace` with `value`.

Values of the Series are replaced with other values dynamically.
This differs from updating with ``.loc`` or ``.iloc``, which require
you to specify a location to update with some value.

Parameters
----------
to_replace : str, regex, list, dict, Series, int, float, or None
    How to find the values that will be replaced.

    * numeric, str or regex:

        - numeric: numeric values equal to `to_replace` will be
          replaced with `value`
        - str: string exactly matching `to_replace` will be replaced
          with `value`
        - regex: regexs matching `to_replace` will be replaced with
          `value`

    * list of str, regex, or numeric:

        - First, if `to_replace` and `value` are both lists, they
          **must** be the same length.
        - Second, if ``regex=True`` then all of the strings in **both**
          lists will be interpreted as regexs otherwise they will match
          directly. This doesn't matter much for `value` since there
          are only a few possible substitution regexes you can use.
        - str, regex and numeric rules apply as above.

    * dict:

        - Dicts can be used to specify different replacement values
          for different existing values. For example,
          ``{'a': 'b', 'y': 'z'}`` replaces the value 'a' with 'b' and
          'y' with 'z'. To use a dict in this way the `value`
          parameter should be `None`.
        - For a DataFrame a dict can specify that different values
          should be replaced in different columns. For example,
          ``{'a': 1, 'b': 'z'}`` looks for the value 1 in column 'a'
          and the value 'z' in column 'b' and replaces these values
          with whatever is specified in `value`. The `value` parameter
          should not be ``None`` in this case. You can treat this as a
          special case of passing two lists except that you are
          specifying the column to search in.
        - For a DataFrame nested dictionaries, e.g.,
          ``{'a': {'b': np.nan}}``, are read as follows: look in column
          'a' for the value 'b' and replace it with NaN. The `value`
          parameter should be ``None`` to use a nested dict in this
          way. You can nest regular expressions as well. Note that
          column names (the top-level dictionary keys in a nested
          dictionary) **cannot** be regular expressions.

    * None:

        - This means that the `regex` argument must be a string,
          compiled regular expression, or list, dict, ndarray or
          Series of such elements. If `value` is also ``None`` then
          this **must** be a nested dictionary or Series.

    See the examples section for examples of each of these.
value : scalar, dict, list, str, regex, default None
    Value to replace any values matching `to_replace` with.
    For a DataFrame a dict of values can be used to specify which
    value to use for each column (columns not in the dict will not be
    filled). Regular expressions, strings and lists or dicts of such
    objects are also allowed.
inplace : bool, default False
    If True, in place. Note: this will modify any
    other views on this object (e.g. a column from a DataFrame).
    Returns the caller if this is True.
limit : int, default None
    Maximum size gap to forward or backward fill.
regex : bool or same types as `to_replace`, default False
    Whether to interpret `to_replace` and/or `value` as regular
    expressions. If this is ``True`` then `to_replace` *must* be a
    string. Alternatively, this could be a regular expression or a
    list, dict, or array of regular expressions in which case
    `to_replace` must be ``None``.
method : {'pad', 'ffill', 'bfill', `None`}
    The method to use when for replacement, when `to_replace` is a
    scalar, list or tuple and `value` is ``None``.

    .. versionchanged:: 0.23.0
        Added to DataFrame.

Returns
-------
Series
    Object after replacement.

Raises
------
AssertionError
    * If `regex` is not a ``bool`` and `to_replace` is not
      ``None``.
TypeError
    * If `to_replace` is a ``dict`` and `value` is not a ``list``,
      ``dict``, ``ndarray``, or ``Series``
    * If `to_replace` is ``None`` and `regex` is not compilable
      into a regular expression or is a list, dict, ndarray, or
      Series.
    * When replacing multiple ``bool`` or ``datetime64`` objects and
      the arguments to `to_replace` does not match the type of the
      value being replaced
ValueError
    * If a ``list`` or an ``ndarray`` is passed to `to_replace` and
      `value` but they are not the same length.

See Also
--------
Series.fillna : Fill NA values.
Series.where : Replace values based on boolean condition.
Series.str.replace : Simple string replacement.

Notes
-----
* Regex substitution is performed under the hood with ``re.sub``. The
  rules for substitution for ``re.sub`` are the same.
* Regular expressions will only substitute on strings, meaning you
  cannot provide, for example, a regular expression matching floating
  point numbers and expect the columns in your frame that have a
  numeric dtype to be matched. However, if those floating point
  numbers *are* strings, then you can do this.
* This method has *a lot* of options. You are encouraged to experiment
  and play with this method to gain intuition about how it works.
* When dict is used as the `to_replace` value, it is like
  key(s) in the dict are the to_replace part and
  value(s) in the dict are the value parameter.

Examples
--------

**Scalar `to_replace` and `value`**

>>> s = pd.Series([0, 1, 2, 3, 4])
>>> s.replace(0, 5)
0    5
1    1
2    2
3    3
4    4
dtype: int64

>>> df = pd.DataFrame({'A': [0, 1, 2, 3, 4],
...                    'B': [5, 6, 7, 8, 9],
...                    'C': ['a', 'b', 'c', 'd', 'e']})
>>> df.replace(0, 5)
   A  B  C
0  5  5  a
1  1  6  b
2  2  7  c
3  3  8  d
4  4  9  e

**List-like `to_replace`**

>>> df.replace([0, 1, 2, 3], 4)
   A  B  C
0  4  5  a
1  4  6  b
2  4  7  c
3  4  8  d
4  4  9  e

>>> df.replace([0, 1, 2, 3], [4, 3, 2, 1])
   A  B  C
0  4  5  a
1  3  6  b
2  2  7  c
3  1  8  d
4  4  9  e

>>> s.replace([1, 2], method='bfill')
0    0
1    3
2    3
3    3
4    4
dtype: int64

**dict-like `to_replace`**

>>> df.replace({0: 10, 1: 100})
     A  B  C
0   10  5  a
1  100  6  b
2    2  7  c
3    3  8  d
4    4  9  e

>>> df.replace({'A': 0, 'B': 5}, 100)
     A    B  C
0  100  100  a
1    1    6  b
2    2    7  c
3    3    8  d
4    4    9  e

>>> df.replace({'A': {0: 100, 4: 400}})
     A  B  C
0  100  5  a
1    1  6  b
2    2  7  c
3    3  8  d
4  400  9  e

**Regular expression `to_replace`**

>>> df = pd.DataFrame({'A': ['bat', 'foo', 'bait'],
...                    'B': ['abc', 'bar', 'xyz']})
>>> df.replace(to_replace=r'^ba.$', value='new', regex=True)
      A    B
0   new  abc
1   foo  new
2  bait  xyz

>>> df.replace({'A': r'^ba.$'}, {'A': 'new'}, regex=True)
      A    B
0   new  abc
1   foo  bar
2  bait  xyz

>>> df.replace(regex=r'^ba.$', value='new')
      A    B
0   new  abc
1   foo  new
2  bait  xyz

>>> df.replace(regex={r'^ba.$': 'new', 'foo': 'xyz'})
      A    B
0   new  abc
1   xyz  new
2  bait  xyz

>>> df.replace(regex=[r'^ba.$', 'foo'], value='new')
      A    B
0   new  abc
1   new  new
2  bait  xyz

Note that when replacing multiple ``bool`` or ``datetime64`` objects,
the data types in the `to_replace` parameter must match the data
type of the value being replaced:

>>> df = pd.DataFrame({'A': [True, False, True],
...                    'B': [False, True, False]})
>>> df.replace({'a string': 'new value', True: False})  # raises
Traceback (most recent call last):
    ...
TypeError: Cannot compare types 'ndarray(dtype=bool)' and 'str'

This raises a ``TypeError`` because one of the ``dict`` keys is not of
the correct type for replacement.

Compare the behavior of ``s.replace({'a': None})`` and
``s.replace('a', None)`` to understand the peculiarities
of the `to_replace` parameter:

>>> s = pd.Series([10, 'a', 'a', 'b', 'a'])

When one uses a dict as the `to_replace` value, it is like the
value(s) in the dict are equal to the `value` parameter.
``s.replace({'a': None})`` is equivalent to
``s.replace(to_replace={'a': None}, value=None, method=None)``:

>>> s.replace({'a': None})
0      10
1    None
2    None
3       b
4    None
dtype: object

When ``value=None`` and `to_replace` is a scalar, list or
tuple, `replace` uses the method parameter (default 'pad') to do the
replacement. So this is why the 'a' values are being replaced by 10
in rows 1 and 2 and 'b' in row 4 in this case.
The command ``s.replace('a', None)`` is actually equivalent to
``s.replace(to_replace='a', value=None, method='pad')``:

>>> s.replace('a', None)
0    10
1    10
2    10
3     b
4     b
dtype: object
"""
        pass
    def shift(
        self,
        periods: int = ...,
        freq = ...,
        axis: SeriesAxisType = ...,
        fill_value: Optional[object] = ...,
    ) -> Series[S1]:
        """Shift index by desired number of periods with an optional time `freq`.

When `freq` is not passed, shift the index without realigning the data.
If `freq` is passed (in this case, the index must be date or datetime,
or it will raise a `NotImplementedError`), the index will be
increased using the periods and the `freq`.

Parameters
----------
periods : int
    Number of periods to shift. Can be positive or negative.
freq : DateOffset, tseries.offsets, timedelta, or str, optional
    Offset to use from the tseries module or time rule (e.g. 'EOM').
    If `freq` is specified then the index values are shifted but the
    data is not realigned. That is, use `freq` if you would like to
    extend the index when shifting and preserve the original data.
axis : {0 or 'index', 1 or 'columns', None}, default None
    Shift direction.
fill_value : object, optional
    The scalar value to use for newly introduced missing values.
    the default depends on the dtype of `self`.
    For numeric data, ``np.nan`` is used.
    For datetime, timedelta, or period data, etc. :attr:`NaT` is used.
    For extension dtypes, ``self.dtype.na_value`` is used.

    .. versionchanged:: 0.24.0

Returns
-------
Series
    Copy of input object, shifted.

See Also
--------
Index.shift : Shift values of Index.
DatetimeIndex.shift : Shift values of DatetimeIndex.
PeriodIndex.shift : Shift values of PeriodIndex.
tshift : Shift the time index, using the index's frequency if
    available.

Examples
--------
>>> df = pd.DataFrame({'Col1': [10, 20, 15, 30, 45],
...                    'Col2': [13, 23, 18, 33, 48],
...                    'Col3': [17, 27, 22, 37, 52]})

>>> df.shift(periods=3)
   Col1  Col2  Col3
0   NaN   NaN   NaN
1   NaN   NaN   NaN
2   NaN   NaN   NaN
3  10.0  13.0  17.0
4  20.0  23.0  27.0

>>> df.shift(periods=1, axis='columns')
   Col1  Col2  Col3
0   NaN  10.0  13.0
1   NaN  20.0  23.0
2   NaN  15.0  18.0
3   NaN  30.0  33.0
4   NaN  45.0  48.0

>>> df.shift(periods=3, fill_value=0)
   Col1  Col2  Col3
0     0     0     0
1     0     0     0
2     0     0     0
3    10    13    17
4    20    23    27
"""
        pass
    def memory_usage(self, index: _bool = ..., deep: _bool = ...) -> int: ...
    def isin(self, values: Union[Iterable, Series[S1], Dict]) -> Series[_bool]: ...
    def between(
        self, left: Union[Scalar, Sequence], right: Union[Scalar, Sequence], inclusive: _bool = ...,
    ) -> Series[_bool]: ...
    def isna(self) -> Series[_bool]:
        """Detect missing values.

Return a boolean same-sized object indicating if the values are NA.
NA values, such as None or :attr:`numpy.NaN`, gets mapped to True
values.
Everything else gets mapped to False values. Characters such as empty
strings ``''`` or :attr:`numpy.inf` are not considered NA values
(unless you set ``pandas.options.mode.use_inf_as_na = True``).

Returns
-------
Series
    Mask of bool values for each element in Series that
    indicates whether an element is not an NA value.

See Also
--------
Series.isnull : Alias of isna.
Series.notna : Boolean inverse of isna.
Series.dropna : Omit axes labels with missing values.
isna : Top-level isna.

Examples
--------
Show which entries in a DataFrame are NA.

>>> df = pd.DataFrame({'age': [5, 6, np.NaN],
...                    'born': [pd.NaT, pd.Timestamp('1939-05-27'),
...                             pd.Timestamp('1940-04-25')],
...                    'name': ['Alfred', 'Batman', ''],
...                    'toy': [None, 'Batmobile', 'Joker']})
>>> df
   age       born    name        toy
0  5.0        NaT  Alfred       None
1  6.0 1939-05-27  Batman  Batmobile
2  NaN 1940-04-25              Joker

>>> df.isna()
     age   born   name    toy
0  False   True  False   True
1  False  False  False  False
2   True  False  False  False

Show which entries in a Series are NA.

>>> ser = pd.Series([5, 6, np.NaN])
>>> ser
0    5.0
1    6.0
2    NaN
dtype: float64

>>> ser.isna()
0    False
1    False
2     True
dtype: bool
"""
        pass
    def isnull(self) -> Series[_bool]:
        """Detect missing values.

Return a boolean same-sized object indicating if the values are NA.
NA values, such as None or :attr:`numpy.NaN`, gets mapped to True
values.
Everything else gets mapped to False values. Characters such as empty
strings ``''`` or :attr:`numpy.inf` are not considered NA values
(unless you set ``pandas.options.mode.use_inf_as_na = True``).

Returns
-------
Series
    Mask of bool values for each element in Series that
    indicates whether an element is not an NA value.

See Also
--------
Series.isnull : Alias of isna.
Series.notna : Boolean inverse of isna.
Series.dropna : Omit axes labels with missing values.
isna : Top-level isna.

Examples
--------
Show which entries in a DataFrame are NA.

>>> df = pd.DataFrame({'age': [5, 6, np.NaN],
...                    'born': [pd.NaT, pd.Timestamp('1939-05-27'),
...                             pd.Timestamp('1940-04-25')],
...                    'name': ['Alfred', 'Batman', ''],
...                    'toy': [None, 'Batmobile', 'Joker']})
>>> df
   age       born    name        toy
0  5.0        NaT  Alfred       None
1  6.0 1939-05-27  Batman  Batmobile
2  NaN 1940-04-25              Joker

>>> df.isna()
     age   born   name    toy
0  False   True  False   True
1  False  False  False  False
2   True  False  False  False

Show which entries in a Series are NA.

>>> ser = pd.Series([5, 6, np.NaN])
>>> ser
0    5.0
1    6.0
2    NaN
dtype: float64

>>> ser.isna()
0    False
1    False
2     True
dtype: bool
"""
        pass
    def notna(self) -> Series[_bool]:
        """Detect existing (non-missing) values.

Return a boolean same-sized object indicating if the values are not NA.
Non-missing values get mapped to True. Characters such as empty
strings ``''`` or :attr:`numpy.inf` are not considered NA values
(unless you set ``pandas.options.mode.use_inf_as_na = True``).
NA values, such as None or :attr:`numpy.NaN`, get mapped to False
values.

Returns
-------
Series
    Mask of bool values for each element in Series that
    indicates whether an element is not an NA value.

See Also
--------
Series.notnull : Alias of notna.
Series.isna : Boolean inverse of notna.
Series.dropna : Omit axes labels with missing values.
notna : Top-level notna.

Examples
--------
Show which entries in a DataFrame are not NA.

>>> df = pd.DataFrame({'age': [5, 6, np.NaN],
...                    'born': [pd.NaT, pd.Timestamp('1939-05-27'),
...                             pd.Timestamp('1940-04-25')],
...                    'name': ['Alfred', 'Batman', ''],
...                    'toy': [None, 'Batmobile', 'Joker']})
>>> df
   age       born    name        toy
0  5.0        NaT  Alfred       None
1  6.0 1939-05-27  Batman  Batmobile
2  NaN 1940-04-25              Joker

>>> df.notna()
     age   born  name    toy
0   True  False  True  False
1   True   True  True   True
2  False   True  True   True

Show which entries in a Series are not NA.

>>> ser = pd.Series([5, 6, np.NaN])
>>> ser
0    5.0
1    6.0
2    NaN
dtype: float64

>>> ser.notna()
0     True
1     True
2    False
dtype: bool
"""
        pass
    def notnull(self) -> Series[_bool]:
        """Detect existing (non-missing) values.

Return a boolean same-sized object indicating if the values are not NA.
Non-missing values get mapped to True. Characters such as empty
strings ``''`` or :attr:`numpy.inf` are not considered NA values
(unless you set ``pandas.options.mode.use_inf_as_na = True``).
NA values, such as None or :attr:`numpy.NaN`, get mapped to False
values.

Returns
-------
Series
    Mask of bool values for each element in Series that
    indicates whether an element is not an NA value.

See Also
--------
Series.notnull : Alias of notna.
Series.isna : Boolean inverse of notna.
Series.dropna : Omit axes labels with missing values.
notna : Top-level notna.

Examples
--------
Show which entries in a DataFrame are not NA.

>>> df = pd.DataFrame({'age': [5, 6, np.NaN],
...                    'born': [pd.NaT, pd.Timestamp('1939-05-27'),
...                             pd.Timestamp('1940-04-25')],
...                    'name': ['Alfred', 'Batman', ''],
...                    'toy': [None, 'Batmobile', 'Joker']})
>>> df
   age       born    name        toy
0  5.0        NaT  Alfred       None
1  6.0 1939-05-27  Batman  Batmobile
2  NaN 1940-04-25              Joker

>>> df.notna()
     age   born  name    toy
0   True  False  True  False
1   True   True  True   True
2  False   True  True   True

Show which entries in a Series are not NA.

>>> ser = pd.Series([5, 6, np.NaN])
>>> ser
0    5.0
1    6.0
2    NaN
dtype: float64

>>> ser.notna()
0     True
1     True
2    False
dtype: bool
"""
        pass
    def dropna(
        self, axis: SeriesAxisType = ..., inplace: _bool = ..., how: Optional[_str] = ...,
    ) -> Series[S1]: ...
    def to_timestamp(
        self, freq = ..., how: Union[_str, Literal["start", "end", "s", "e"]] = ..., copy: _bool = ...,
    ) -> Series[S1]: ...
    def to_period(self, freq: Optional[_str] = ..., copy: _bool = ...) -> DataFrame: ...
    def str(self) -> _str: ...
    @property
    def dt(self) -> Series: ...
    cat = ...
    def plot(self, **kwargs) -> Union[PlotAxes, np.ndarray]: ...
    sparse = ...
    def hist(
        self,
        by: Optional[object] = ...,
        ax: Optional[PlotAxes] = ...,
        grid: _bool = ...,
        xlabelsize: Optional[int] = ...,
        xrot: Optional[float] = ...,
        ylabelsize: Optional[int] = ...,
        yrot: Optional[float] = ...,
        figsize: Optional[Tuple[float, float]] = ...,
        bins: Union[int, Sequence] = ...,
        backend: Optional[_str] = ...,
        **kwargs
    ) -> SubplotBase: ...
    def swapaxes(self, axis1: SeriesAxisType, axis2: SeriesAxisType, copy: _bool = ...) -> Series[S1]: ...
    def droplevel(self, level: Level, axis: SeriesAxisType = ...) -> DataFrame: ...
    def pop(self, item: _str) -> Series[S1]: ...
    def squeeze(self, axis: Optional[SeriesAxisType] = ...) -> Scalar: ...
    def __abs__(self) -> Series[S1]: ...
    def add_prefix(self, prefix: _str) -> Series[S1]: ...
    def add_suffix(self, suffix: _str) -> Series[S1]: ...
    def reindex(self, index: Optional[_ListLike] = ..., **kwargs) -> Series[S1]:
        """Conform Series to new index with optional filling logic.

Places NA/NaN in locations having no value in the previous index. A new object
is produced unless the new index is equivalent to the current one and
``copy=False``.

Parameters
----------

index : array-like, optional
    New labels / index to conform to, should be specified using
    keywords. Preferably an Index object to avoid duplicating data.

method : {None, 'backfill'/'bfill', 'pad'/'ffill', 'nearest'}
    Method to use for filling holes in reindexed DataFrame.
    Please note: this is only applicable to DataFrames/Series with a
    monotonically increasing/decreasing index.

    * None (default): don't fill gaps
    * pad / ffill: Propagate last valid observation forward to next
      valid.
    * backfill / bfill: Use next valid observation to fill gap.
    * nearest: Use nearest valid observations to fill gap.

copy : bool, default True
    Return a new object, even if the passed indexes are the same.
level : int or name
    Broadcast across a level, matching Index values on the
    passed MultiIndex level.
fill_value : scalar, default np.NaN
    Value to use for missing values. Defaults to NaN, but can be any
    "compatible" value.
limit : int, default None
    Maximum number of consecutive elements to forward or backward fill.
tolerance : optional
    Maximum distance between original and new labels for inexact
    matches. The values of the index at the matching locations most
    satisfy the equation ``abs(index[indexer] - target) <= tolerance``.

    Tolerance may be a scalar value, which applies the same tolerance
    to all values, or list-like, which applies variable tolerance per
    element. List-like includes list, tuple, array, Series, and must be
    the same size as the index and its dtype must exactly match the
    index's type.

    .. versionadded:: 0.21.0 (list-like tolerance)

Returns
-------
Series with changed index.

See Also
--------
DataFrame.set_index : Set row labels.
DataFrame.reset_index : Remove row labels or move them to new columns.
DataFrame.reindex_like : Change to same indices as other DataFrame.

Examples
--------

``DataFrame.reindex`` supports two calling conventions

* ``(index=index_labels, columns=column_labels, ...)``
* ``(labels, axis={'index', 'columns'}, ...)``

We *highly* recommend using keyword arguments to clarify your
intent.

Create a dataframe with some fictional data.

>>> index = ['Firefox', 'Chrome', 'Safari', 'IE10', 'Konqueror']
>>> df = pd.DataFrame({'http_status': [200, 200, 404, 404, 301],
...                   'response_time': [0.04, 0.02, 0.07, 0.08, 1.0]},
...                   index=index)
>>> df
           http_status  response_time
Firefox            200           0.04
Chrome             200           0.02
Safari             404           0.07
IE10               404           0.08
Konqueror          301           1.00

Create a new index and reindex the dataframe. By default
values in the new index that do not have corresponding
records in the dataframe are assigned ``NaN``.

>>> new_index = ['Safari', 'Iceweasel', 'Comodo Dragon', 'IE10',
...              'Chrome']
>>> df.reindex(new_index)
               http_status  response_time
Safari               404.0           0.07
Iceweasel              NaN            NaN
Comodo Dragon          NaN            NaN
IE10                 404.0           0.08
Chrome               200.0           0.02

We can fill in the missing values by passing a value to
the keyword ``fill_value``. Because the index is not monotonically
increasing or decreasing, we cannot use arguments to the keyword
``method`` to fill the ``NaN`` values.

>>> df.reindex(new_index, fill_value=0)
               http_status  response_time
Safari                 404           0.07
Iceweasel                0           0.00
Comodo Dragon            0           0.00
IE10                   404           0.08
Chrome                 200           0.02

>>> df.reindex(new_index, fill_value='missing')
              http_status response_time
Safari                404          0.07
Iceweasel         missing       missing
Comodo Dragon     missing       missing
IE10                  404          0.08
Chrome                200          0.02

We can also reindex the columns.

>>> df.reindex(columns=['http_status', 'user_agent'])
           http_status  user_agent
Firefox            200         NaN
Chrome             200         NaN
Safari             404         NaN
IE10               404         NaN
Konqueror          301         NaN

Or we can use "axis-style" keyword arguments

>>> df.reindex(['http_status', 'user_agent'], axis="columns")
           http_status  user_agent
Firefox            200         NaN
Chrome             200         NaN
Safari             404         NaN
IE10               404         NaN
Konqueror          301         NaN

To further illustrate the filling functionality in
``reindex``, we will create a dataframe with a
monotonically increasing index (for example, a sequence
of dates).

>>> date_index = pd.date_range('1/1/2010', periods=6, freq='D')
>>> df2 = pd.DataFrame({"prices": [100, 101, np.nan, 100, 89, 88]},
...                    index=date_index)
>>> df2
            prices
2010-01-01   100.0
2010-01-02   101.0
2010-01-03     NaN
2010-01-04   100.0
2010-01-05    89.0
2010-01-06    88.0

Suppose we decide to expand the dataframe to cover a wider
date range.

>>> date_index2 = pd.date_range('12/29/2009', periods=10, freq='D')
>>> df2.reindex(date_index2)
            prices
2009-12-29     NaN
2009-12-30     NaN
2009-12-31     NaN
2010-01-01   100.0
2010-01-02   101.0
2010-01-03     NaN
2010-01-04   100.0
2010-01-05    89.0
2010-01-06    88.0
2010-01-07     NaN

The index entries that did not have a value in the original data frame
(for example, '2009-12-29') are by default filled with ``NaN``.
If desired, we can fill in the missing values using one of several
options.

For example, to back-propagate the last valid value to fill the ``NaN``
values, pass ``bfill`` as an argument to the ``method`` keyword.

>>> df2.reindex(date_index2, method='bfill')
            prices
2009-12-29   100.0
2009-12-30   100.0
2009-12-31   100.0
2010-01-01   100.0
2010-01-02   101.0
2010-01-03     NaN
2010-01-04   100.0
2010-01-05    89.0
2010-01-06    88.0
2010-01-07     NaN

Please note that the ``NaN`` value present in the original dataframe
(at index value 2010-01-03) will not be filled by any of the
value propagation schemes. This is because filling while reindexing
does not look at dataframe values, but only compares the original and
desired indexes. If you do want to fill in the ``NaN`` values present
in the original dataframe, use the ``fillna()`` method.

See the :ref:`user guide <basics.reindexing>` for more.
"""
        pass
    def filter(
        self,
        items: Optional[_ListLike] = ...,
        like: Optional[_str] = ...,
        regex: Optional[_str] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def head(self, n: int = ...) -> Series[S1]: ...
    def tail(self, n: int = ...) -> Series[S1]: ...
    def sample(
        self,
        n: Optional[int] = ...,
        frac: Optional[float] = ...,
        replace: _bool = ...,
        weights: Optional[Union[_str, _ListLike, np.ndarray]] = ...,
        random_state: Optional[int] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def astype(
        self,
        dtype: Union[S1, _str],
        copy: _bool = ...,
        errors: Union[_str, Literal["raise", "ignore"]] = ...,
    ) -> Series: ...
    def copy(self, deep: _bool = ...) -> Series[S1]: ...
    def infer_objects(self) -> Series[S1]: ...
    def convertDTypes(
        self,
        infer_objects: _bool = ...,
        convert_string: _bool = ...,
        convert_integer: _bool = ...,
        convert_boolean: _bool = ...,
    ) -> Series[S1]: ...
    @overload
    def ffill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType,
        inplace: Literal[True],
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Series[S1]: ...
    @overload
    def ffill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        inplace: Literal[True],
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> None: ...
    @overload
    def ffill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType = ...,
        *,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Series[S1]: ...
    @overload
    def ffill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType = ...,
        inplace: _bool = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Union[Series[S1], None]: ...
    @overload
    def bfill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
        *,
        inplace: Literal[True]
    ) -> None: ...
    @overload
    def bfill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType = ...,
        *,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Series[S1]: ...
    @overload
    def bfill(
        self,
        value: Union[S1, Dict, Series[S1], DataFrame],
        axis: SeriesAxisType = ...,
        inplace: _bool = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Union[Series[S1], None]: ...
    def interpolate(
        self,
        method: Union[_str, Literal[
            "linear",
            "time",
            "index",
            "values",
            "pad",
            "nearest",
            "slinear",
            "quadratic",
            "cubic",
            "spline",
            "barycentric",
            "polynomial",
            "krogh",
            "pecewise_polynomial",
            "spline",
            "pchip",
            "akima",
            "from_derivatives",
        ]] = ...,
        axis: Optional[SeriesAxisType] = ...,
        limit: Optional[int] = ...,
        inplace: _bool = ...,
        limit_direction: Optional[Union[_str, Literal["forward", "backward", "both"]]] = ...,
        limit_area: Optional[Union[_str, Literal["inside", "outside"]]] = ...,
        downcast: Optional[Union[_str, Literal["infer"]]] = ...,
        **kwargs
    ) -> Series[S1]: ...
    def asof(
        self, where: Union[Scalar, Sequence[Scalar]], subset: Optional[Union[_str, Sequence[_str]]] = ...,
    ) -> Union[Scalar, Series[S1]]: ...
    def clip(
        self,
        lower: Optional[float] = ...,
        upper: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
        inplace: _bool = ...,
        *args,
        **kwargs
    ) -> Series[S1]: ...
    def asfreq(
        self,
        freq,
        method: Optional[Union[_str, Literal["backfill", "bfill", "pad", "ffill"]]] = ...,
        how: Optional[Union[_str, Literal["start", "end"]]] = ...,
        normalize: _bool = ...,
        fill_value: Optional[Scalar] = ...,
    ) -> Series[S1]: ...
    def at_time(
        self, time: Union[_str, time], asof: _bool = ..., axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def between_time(
        self,
        start_time: Union[_str, time],
        end_time: Union[_str, time],
        include_start: _bool = ...,
        include_end: _bool = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    # Next one should return a 'Resampler' object
    def resample(
        self,
        rule,
        axis: SeriesAxisType = ...,
        closed: Optional[_str] = ...,
        label: Optional[_str] = ...,
        convention: Union[_str, Literal["start", "end", "s", "e"]] = ...,
        kind: Optional[Union[_str, Literal["timestamp", "period"]]] = ...,
        loffset = ...,
        base: int = ...,
        on: Optional[_str] = ...,
        level: Optional[Level] = ...,
    ) : ...
    def first(self, offset) -> Series[S1]: ...
    def last(self, offset) -> Series[S1]: ...
    def rank(
        self,
        axis: SeriesAxisType = ...,
        method: Union[_str, Literal["average", "min", "max", "first", "dense"]] = ...,
        numeric_only: Optional[_bool] = ...,
        na_option: Union[_str, Literal["keep", "top", "bottom"]] = ...,
        ascending: _bool = ...,
        pct: _bool = ...,
    ) -> Series: ...
    def where(
        self,
        cond: Union[Series[S1], Series[S1], np.ndarray],
        other = ...,
        inplace: _bool = ...,
        axis: Optional[SeriesAxisType] = ...,
        level: Optional[Level] = ...,
        errors: _str = ...,
        try_cast: _bool = ...,
    ) -> Series[S1]: ...
    def mask(
        self,
        cond: Union[Series[S1], np.ndarray, Callable],
        other: Union[Scalar, Series[S1], DataFrame, Callable] = ...,
        inplace: _bool = ...,
        axis: Optional[SeriesAxisType] = ...,
        level: Optional[Level] = ...,
        errors: Union[_str, Literal["raise", "ignore"]] = ...,
        try_cast: _bool = ...,
    ) -> Series[S1]: ...
    def slice_shift(self, periods: int = ..., axis: SeriesAxisType = ...) -> Series[S1]: ...
    def tshift(self, periods: int = ..., freq = ..., axis: SeriesAxisType = ...) -> Series[S1]: ...
    def truncate(
        self,
        before: Optional[Union[date, _str, int]] = ...,
        after: Optional[Union[date, _str, int]] = ...,
        axis: Optional[SeriesAxisType] = ...,
        copy: _bool = ...,
    ) -> Series[S1]: ...
    def tz_convert(
        self, tz, axis: SeriesAxisType = ..., level: Optional[Level] = ..., copy: _bool = ...,
    ) -> Series[S1]: ...
    def tz_localize(
        self,
        tz,
        axis: SeriesAxisType = ...,
        level: Optional[Level] = ...,
        copy: _bool = ...,
        ambiguous = ...,
        nonexistent: _str = ...,
    ) -> Series[S1]: ...
    def abs(self) -> Series[S1]: ...
    def describe(
        self,
        percentiles: Optional[List[float]] = ...,
        include: Optional[Union[_str, Literal["all"], List[S1]]] = ...,
        exclude: Optional[List[S1]] = ...,
    ) -> Series[S1]: ...
    def pct_change(
        self,
        periods: int = ...,
        fill_method: _str = ...,
        limit: Optional[int] = ...,
        freq = ...,
        **kwargs
    ) -> Series[S1]: ...
    def first_valid_index(self) -> Scalar: ...
    def last_valid_index(self) -> Scalar: ...
    def value_counts(
        self,
        normalize: _bool = ...,
        sort: _bool = ...,
        ascending: _bool = ...,
        bins: Optional[int] = ...,
        dropna: _bool = ...,
    ) -> Series[S1]: ...
    def transpose(self, *args, **kwargs) -> Series[S1]: ...
    @property
    def T(self) -> Series[S1]: ...

    # The rest of these were left over from the old 
    # stubs we shipped in preview. They may belong in 
    # the base classes in some cases; I expect stubgen
    # just failed to generate these so I couldn't match 
    # them up.

    def __add__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __and__(self, other: Union[_ListLike, Series[S1]]) -> Series[_bool]: ...
    # def __array__(self, dtype: Optional[_bool] = ...) -> _np_ndarray
    def __div__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __eq__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __floordiv__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[int]: ...
    def __ge__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __gt__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    # def __iadd__(self, other: S1) -> Series[S1]: ...
    # def __iand__(self, other: S1) -> Series[_bool]: ...
    # def __idiv__(self, other: S1) -> Series[S1]: ...
    # def __ifloordiv__(self, other: S1) -> Series[S1]: ...
    # def __imod__(self, other: S1) -> Series[S1]: ...
    # def __imul__(self, other: S1) -> Series[S1]: ...
    # def __ior__(self, other: S1) -> Series[_bool]: ...
    # def __ipow__(self, other: S1) -> Series[S1]: ...
    # def __isub__(self, other: S1) -> Series[S1]: ...
    # def __itruediv__(self, other: S1) -> Series[S1]: ...
    # def __itruediv__(self, other) -> None: ...
    # def __ixor__(self, other: S1) -> Series[_bool]: ...
    def __le__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __lt__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __mul__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __mod__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __ne__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __pow__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __or__(self, other: Union[_ListLike, Series[S1]]) -> Series[_bool]: ...
    def __radd__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rand__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __rdiv__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rdivmod__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rfloordiv__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rmod__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rmul__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rnatmul__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rpow__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __ror__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __rsub__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rtruediv__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __rxor__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[_bool]: ...
    def __sub__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __truediv__(self, other: Union[num, _ListLike, Series[S1]]) -> Series[S1]: ...
    def __xor__(self, other: Union[_ListLike, Series[S1]]) -> Series: ...
    # properties
    # @property
    # def array(self) -> _npndarray
    @property
    def at(self) -> _LocIndexerSeries[S1]: ...

    # @property
    # def cat(self) -> ?

    @property
    def iat(self) -> _iLocIndexerSeries[S1]: ...
    @property
    def iloc(self) -> _iLocIndexerSeries[S1]: ...
    @property
    def loc(self) -> _LocIndexerSeries[S1]: ...

    # Methods
    def add(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: int = ...,
    ) -> Series[S1]: ...
    def all(
        self,
        axis: SeriesAxisType = ...,
        bool_only: Optional[_bool] = ...,
        skipna: _bool = ...,
        level: Optional[Level] = ...,
        **kwargs
    ) -> _bool: ...
    def any(
        self,
        axis: SeriesAxisType = ...,
        bool_only: Optional[_bool] = ...,
        skipna: _bool = ...,
        level: Optional[Level] = ...,
        **kwargs
    ) -> _bool: ...
    def cummax(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., *args, **kwargs
    ) -> Series[S1]: ...
    def cummin(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., *args, **kwargs
    ) -> Series[S1]: ...
    def cumprod(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., *args, **kwargs
    ) -> Series[S1]: ...
    def cumsum(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., *args, **kwargs
    ) -> Series[S1]: ...
    def divide(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[float]: ...
    def divmod(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def eq(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    def ewm(
        self,
        com: Optional[float] = ...,
        span: Optional[float] = ...,
        halflife: Optional[float] = ...,
        alpha: Optional[float] = ...,
        min_periods: int = ...,
        adjust: _bool = ...,
        ignore_na: _bool = ...,
        axis: SeriesAxisType = ...,
    ) -> DataFrame: ...
    def expanding(self, min_periods: int = ..., center: _bool = ..., axis: SeriesAxisType = ...) -> DataFrame: ...
    def floordiv(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[int]: ...
    def ge(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    def gt(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    def item(self) -> S1: ...
    @overload
    def kurt(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def kurt(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> Scalar: ...
    @overload
    def kurtosis(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Optional[Level],
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def kurtosis(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> Scalar: ...
    def le(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    def lt(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    @overload
    def mad(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., *, level: Optional[Level], **kwargs
    ) -> Series[S1]: ...
    @overload
    def mad(
        self, axis: Optional[SeriesAxisType] = ..., skipna: _bool = ..., level: None = ..., **kwargs
    ) -> Scalar: ...
    @overload
    def max(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def max(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> S1: ...
    @overload
    def mean(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def mean(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> S1: ...
    @overload
    def median(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def median(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> S1: ...
    @overload
    def min(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def min(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: _bool = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> S1: ...
    def mod(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def mul(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def multiply(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    def ne(
        self,
        other: Union[Scalar, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[_bool]: ...
    def nunique(self, dropna: _bool = ...) -> int: ...
    def pow(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> Series[S1]: ...
    @overload
    def prod(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        numeric_only: Optional[_bool] = ...,
        min_count: int = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def prod(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        min_count: int = ...,
        **kwargs
    ) -> Scalar: ...
    @overload
    def product(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        numeric_only: Optional[_bool] = ...,
        min_count: int = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def product(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        min_count: int = ...,
        **kwargs
    ) -> Scalar: ...
    def radd(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rdivmod(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rfloordiv(
        self,
        other,
        level: Optional[Level] = ...,
        fill_value: Optional[Union[float, None]] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rmod(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rmul(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    # Next one should return a window class
    def rolling(
        self,
        window,
        min_periods: Optional[int] = ...,
        center: _bool = ...,
        win_type: Optional[_str] = ...,
        on: Optional[_str] = ...,
        axis: SeriesAxisType = ...,
        closed: Optional[_str] = ...,
    ) : ...
    def rpow(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rsub(
        self,
        other: Union[Series[S1], Scalar],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    def rtruediv(
        self,
        other,
        level: Optional[Level] = ...,
        fill_value: Optional[Union[float, None]] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[S1]: ...
    @overload
    def sem(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def sem(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> Scalar: ...
    @overload
    def skew(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def skew(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> Scalar: ...
    @overload
    def std(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[float]: ...
    @overload
    def std(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> float: ...
    def sub(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> float: ...
    def subtract(
        self,
        other: Union[num, _ListLike, Series[S1]],
        level: Optional[Level] = ...,
        fill_value: Optional[float] = ...,
        axis: Optional[SeriesAxisType] = ...,
    ) -> float: ...
    def sum(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: Optional[Level] = ...,
        numeric_only: Optional[_bool] = ...,
        min_count: int = ...,
        **kwargs
    ) -> float: ...
    def to_list(self) -> List: ...
    def to_numpy(
        self, dtype: Optional[Type[DtypeNp]] = ..., copy: _bool = ..., na_value = ..., **kwargs
    ) -> np.ndarray: ...
    def to_records(
        self,
        index: _bool = ...,
        columnDTypes: Optional[Union[_str, Dict]] = ...,
        indexDTypes: Optional[Union[_str, Dict]] = ...,
    ) : ...
    def tolist(self) -> List: ...
    def truediv(
        self,
        other,
        level: Optional[Level] = ...,
        fill_value: Optional[Union[float, None]] = ...,
        axis: SeriesAxisType = ...,
    ) -> Series[float]: ...
    @overload
    def var(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> Series[S1]: ...
    @overload
    def var(
        self,
        axis: Optional[SeriesAxisType] = ...,
        skipna: Optional[_bool] = ...,
        level: None = ...,
        ddof: int = ...,
        numeric_only: Optional[_bool] = ...,
        **kwargs
    ) -> Scalar: ...



