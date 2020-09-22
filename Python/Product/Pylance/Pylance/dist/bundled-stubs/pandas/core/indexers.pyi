import numpy as np
from typing import Optional, Tuple
from pandas._typing import AnyArrayLike as AnyArrayLike

def is_list_like_indexer(key) -> bool: ...
def is_scalar_indexer(indexer, arr_value) -> bool: ...
def is_empty_indexer(indexer, arr_value: np.ndarray) -> bool: ...
def check_setitem_lengths(indexer, value, values) -> None: ...
def validate_indices(indices: np.ndarray, n: int) -> None: ...
def maybe_convert_indices(indices, n: int) : ...
def length_of_indexer(indexer, target=...) -> int: ...
def deprecate_ndim_indexing(result) -> None: ...
def check_array_indexer(arrayArrayLike, indexer) : ...

class BaseIndexer:
    def __init__(
        self, index_array: Optional[np.ndarray] = ..., window_size: int = ..., **kwargs,
    ): ...
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class FixedWindowIndexer(BaseIndexer):
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class VariableWindowIndexer(BaseIndexer):
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class VariableOffsetWindowIndexer(BaseIndexer):
    def __init__(
        self,
        index_array: Optional[np.ndarray] = ...,
        window_size: int = ...,
        index=...,
        offset=...,
        **kwargs,
    ): ...
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class ExpandingIndexer(BaseIndexer):
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class FixedForwardWindowIndexer(BaseIndexer):
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...
