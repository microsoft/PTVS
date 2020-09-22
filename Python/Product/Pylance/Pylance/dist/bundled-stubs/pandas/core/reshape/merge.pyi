from pandas._libs.tslibs import Timedelta
from pandas import DataFrame as DataFrame, Series as Series
from pandas._typing import Label
from typing import Optional, Sequence, Union

def merge(left: DataFrame,
          right: Union[DataFrame, Series],
          how: str = ...,
          on: Optional[Label, Sequence] = ...,
          left_on: Optional[Label, Sequence] = ...,
          right_on: Optional[Label, Sequence] = ...,
          left_index: bool = ...,
          right_index: bool = ...,
          sort: bool = ...,
          suffixes: Sequence[Union[str, None]] = ...,
          copy: bool = ...,
          indicator: Union[bool, str] = ...,
          validate: str = ...) -> DataFrame:
    """Merge DataFrame or named Series objects with a database-style join.

The join is done on columns or indexes. If joining columns on
columns, the DataFrame indexes *will be ignored*. Otherwise if joining indexes
on indexes or indexes on a column or columns, the index will be passed on.

Parameters
----------
left : DataFrame
right : DataFrame or named Series
    Object to merge with.
how : {'left', 'right', 'outer', 'inner'}, default 'inner'
    Type of merge to be performed.

    * left: use only keys from left frame, similar to a SQL left outer join;
      preserve key order.
    * right: use only keys from right frame, similar to a SQL right outer join;
      preserve key order.
    * outer: use union of keys from both frames, similar to a SQL full outer
      join; sort keys lexicographically.
    * inner: use intersection of keys from both frames, similar to a SQL inner
      join; preserve the order of the left keys.
on : label or list
    Column or index level names to join on. These must be found in both
    DataFrames. If `on` is None and not merging on indexes then this defaults
    to the intersection of the columns in both DataFrames.
left_on : label or list, or array-like
    Column or index level names to join on in the left DataFrame. Can also
    be an array or list of arrays of the length of the left DataFrame.
    These arrays are treated as if they are columns.
right_on : label or list, or array-like
    Column or index level names to join on in the right DataFrame. Can also
    be an array or list of arrays of the length of the right DataFrame.
    These arrays are treated as if they are columns.
left_index : bool, default False
    Use the index from the left DataFrame as the join key(s). If it is a
    MultiIndex, the number of keys in the other DataFrame (either the index
    or a number of columns) must match the number of levels.
right_index : bool, default False
    Use the index from the right DataFrame as the join key. Same caveats as
    left_index.
sort : bool, default False
    Sort the join keys lexicographically in the result DataFrame. If False,
    the order of the join keys depends on the join type (how keyword).
suffixes : tuple of (str, str), default ('_x', '_y')
    Suffix to apply to overlapping column names in the left and right
    side, respectively. To raise an exception on overlapping columns use
    (False, False).
copy : bool, default True
    If False, avoid copy if possible.
indicator : bool or str, default False
    If True, adds a column to output DataFrame called "_merge" with
    information on the source of each row.
    If string, column with information on source of each row will be added to
    output DataFrame, and column will be named value of string.
    Information column is Categorical-type and takes on a value of "left_only"
    for observations whose merge key only appears in 'left' DataFrame,
    "right_only" for observations whose merge key only appears in 'right'
    DataFrame, and "both" if the observation's merge key is found in both.

validate : str, optional
    If specified, checks if merge is of specified type.

    * "one_to_one" or "1:1": check if merge keys are unique in both
      left and right datasets.
    * "one_to_many" or "1:m": check if merge keys are unique in left
      dataset.
    * "many_to_one" or "m:1": check if merge keys are unique in right
      dataset.
    * "many_to_many" or "m:m": allowed, but does not result in checks.

    .. versionadded:: 0.21.0

Returns
-------
DataFrame
    A DataFrame of the two merged objects.

See Also
--------
merge_ordered : Merge with optional filling/interpolation.
merge_asof : Merge on nearest keys.
DataFrame.join : Similar method using indices.

Notes
-----
Support for specifying index levels as the `on`, `left_on`, and
`right_on` parameters was added in version 0.23.0
Support for merging named Series objects was added in version 0.24.0

Examples
--------

>>> df1 = pd.DataFrame({'lkey': ['foo', 'bar', 'baz', 'foo'],
...                     'value': [1, 2, 3, 5]})
>>> df2 = pd.DataFrame({'rkey': ['foo', 'bar', 'baz', 'foo'],
...                     'value': [5, 6, 7, 8]})
>>> df1
    lkey value
0   foo      1
1   bar      2
2   baz      3
3   foo      5
>>> df2
    rkey value
0   foo      5
1   bar      6
2   baz      7
3   foo      8

Merge df1 and df2 on the lkey and rkey columns. The value columns have
the default suffixes, _x and _y, appended.

>>> df1.merge(df2, left_on='lkey', right_on='rkey')
  lkey  value_x rkey  value_y
0  foo        1  foo        5
1  foo        1  foo        8
2  foo        5  foo        5
3  foo        5  foo        8
4  bar        2  bar        6
5  baz        3  baz        7

Merge DataFrames df1 and df2 with specified left and right suffixes
appended to any overlapping columns.

>>> df1.merge(df2, left_on='lkey', right_on='rkey',
...           suffixes=('_left', '_right'))
  lkey  value_left rkey  value_right
0  foo           1  foo            5
1  foo           1  foo            8
2  foo           5  foo            5
3  foo           5  foo            8
4  bar           2  bar            6
5  baz           3  baz            7

Merge DataFrames df1 and df2, but raise an exception if the DataFrames have
any overlapping columns.

>>> df1.merge(df2, left_on='lkey', right_on='rkey', suffixes=(False, False))
Traceback (most recent call last):
...
ValueError: columns overlap but no suffix specified:
    Index(['value'], dtype='object')
"""
    pass

