import abc
from pandas._typing import Scalar
from pandas.core.frame import DataFrame as DataFrame
from typing import Any, Callable, Dict, Optional, Sequence, Union, overload

@overload
def read_excel(
    filepath: str,
    sheet_name: Optional[Sequence[Union[int, str]]],
    header: Union[int, Sequence[int]] = ...,
    names: Optional[Sequence[str]] = ...,
    index_col: Optional[Union[int, Sequence[int]]] = ...,
    usecols: Optional[Union[int, str, Sequence[Union[int, str, Callable]]]] = ...,
    squeeze: bool = ...,
    dtype: Union[str, Dict[str, Any]] = ...,
    engine: Optional[str] = ...,
    converters: Optional[Dict[Union[int, str], Callable]] = ...,
    true_values: Optional[Sequence[Scalar]] = ...,
    false_values: Optional[Sequence[Scalar]] = ...,
    skiprows: Optional[Sequence[int]] = ...,
    nrows: Optional[int] = ...,
    na_values = ...,
    keep_default_na: bool = ...,
    verbose: bool = ...,
    parse_dates: Union[bool, Sequence, Dict[str, Sequence]] = ...,
    date_parser: Optional[Callable] = ...,
    thousands: Optional[str] = ...,
    comment: Optional[str] = ...,
    skipfooter: int = ...,
    convert_float: bool = ...,
    mangle_dupe_cols: bool = ...,
) -> Dict[Union[int, str], DataFrame]:
    """
Read an Excel file into a pandas DataFrame.

Supports `xls`, `xlsx`, `xlsm`, `xlsb`, and `odf` file extensions
read from a local filesystem or URL. Supports an option to read
a single sheet or a list of sheets.

Parameters
----------
io : str, bytes, ExcelFile, xlrd.Book, path object, or file-like object
    Any valid string path is acceptable. The string could be a URL. Valid
    URL schemes include http, ftp, s3, and file. For file URLs, a host is
    expected. A local file could be: ``file://localhost/path/to/table.xlsx``.

    If you want to pass in a path object, pandas accepts any ``os.PathLike``.

    By file-like object, we refer to objects with a ``read()`` method,
    such as a file handler (e.g. via builtin ``open`` function)
    or ``StringIO``.
sheet_name : str, int, list, or None, default 0
    Strings are used for sheet names. Integers are used in zero-indexed
    sheet positions. Lists of strings/integers are used to request
    multiple sheets. Specify None to get all sheets.

    Available cases:

    * Defaults to ``0``: 1st sheet as a `DataFrame`
    * ``1``: 2nd sheet as a `DataFrame`
    * ``"Sheet1"``: Load sheet with name "Sheet1"
    * ``[0, 1, "Sheet5"]``: Load first, second and sheet named "Sheet5"
      as a dict of `DataFrame`
    * None: All sheets.

header : int, list of int, default 0
    Row (0-indexed) to use for the column labels of the parsed
    DataFrame. If a list of integers is passed those row positions will
    be combined into a ``MultiIndex``. Use None if there is no header.
names : array-like, default None
    List of column names to use. If file contains no header row,
    then you should explicitly pass header=None.
index_col : int, list of int, default None
    Column (0-indexed) to use as the row labels of the DataFrame.
    Pass None if there is no such column.  If a list is passed,
    those columns will be combined into a ``MultiIndex``.  If a
    subset of data is selected with ``usecols``, index_col
    is based on the subset.
usecols : int, str, list-like, or callable default None
    * If None, then parse all columns.
    * If str, then indicates comma separated list of Excel column letters
      and column ranges (e.g. "A:E" or "A,C,E:F"). Ranges are inclusive of
      both sides.
    * If list of int, then indicates list of column numbers to be parsed.
    * If list of string, then indicates list of column names to be parsed.

      .. versionadded:: 0.24.0

    * If callable, then evaluate each column name against it and parse the
      column if the callable returns ``True``.

    Returns a subset of the columns according to behavior above.

      .. versionadded:: 0.24.0

squeeze : bool, default False
    If the parsed data only contains one column then return a Series.
dtype : Type name or dict of column -> type, default None
    Data type for data or columns. E.g. {'a': np.float64, 'b': np.int32}
    Use `object` to preserve data as stored in Excel and not interpret dtype.
    If converters are specified, they will be applied INSTEAD
    of dtype conversion.
engine : str, default None
    If io is not a buffer or path, this must be set to identify io.
    Acceptable values are None, "xlrd", "openpyxl" or "odf".
converters : dict, default None
    Dict of functions for converting values in certain columns. Keys can
    either be integers or column labels, values are functions that take one
    input argument, the Excel cell content, and return the transformed
    content.
true_values : list, default None
    Values to consider as True.
false_values : list, default None
    Values to consider as False.
skiprows : list-like
    Rows to skip at the beginning (0-indexed).
nrows : int, default None
    Number of rows to parse.

    .. versionadded:: 0.23.0

na_values : scalar, str, list-like, or dict, default None
    Additional strings to recognize as NA/NaN. If dict passed, specific
    per-column NA values. By default the following values are interpreted
    as NaN: '', '#N/A', '#N/A N/A', '#NA', '-1.#IND', '-1.#QNAN', '-NaN', '-nan',
    '1.#IND', '1.#QNAN', '<NA>', 'N/A', 'NA', 'NULL', 'NaN', 'n/a',
    'nan', 'null'.
keep_default_na : bool, default True
    Whether or not to include the default NaN values when parsing the data.
    Depending on whether `na_values` is passed in, the behavior is as follows:

    * If `keep_default_na` is True, and `na_values` are specified, `na_values`
      is appended to the default NaN values used for parsing.
    * If `keep_default_na` is True, and `na_values` are not specified, only
      the default NaN values are used for parsing.
    * If `keep_default_na` is False, and `na_values` are specified, only
      the NaN values specified `na_values` are used for parsing.
    * If `keep_default_na` is False, and `na_values` are not specified, no
      strings will be parsed as NaN.

    Note that if `na_filter` is passed in as False, the `keep_default_na` and
    `na_values` parameters will be ignored.
na_filter : bool, default True
    Detect missing value markers (empty strings and the value of na_values). In
    data without any NAs, passing na_filter=False can improve the performance
    of reading a large file.
verbose : bool, default False
    Indicate number of NA values placed in non-numeric columns.
parse_dates : bool, list-like, or dict, default False
    The behavior is as follows:

    * bool. If True -> try parsing the index.
    * list of int or names. e.g. If [1, 2, 3] -> try parsing columns 1, 2, 3
      each as a separate date column.
    * list of lists. e.g.  If [[1, 3]] -> combine columns 1 and 3 and parse as
      a single date column.
    * dict, e.g. {'foo' : [1, 3]} -> parse columns 1, 3 as date and call
      result 'foo'

    If a column or index contains an unparseable date, the entire column or
    index will be returned unaltered as an object data type. If you don`t want to
    parse some cells as date just change their type in Excel to "Text".
    For non-standard datetime parsing, use ``pd.to_datetime`` after ``pd.read_excel``.

    Note: A fast-path exists for iso8601-formatted dates.
date_parser : function, optional
    Function to use for converting a sequence of string columns to an array of
    datetime instances. The default uses ``dateutil.parser.parser`` to do the
    conversion. Pandas will try to call `date_parser` in three different ways,
    advancing to the next if an exception occurs: 1) Pass one or more arrays
    (as defined by `parse_dates`) as arguments; 2) concatenate (row-wise) the
    string values from the columns defined by `parse_dates` into a single array
    and pass that; and 3) call `date_parser` once for each row using one or
    more strings (corresponding to the columns defined by `parse_dates`) as
    arguments.
thousands : str, default None
    Thousands separator for parsing string columns to numeric.  Note that
    this parameter is only necessary for columns stored as TEXT in Excel,
    any numeric columns will automatically be parsed, regardless of display
    format.
comment : str, default None
    Comments out remainder of line. Pass a character or characters to this
    argument to indicate comments in the input file. Any data between the
    comment string and the end of the current line is ignored.
skipfooter : int, default 0
    Rows at the end to skip (0-indexed).
convert_float : bool, default True
    Convert integral floats to int (i.e., 1.0 --> 1). If False, all numeric
    data will be read in as floats: Excel stores all numbers as floats
    internally.
mangle_dupe_cols : bool, default True
    Duplicate columns will be specified as 'X', 'X.1', ...'X.N', rather than
    'X'...'X'. Passing in False will cause data to be overwritten if there
    are duplicate names in the columns.
**kwds : optional
        Optional keyword arguments can be passed to ``TextFileReader``.

Returns
-------
DataFrame or dict of DataFrames
    DataFrame from the passed in Excel file. See notes in sheet_name
    argument for more information on when a dict of DataFrames is returned.

See Also
--------
to_excel : Write DataFrame to an Excel file.
to_csv : Write DataFrame to a comma-separated values (csv) file.
read_csv : Read a comma-separated values (csv) file into DataFrame.
read_fwf : Read a table of fixed-width formatted lines into DataFrame.

Examples
--------
The file can be read using the file name as string or an open file object:

>>> pd.read_excel('tmp.xlsx', index_col=0)  # doctest: +SKIP
       Name  Value
0   string1      1
1   string2      2
2  #Comment      3

>>> pd.read_excel(open('tmp.xlsx', 'rb'),
...               sheet_name='Sheet3')  # doctest: +SKIP
   Unnamed: 0      Name  Value
0           0   string1      1
1           1   string2      2
2           2  #Comment      3

Index and header can be specified via the `index_col` and `header` arguments

>>> pd.read_excel('tmp.xlsx', index_col=None, header=None)  # doctest: +SKIP
     0         1      2
0  NaN      Name  Value
1  0.0   string1      1
2  1.0   string2      2
3  2.0  #Comment      3

Column types are inferred but can be explicitly specified

>>> pd.read_excel('tmp.xlsx', index_col=0,
...               dtype={'Name': str, 'Value': float})  # doctest: +SKIP
       Name  Value
0   string1    1.0
1   string2    2.0
2  #Comment    3.0

True, False, and NA values, and thousands separators have defaults,
but can be explicitly specified, too. Supply the values you would like
as strings or lists of strings!

>>> pd.read_excel('tmp.xlsx', index_col=0,
...               na_values=['string1', 'string2'])  # doctest: +SKIP
       Name  Value
0       NaN      1
1       NaN      2
2  #Comment      3

Comment lines in the excel input file can be skipped using the `comment` kwarg

>>> pd.read_excel('tmp.xlsx', index_col=0, comment='#')  # doctest: +SKIP
      Name  Value
0  string1    1.0
1  string2    2.0
2     None    NaN
"""
    pass

