from matplotlib.axes import Axes as PlotAxes, SubplotBase as AxesSubplot
import numpy as np
import sys
from pandas._typing import FrameOrSeries as FrameOrSeries, AxisType, Dtype, Level
from pandas.core.frame import DataFrame as DataFrame
from pandas.core.groupby.groupby import GroupBy as GroupBy #, get_groupby as get_groupby
from pandas.core.groupby.grouper import Grouper as Grouper
from pandas.core.series import Series as Series
from typing import Any, Callable, Dict, FrozenSet, NamedTuple, Optional, Sequence, Tuple, Type, Union, overload
if sys.version_info >= (3, 8):
    from typing import Literal
else:
    from typing_extensions import Literal

class NamedAgg(NamedTuple):
    column = ...
    aggfunc = ...

AggScalar = Union[str, Callable[..., Any]]
ScalarResult = ...

def generate_property(name: str, klass: Type[FrameOrSeries]) : ...
def pin_whitelisted_properties(klass: Type[FrameOrSeries], whitelist: FrozenSet[str]) : ...

class SeriesGroupBy(GroupBy):
    def apply(self, func, *args, **kwargs): ...
    def aggregate(self, func = ..., *args, **kwargs): ...
    agg = aggregate
    def transform(self, func, *args, **kwargs): ...
    def filter(self, func, dropna: bool = ..., *args, **kwargs): ...
    def nunique(self, dropna: bool = ...) -> Series: ...
    def describe(self, **kwargs) -> Series[np.double64]: ...
    def value_counts(
        self, normalize: bool = ..., sort: bool = ..., ascending: bool = ..., bins = ..., dropna: bool = ...,
    ) -> DataFrame: ...
    def count(self) -> Series[Dtype]: ...
    def pct_change(
        self, periods: int = ..., fill_method: str = ..., limit = ..., freq = ..., axis: AxisType = ...,
    ) -> Series[Dtype]: ...
    # Overrides and others from original pylance stubs
    @property
    def is_monotonic_increasing(self) -> bool: ...
    @property
    def is_monotonic_decreasing(self) -> bool: ...
    def __getitem__(self, item: str) -> Series[Dtype]: ...
    def bfill(self, limit: Optional[int] = ...) -> Series[Dtype]: ...
    def cummax(self, axis: AxisType = ..., **kwargs) -> Series[Dtype]: ...
    def cummin(self, axis: AxisType = ..., **kwargs) -> Series[Dtype]: ...
    def cumprod(self, axis: AxisType = ..., **kwargs) -> Series[Dtype]: ...
    def cumsum(self, axis: AxisType = ..., **kwargs) -> Series[Dtype]: ...
    def ffill(self, limit: Optional[int] = ...) -> Series[Dtype]: ...
    def first(self, **kwargs) -> Series[Dtype]: ...
    def head(self, n: int = ...) -> Series[Dtype]: ...
    def last(self, **kwargs) -> Series[Dtype]: ...
    def max(self, **kwargs) -> Series[Dtype]: ...
    def mean(self, **kwargs) -> Series[Dtype]: ...
    def median(self, **kwargs) -> Series[Dtype]: ...
    def min(self, **kwargs) -> Series[Dtype]: ...
    def nlargest(self, n: int = ..., keep: str = ...) -> Series[Dtype]: ...
    def nsmallest(self, n: int = ..., keep: str = ...) -> Series[Dtype]: ...
    def nth(self, n: Union[int, Sequence[int]], dropna: Optional[str] = ...) -> Series[Dtype]: ...