def merge_ordered(left: DataFrame,
                  right: Union[DataFrame, Series],
                  on: Optional[Label, Sequence] = ...,
                  left_on: Optional[Label, Sequence] = ...,
                  right_on: Optional[Label, Sequence] = ...,
                  left_by: Optional[Union[str, Sequence[str]]] = ...,
                  right_by: Optional[Union[str, Sequence[str]]] = ...,
                  fill_method: Optional[str] = ...,
                  suffixes: Sequence[Union[str, None]] = ...,
                  how: str = ...) -> DataFrame: ...

def merge_asof(
        left: DataFrame,
        right: DataFrame, Series,
        on: Optional[Label] = ...,
        left_on: Optional[Label] = ...,
        right_on: Optional[Label] = ...,
        left_index: bool = ...,
        right_index: bool = ...,
        by: Optional[Union[str, Sequence[str]]] = ...,
        left_by: Optional[str] = ...,
        right_by: Optional[str] = ...,
        suffixes: Sequence[Union[str, None]] = ...,
        tolerance: Optional[Union[int, Timedelta]] = ...,
        allow_exact_matches: bool = ...,
        direction: str = ...) -> DataFrame: ...

class _MergeOperation:
    left = ...
    right = ...
    how = ...
    axis = ...
    on = ...
    left_on = ...
    right_on = ...
    copy = ...
    suffixes = ...
    sort = ...
    left_index = ...
    right_index = ...
    indicator = ...
    indicator_name = ...
    def __init__(self, left: Union[Series, DataFrame], right: Union[Series, DataFrame], how: str=..., on=..., left_on=..., right_on=..., axis=..., left_index: bool=..., right_index: bool=..., sort: bool=..., suffixes=..., copy: bool=..., indicator: bool=..., validate=...) -> None: ...
    def get_result(self): ...

class _OrderedMerge(_MergeOperation):
    fill_method = ...
    def __init__(self, left, right, on=..., left_on=..., right_on=..., left_index: bool=..., right_index: bool=..., axis=..., suffixes=..., copy: bool=..., fill_method=..., how: str=...) -> None: ...
    def get_result(self): ...

class _AsOfMerge(_OrderedMerge):
    by = ...
    left_by = ...
    right_by = ...
    tolerance = ...
    allow_exact_matches = ...
    direction = ...
    def __init__(self, left, right, on=..., left_on=..., right_on=..., left_index: bool=..., right_index: bool=..., by=..., left_by=..., right_by=..., axis=..., suffixes=..., copy: bool=..., fill_method=..., how: str=..., tolerance=..., allow_exact_matches: bool=..., direction: str=...) -> None: ...