@overload
def read_excel(
    filepath: str,
    sheet_name: Union[int, str] = ...,
    header: Union[int, Sequence[int]] = ...,
    names: Optional[Sequence[str]] = ...,
    index_col: Optional[Union[int, Sequence[int]]] = ...,
    usecols: Optional[Union[int, str, Sequence[Union[int, str, Callable]]]] = ...,
    squeeze: bool = ...,
    dtype: Union[str, Dict[str, Any]] = ...,
    engine: Optional[str] = ...,
    converters: Optional[Dict[Union[int, str], Callable]] = ...,
    true_values: Optional[Sequence[Scalar]] = ...,
    false_values: Optional[Sequence[Scalar]] = ...,
    skiprows: Optional[Sequence[int]] = ...,
    nrows: Optional[int] = ...,
    na_values = ...,
    keep_default_na: bool = ...,
    verbose: bool = ...,
    parse_dates: Union[bool, Sequence, Dict[str, Sequence]] = ...,
    date_parser: Optional[Callable] = ...,
    thousands: Optional[str] = ...,
    comment: Optional[str] = ...,
    skipfooter: int = ...,
    convert_float: bool = ...,
    mangle_dupe_cols: bool = ...,
    **kwargs
) -> DataFrame: ...

class _BaseExcelReader(metaclass=abc.ABCMeta):
    book = ...
    def __init__(self, filepath_or_buffer) -> None: ...
    @abc.abstractmethod
    def load_workbook(self, filepath_or_buffer): ...
    def close(self) -> None: ...
    @property
    @abc.abstractmethod
    def sheet_names(self): ...
    @abc.abstractmethod
    def get_sheet_by_name(self, name): ...
    @abc.abstractmethod
    def get_sheet_by_index(self, index): ...
    @abc.abstractmethod
    def get_sheet_data(self, sheet, convert_float): ...
    def parse(self, sheet_name: int = ..., header: int = ..., names = ..., index_col = ..., usecols = ..., squeeze: bool = ..., dtype = ..., true_values = ..., false_values = ..., skiprows = ..., nrows = ..., na_values = ..., verbose: bool = ..., parse_dates: bool = ..., date_parser = ..., thousands = ..., comment = ..., skipfooter: int = ..., convert_float: bool = ..., mangle_dupe_cols: bool = ..., **kwds): ...

