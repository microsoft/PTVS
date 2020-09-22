import numpy as np
from typing import Dict, Optional, Tuple, Type, Union

get_window_bounds_doc: str

class BaseIndexer:
    index_array = ...
    window_size = ...
    def __init__(self, index_array: Optional[np.ndarray]=..., window_size: int=..., **kwargs) -> None: ...
    def get_window_bounds(self, num_values: int=..., min_periods: Optional[int]=...,
                          center: Optional[bool]=..., closed: Optional[str]=...) -> Tuple[np.ndarray, np.ndarray]: ...

class FixedWindowIndexer(BaseIndexer):
    def get_window_bounds(self, num_values: int=..., min_periods: Optional[int]=...,
                          center: Optional[bool]=..., closed: Optional[str]=...) -> Tuple[np.ndarray, np.ndarray]: ...

class VariableWindowIndexer(BaseIndexer):
    def get_window_bounds(self, num_values: int=..., min_periods: Optional[int]=...,
                          center: Optional[bool]=..., closed: Optional[str]=...) -> Tuple[np.ndarray, np.ndarray]: ...

class VariableOffsetWindowIndexer(BaseIndexer):
    def __init__(
        self,
        index_array: Optional[np.ndarray] = ...,
        window_size: int = ...,
        index=...,
        offset=...,
        **kwargs,
    ) -> None: ...

    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class ExpandingIndexer(BaseIndexer):
    def get_window_bounds(self, num_values: int=..., min_periods: Optional[int]=...,
                          center: Optional[bool]=..., closed: Optional[str]=...) -> Tuple[np.ndarray, np.ndarray]: ...


class FixedForwardWindowIndexer(BaseIndexer):
    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...

class GroupbyRollingIndexer(BaseIndexer):
    def __init__(
        self,
        index_array: Optional[np.ndarray],
        window_size: int,
        groupby_indicies: Dict,
        rolling_indexer: Union[Type[FixedWindowIndexer], Type[VariableWindowIndexer]],
        **kwargs,
    ) -> None: ...

    def get_window_bounds(
        self,
        num_values: int = ...,
        min_periods: Optional[int] = ...,
        center: Optional[bool] = ...,
        closed: Optional[str] = ...,
    ) -> Tuple[np.ndarray, np.ndarray]: ...