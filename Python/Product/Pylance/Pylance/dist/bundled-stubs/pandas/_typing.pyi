import datetime
import numpy as np
import sys
from pathlib import Path
from typing import Any, AnyStr, Callable, Collection, Dict, Hashable, IO, List, Mapping, NewType, Optional, Sequence, Type, TypeVar, Union

from pandas.core.generic import NDFrame
from pandas._libs.tslibs import Period, Timedelta, Timestamp
from pandas.core.arrays import ExtensionArray
from pandas.core.series import Series
from pandas.core.indexes.base import Index
if sys.version_info >= (3, 8):
    from typing import Literal
else:
    from typing_extensions import Literal

AnyArrayLike = TypeVar('AnyArrayLike', ExtensionArray, Index, Series, np.ndarray)
ArrayLike = TypeVar('ArrayLike', ExtensionArray, np.ndarray)
PythonScalar = Union[str, int, float, bool, complex]
DatetimeLikeScalar = TypeVar('DatetimeLikeScalar', Period, Timestamp, Timedelta)
PandasScalar = Union[bytes, datetime.date, datetime.datetime, datetime.timedelta]
#Scalar = Union[PythonScalar, PandasScalar]
#Dtype: Any
FilePathOrBuffer = Union[str, Path, IO[AnyStr]]
FrameOrSeriesUnion: Any
FrameOrSeries = TypeVar('FrameOrSeries', bound=NDFrame)
Axis = Union[str, int]
Label = Optional[Hashable]
Level = Union[Label, int]
Ordered = Optional[bool]
JSONSerializable = Union[PythonScalar, List, Dict]
Axes = Collection
Renamer = Union[Mapping[Label, Any], Callable[[Label], Label]]
T = TypeVar('T')


num = Union[int, float]
SeriesAxisType = Union[str, int, Literal["index", 0]]  # Restricted subset of _AxisType for series
AxisType = Union[str, int, Literal["columns", "index", 0, 1]]
Dtype = TypeVar("Dtype", bool, int, float, object)
DtypeNp = TypeVar("DtypeNp", bound=np.dtype)
KeysArgType = Any
ListLike = TypeVar("ListLike", Sequence, np.ndarray, 'Series')
StrLike = Union[str, np.str_]
Scalar = Union[str, bytes, datetime.date, datetime.datetime, datetime.timedelta, bool, int, float, complex]
# Refine the next 3 in 3.9 to use the specialized type.
np_ndarray_int64 = NewType("np_ndarray_int64", Type[np.ndarray])
np_ndarray_bool = NewType("np_ndarray_bool", Type[np.ndarray])
np_ndarray_str = NewType("np_ndarray_str", Type[np.ndarray])
IndexType = Union[slice, np_ndarray_int64, Index[int], List[int], Series[int]]
MaskType = Union[Series[bool], np_ndarray_bool, Sequence[bool]]
# Scratch types for generics
S1 = TypeVar("S1", str, bytes, datetime.date, datetime.datetime, datetime.timedelta, bool, int, float, complex)
T1 = TypeVar("T1", str, int)
T2 = TypeVar("T2", str, int)


