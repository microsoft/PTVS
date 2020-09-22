from datetime import datetime
from pandas._typing import ArrayLike as ArrayLike
from pandas.core.dtypes.generic import ABCSeries as ABCSeries
from typing import Optional, TypeVar, Union

ArrayConvertible = Union[list, tuple, ArrayLike, ABCSeries]
Scalar = Union[int, float, str]
DatetimeScalar = TypeVar('DatetimeScalar', Scalar, datetime)
DatetimeScalarOrArrayConvertible = Union[DatetimeScalar, list, tuple, ArrayLike, ABCSeries]

def should_cache(arg: ArrayConvertible, unique_share: float=..., check_count: Optional[int]=...) -> bool: ...
def to_datetime(arg, errors: str = ..., dayfirst: bool = ..., yearfirst: bool = ..., utc = ..., format = ..., exact: bool = ..., unit = ..., infer_datetime_format: bool = ..., origin: str = ..., cache: bool = ...): ...
def to_time(arg, format = ..., infer_time_format: bool = ..., errors: str = ...): ...