class DataFrameGroupBy(GroupBy):
    @overload
    def aggregate(self, arg: str, *args, **kwargs) -> DataFrame:
        """Aggregate using one or more operations over the specified axis.

Parameters
----------
func : function, str, list or dict
    Function to use for aggregating the data. If a function, must either
    work when passed a DataFrame or when passed to DataFrame.apply.

    Accepted combinations are:

    - function
    - string function name
    - list of functions and/or function names, e.g. ``[np.sum, 'mean']``
    - dict of axis labels -> functions, function names or list of such.

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
pandas.DataFrame.groupby.apply
pandas.DataFrame.groupby.transform
pandas.DataFrame.aggregate

Notes
-----
`agg` is an alias for `aggregate`. Use the alias.

A passed user-defined-function will be passed a Series for evaluation.

Examples
--------

>>> df = pd.DataFrame({'A': [1, 1, 2, 2],
...                    'B': [1, 2, 3, 4],
...                    'C': np.random.randn(4)})

>>> df
   A  B         C
0  1  1  0.362838
1  1  2  0.227877
2  2  3  1.267767
3  2  4 -0.562860

The aggregation is for each column.

>>> df.groupby('A').agg('min')
   B         C
A
1  1  0.227877
2  3 -0.562860

Multiple aggregations

>>> df.groupby('A').agg(['min', 'max'])
    B             C
  min max       min       max
A
1   1   2  0.227877  0.362838
2   3   4 -0.562860  1.267767

Select a column for aggregation

>>> df.groupby('A').B.agg(['min', 'max'])
   min  max
A
1    1    2
2    3    4

Different aggregations per column

>>> df.groupby('A').agg({'B': ['min', 'max'], 'C': 'sum'})
    B             C
  min max       sum
A
1   1   2  0.590716
2   3   4  0.704907

To control the output names with different aggregations per column,
pandas supports "named aggregation"

>>> df.groupby("A").agg(
...     b_min=pd.NamedAgg(column="B", aggfunc="min"),
...     c_sum=pd.NamedAgg(column="C", aggfunc="sum"))
   b_min     c_sum
A
1      1 -1.956929
2      3 -0.322183

- The keywords are the *output* column names
- The values are tuples whose first element is the column to select
  and the second element is the aggregation to apply to that column.
  Pandas provides the ``pandas.NamedAgg`` namedtuple with the fields
  ``['column', 'aggfunc']`` to make it clearer what the arguments are.
  As usual, the aggregation can be a callable or a string alias.

See :ref:`groupby.aggregate.named` for more.
"""
        pass
    @overload
    def aggregate(self, arg: Dict, *args, **kwargs) -> DataFrame: ...
    @overload
    def aggregate(self, arg: Callable[[], Any], *args, **kwargs) -> DataFrame: ...
    @overload
    def agg(self, arg: str, *args, **kwargs) -> DataFrame: ...
    @overload
    def agg(self, arg: Dict, *args, **kwargs) -> DataFrame: ...
    @overload
    def agg(self, arg: Callable[[], Any], *args, **kwargs) -> DataFrame: ...
    def transform(self, func, *args, **kwargs): ...
    def filter(self, func: Callable, dropna: bool = ..., *args, **kwargs) -> DataFrame:
        """Return a copy of a DataFrame excluding elements from groups that
do not satisfy the boolean criterion specified by func.

Parameters
----------
f : function
    Function to apply to each subframe. Should return True or False.
dropna : Drop groups that do not pass the filter. True by default;
    If False, groups that evaluate False are filled with NaNs.

Returns
-------
filtered : DataFrame

Notes
-----
Each subframe is endowed the attribute 'name' in case you need to know
which group you are working on.

Examples
--------
>>> df = pd.DataFrame({'A' : ['foo', 'bar', 'foo', 'bar',
...                           'foo', 'bar'],
...                    'B' : [1, 2, 3, 4, 5, 6],
...                    'C' : [2.0, 5., 8., 1., 2., 9.]})
>>> grouped = df.groupby('A')
>>> grouped.filter(lambda x: x['B'].mean() > 3.)
        A  B    C
1  bar  2  5.0
3  bar  4  1.0
5  bar  6  9.0
"""
        pass
    @overload
    def __getitem__(self, item: str) -> SeriesGroupBy[Dtype]: ...
    @overload
    def __getitem__(self, item: Sequence[str]) -> DataFrameGroupBy: ...
    def count(self) -> DataFrame:
        """Compute count of group, excluding missing values.

Returns
-------
DataFrame
    Count of values within each group.
"""
        pass
    def nunique(self, dropna: bool = ...) -> DataFrame:
        """
Return DataFrame with number of distinct observations per group for
each column.

Parameters
----------
dropna : bool, default True
    Don't include NaN in the counts.

Returns
-------
nunique: DataFrame

Examples
--------
>>> df = pd.DataFrame({'id': ['spam', 'egg', 'egg', 'spam',
...                           'ham', 'ham'],
...                    'value1': [1, 5, 5, 2, 5, 5],
...                    'value2': list('abbaxy')})
>>> df
        id  value1 value2
0  spam       1      a
1   egg       5      b
2   egg       5      b
3  spam       2      a
4   ham       5      x
5   ham       5      y

>>> df.groupby('id').nunique()
    id  value1  value2
id
egg    1       1       1
ham    1       1       2
spam   1       2       1

Check for rows with the same id but conflicting values:

>>> df.groupby('id').filter(lambda g: (g.nunique() > 1).any())
        id  value1 value2
0  spam       1      a
3  spam       2      a
4   ham       5      x
5   ham       5      y
"""
        pass
    def boxplot(
        self,
        grouped: DataFrame,
        subplots: bool = ...,
        column: Optional[Union[str, Sequence]] = ...,
        fontsize: Union[int, str] = ...,
        rot: float = ...,
        grid: bool = ...,
        ax: Optional[PlotAxes] = ...,
        figsize: Optional[Tuple[float, float]] = ...,
        layout: Optional[Tuple[int, int]] = ...,
        sharex: bool = ...,
        sharey: bool = ...,
        bins: Union[int, Sequence] = ...,
        backend: Optional[str] = ...,
        **kwargs
    ) -> Union[AxesSubplot, Sequence[AxesSubplot]]:
        """Make box plots from DataFrameGroupBy data.

Parameters
----------
grouped : Grouped DataFrame
subplots : bool
    * ``False`` - no subplots will be used
    * ``True`` - create a subplot for each group.

column : column name or list of names, or vector
    Can be any valid input to groupby.
fontsize : int or str
rot : label rotation angle
grid : Setting this to True will show the grid
ax : Matplotlib axis object, default None
figsize : A tuple (width, height) in inches
layout : tuple (optional)
    The layout of the plot: (rows, columns).
sharex : bool, default False
    Whether x-axes will be shared among subplots.

    .. versionadded:: 0.23.1
sharey : bool, default True
    Whether y-axes will be shared among subplots.

    .. versionadded:: 0.23.1
backend : str, default None
    Backend to use instead of the backend specified in the option
    ``plotting.backend``. For instance, 'matplotlib'. Alternatively, to
    specify the ``plotting.backend`` for the whole session, set
    ``pd.options.plotting.backend``.

    .. versionadded:: 1.0.0

**kwargs
    All other plotting keyword arguments to be passed to
    matplotlib's boxplot function.

Returns
-------
dict of key/value = group key/DataFrame.boxplot return value
or DataFrame.boxplot return value in case subplots=figures=False

Examples
--------
>>> import itertools
>>> tuples = [t for t in itertools.product(range(1000), range(4))]
>>> index = pd.MultiIndex.from_tuples(tuples, names=['lvl0', 'lvl1'])
>>> data = np.random.randn(len(index),4)
>>> df = pd.DataFrame(data, columns=list('ABCD'), index=index)
>>>
>>> grouped = df.groupby(level='lvl1')
>>> boxplot_frame_groupby(grouped)
>>>
>>> grouped = df.unstack(level='lvl1').groupby(level=0, axis=1)
>>> boxplot_frame_groupby(grouped, subplots=False)
"""
        pass
    # Overrides and others from original pylance stubs
    ## These are "properties" but properties can't have all these arguments?!
    def corr(self, method: Union[str, Callable], min_periods: int = ...) -> DataFrame: ...
    def cov(self, min_periods: int = ...) -> DataFrame: ...
    def diff(self, periods: int = ..., axis: AxisType = ...) -> DataFrame: ...


    def bfill(self, limit: Optional[int] = ...) -> DataFrame: ...

    def corrwith(self, other: DataFrame, axis: AxisType = ..., drop: bool = ..., method: str = ...,) -> Series:
        """Compute pairwise correlation.

Pairwise correlation is computed between rows or columns of
DataFrame with rows or columns of Series or DataFrame. DataFrames
are first aligned along both axes before computing the
correlations.

Parameters
----------
other : DataFrame, Series
    Object with which to compute correlations.
axis : {0 or 'index', 1 or 'columns'}, default 0
    The axis to use. 0 or 'index' to compute column-wise, 1 or 'columns' for
    row-wise.
drop : bool, default False
    Drop missing indices from result.
method : {'pearson', 'kendall', 'spearman'} or callable
    Method of correlation:

    * pearson : standard correlation coefficient
    * kendall : Kendall Tau correlation coefficient
    * spearman : Spearman rank correlation
    * callable: callable with input two 1d ndarrays
        and returning a float.

    .. versionadded:: 0.24.0

Returns
-------
Series
    Pairwise correlations.

See Also
--------
DataFrame.corr
"""
        pass

    def cummax(self, axis: AxisType = ..., **kwargs) -> DataFrame: ...
    def cummin(self, axis: AxisType = ..., **kwargs) -> DataFrame: ...
    def cumprod(self, axis: AxisType = ..., **kwargs) -> DataFrame: ...
    def cumsum(self, axis: AxisType = ..., **kwargs) -> DataFrame: ...
    def describe(self, **kwargs) -> DataFrame: ...
    def ffill(self, limit: Optional[int] = ...) -> DataFrame: ...
    @overload
    def fillna(
        self,
        value,
        method: Optional[str] = ...,
        axis: AxisType = ...,
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
axis : {0 or 'index', 1 or 'columns'}
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
DataFrame or None
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
        value,
        method: Optional[str] = ...,
        axis: AxisType = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
        *,
        inplace: Literal[False]
    ) -> DataFrame: ...
    @overload
    def fillna(
        self,
        value,
        method: Optional[str] = ...,
        axis: AxisType = ...,
        inplace: bool = ...,
        limit: Optional[int] = ...,
        downcast: Optional[Dict] = ...,
    ) -> Union[None, DataFrame]: ...

    def first(self, **kwargs) -> DataFrame: ...
    def head(self, n: int = ...) -> DataFrame: ...
    def hist(
        self,
        data: DataFrame,
        column: Optional[Union[str, Sequence]] = ...,
        by = ...,
        grid: bool = ...,
        xlabelsize: Optional[int] = ...,
        xrot: Optional[float] = ...,
        ylabelsize: Optional[int] = ...,
        yrot: Optional[float] = ...,
        ax: Optional[PlotAxes] = ...,
        sharex: bool = ...,
        sharey: bool = ...,
        figsize: Optional[Tuple[float, float]] = ...,
        layout: Optional[Tuple[int, int]] = ...,
        bins: Union[int, Sequence] = ...,
        backend: Optional[str] = ...,
        **kwargs
    ) -> Union[AxesSubplot, Sequence[AxesSubplot]]:
        """Make a histogram of the DataFrame's.

A `histogram`_ is a representation of the distribution of data.
This function calls :meth:`matplotlib.pyplot.hist`, on each series in
the DataFrame, resulting in one histogram per column.

.. _histogram: https://en.wikipedia.org/wiki/Histogram

Parameters
----------
data : DataFrame
    The pandas object holding the data.
column : str or sequence
    If passed, will be used to limit data to a subset of columns.
by : object, optional
    If passed, then used to form histograms for separate groups.
grid : bool, default True
    Whether to show axis grid lines.
xlabelsize : int, default None
    If specified changes the x-axis label size.
xrot : float, default None
    Rotation of x axis labels. For example, a value of 90 displays the
    x labels rotated 90 degrees clockwise.
ylabelsize : int, default None
    If specified changes the y-axis label size.
yrot : float, default None
    Rotation of y axis labels. For example, a value of 90 displays the
    y labels rotated 90 degrees clockwise.
ax : Matplotlib axes object, default None
    The axes to plot the histogram on.
sharex : bool, default True if ax is None else False
    In case subplots=True, share x axis and set some x axis labels to
    invisible; defaults to True if ax is None otherwise False if an ax
    is passed in.
    Note that passing in both an ax and sharex=True will alter all x axis
    labels for all subplots in a figure.
sharey : bool, default False
    In case subplots=True, share y axis and set some y axis labels to
    invisible.
figsize : tuple
    The size in inches of the figure to create. Uses the value in
    `matplotlib.rcParams` by default.
layout : tuple, optional
    Tuple of (rows, columns) for the layout of the histograms.
bins : int or sequence, default 10
    Number of histogram bins to be used. If an integer is given, bins + 1
    bin edges are calculated and returned. If bins is a sequence, gives
    bin edges, including left edge of first bin and right edge of last
    bin. In this case, bins is returned unmodified.
backend : str, default None
    Backend to use instead of the backend specified in the option
    ``plotting.backend``. For instance, 'matplotlib'. Alternatively, to
    specify the ``plotting.backend`` for the whole session, set
    ``pd.options.plotting.backend``.

    .. versionadded:: 1.0.0

**kwargs
    All other plotting keyword arguments to be passed to
    :meth:`matplotlib.pyplot.hist`.

Returns
-------
matplotlib.AxesSubplot or numpy.ndarray of them

See Also
--------
matplotlib.pyplot.hist : Plot a histogram using matplotlib.

Examples
--------

.. plot::
    :context: close-figs

    This example draws a histogram based on the length and width of
    some animals, displayed in three bins

    >>> df = pd.DataFrame({
    ...     'length': [1.5, 0.5, 1.2, 0.9, 3],
    ...     'width': [0.7, 0.2, 0.15, 0.2, 1.1]
    ...     }, index=['pig', 'rabbit', 'duck', 'chicken', 'horse'])
    >>> hist = df.hist(bins=3)
"""
        pass
    def idxmax(self, axis: AxisType = ..., skipna: bool = ...) -> Series:
        """Return index of first occurrence of maximum over requested axis.

NA/null values are excluded.

Parameters
----------
axis : {0 or 'index', 1 or 'columns'}, default 0
    The axis to use. 0 or 'index' for row-wise, 1 or 'columns' for column-wise.
skipna : bool, default True
    Exclude NA/null values. If an entire row/column is NA, the result
    will be NA.

Returns
-------
Series
    Indexes of maxima along the specified axis.

Raises
------
ValueError
    * If the row/column is empty

See Also
--------
Series.idxmax

Notes
-----
This method is the DataFrame version of ``ndarray.argmax``.
"""
        pass
    def idxmin(self, axis: AxisType = ..., skipna: bool = ...) -> Series:
        """Return index of first occurrence of minimum over requested axis.

NA/null values are excluded.

Parameters
----------
axis : {0 or 'index', 1 or 'columns'}, default 0
    The axis to use. 0 or 'index' for row-wise, 1 or 'columns' for column-wise.
skipna : bool, default True
    Exclude NA/null values. If an entire row/column is NA, the result
    will be NA.

Returns
-------
Series
    Indexes of minima along the specified axis.

Raises
------
ValueError
    * If the row/column is empty

See Also
--------
Series.idxmin

Notes
-----
This method is the DataFrame version of ``ndarray.argmin``.
"""
        pass
    def last(self, **kwargs) -> DataFrame: ...
    @overload
    def mad(
        self,
        axis: AxisType = ...,
        skipna: bool = ...,
        numeric_only: Optional[bool] = ...,
        *,
        level: Level,
        **kwargs
    ) -> DataFrame:
        """Return the mean absolute deviation of the values for the requested axis.

Parameters
----------
axis : {index (0), columns (1)}
    Axis for the function to be applied on.
skipna : bool, default True
    Exclude NA/null values when computing the result.
level : int or level name, default None
    If the axis is a MultiIndex (hierarchical), count along a
    particular level, collapsing into a Series.
numeric_only : bool, default None
    Include only float, int, boolean columns. If None, will attempt to use
    everything, then use only numeric data. Not implemented for Series.
**kwargs
    Additional keyword arguments to be passed to the function.

Returns
-------
Series or DataFrame (if level specified)
"""
        pass
    @overload
    def mad(
        self,
        axis: AxisType = ...,
        skipna: bool = ...,
        level: None = ...,
        numeric_only: Optional[bool] = ...,
        **kwargs
    ) -> Series: ...
    def max(self, **kwargs) -> DataFrame: ...
    def mean(self, **kwargs) -> DataFrame: ...
    def median(self, **kwargs) -> DataFrame: ...
    def min(self, **kwargs) -> DataFrame: ...
    def nth(self, n: Union[int, Sequence[int]], dropna: Optional[str] = ...) -> DataFrame: ...

    def pct_change(
        self, periods: int = ..., fill_method: str = ..., limit = ..., freq = ..., axis: AxisType = ...,
    ) -> DataFrame: ...
    def prod(self, **kwargs) -> DataFrame: ...
    def quantile(self, q: float = ..., interpolation: str = ...) -> DataFrame: ...
    def rank(
        self, method: str = ..., ascending: bool = ..., na_option: str = ..., pct: bool = ..., axis: AxisType = ...,
    ) -> DataFrame: ...
    def resample(self, rule, *args, **kwargs) -> Grouper: ...
    def sem(self, ddof: int = ...) -> DataFrame: ...
    def shift(
        self, periods: int = ..., freq: str = ..., axis: AxisType = ..., fill_value = ...,
    ) -> DataFrame: ...
    def size(self) -> Series[int]: ...
    @overload
    def skew(
        self, axis: AxisType = ..., skipna: bool = ..., numeric_only: bool = ..., *, level: Level, **kwargs
    ) -> DataFrame:
        """Return unbiased skew over requested axis.

Normalized by N-1.

Parameters
----------
axis : {index (0), columns (1)}
    Axis for the function to be applied on.
skipna : bool, default True
    Exclude NA/null values when computing the result.
level : int or level name, default None
    If the axis is a MultiIndex (hierarchical), count along a
    particular level, collapsing into a Series.
numeric_only : bool, default None
    Include only float, int, boolean columns. If None, will attempt to use
    everything, then use only numeric data. Not implemented for Series.
**kwargs
    Additional keyword arguments to be passed to the function.

Returns
-------
Series or DataFrame (if level specified)
"""
        pass
    @overload
    def skew(
        self, axis: AxisType = ..., skipna: bool = ..., level: None = ..., numeric_only: bool = ..., **kwargs
    ) -> Series: ...
    def std(self, ddof: int = ...) -> DataFrame: ...
    def sum(self, **kwargs) -> DataFrame: ...
    def tail(self, n: int = ...) -> DataFrame: ...
    def take(self, indices: Sequence, axis: AxisType = ..., **kwargs) -> DataFrame:
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
    def tshift(self, periods: int, freq = ..., axis: AxisType = ...) -> DataFrame: ...
    def var(self, ddof: int = ...) -> DataFrame: ...