class ExcelWriter(metaclass=abc.ABCMeta):
    def __new__(cls, path, engine = ..., **kwargs): ...
    book = ...
    curr_sheet = ...
    path = ...
    @property
    @abc.abstractmethod
    def supported_extensions(self): ...
    @property
    @abc.abstractmethod
    def engine(self): ...
    @abc.abstractmethod
    def write_cells(self, cells, sheet_name = ..., startrow: int = ..., startcol: int = ..., freeze_panes = ...): ...
    @abc.abstractmethod
    def save(self): ...
    sheets = ...
    cur_sheet = ...
    date_format: str = ...
    datetime_format: str = ...
    mode = ...
    def __init__(self, path, engine = ..., date_format = ..., datetime_format = ..., mode: str = ..., **engine_kwargs) -> None: ...
    def __fspath__(self): ...
    @classmethod
    def check_extension(cls, ext): ...
    def __enter__(self): ...
    def __exit__(self, exc_type, exc_value, traceback) -> None: ...
    def close(self): ...

class ExcelFile:
    engine = ...
    io = ...
    def __init__(self, io, engine = ...) -> None: ...
    def __fspath__(self): ...
    def parse(self, sheet_name: int = ..., header: int = ..., names = ..., index_col = ..., usecols = ..., squeeze: bool = ..., converters = ..., true_values = ..., false_values = ..., skiprows = ..., nrows = ..., na_values = ..., parse_dates: bool = ..., date_parser = ..., thousands = ..., comment = ..., skipfooter: int = ..., convert_float: bool = ..., mangle_dupe_cols: bool = ..., **kwds): ...
    @property
    def book(self): ...
    @property
    def sheet_names(self): ...
    def close(self) -> None: ...
    def __enter__(self): ...
    def __exit__(self, exc_type, exc_value, traceback) -> None: ...
    def __del__(self) -> None: ...
