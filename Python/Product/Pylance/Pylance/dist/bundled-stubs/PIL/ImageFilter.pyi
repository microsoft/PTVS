# pyright: strict
from typing import (
    Any, Callable, List, Optional, Sequence, Tuple, Type, TypeVar, Union)


_S = TypeVar("_S")

class Filter: ...


class MultibandFilter(Filter): ...


class BuiltinFilter(MultibandFilter): ...


class Color3DLUT(MultibandFilter):
    def __init__(
        self,
        size: Union[int, Tuple[int, int, int]],
        table: Union[List[float], List[Tuple[float, ...]]], # TODO: apparently accepts numpy
        channels: int = ...,
        target_mode: Optional[str] = ...,
        **kwargs: Any
    ) -> None: ...

    @classmethod
    def generate(
        cls: Type[_S],
        size: Union[int, Tuple[int, int, int]],
        callback: Callable[[float, float, float], Any],
        channels: int = ...,
        target_mode: Optional[str] = ...
    ) -> _S: ...

    def transform(
        self: _S,
        callback: Callable[[float, float, float], Any],
        with_normals: bool = ...,
        channels: Optional[int] = ...,
        target_mode: Optional[str] = ...
    ) -> _S: ...


class BoxBlur(MultibandFilter):
    def __init__(self, radius: float) -> None: ...


class GaussianBlur(MultibandFilter):
    def __init__(self, radius: float = ...) -> None: ...


class UnsharpMask(MultibandFilter):
    def __init__(self, radius: float = ..., percent: int = ..., threshold: int = ...) -> None: ...


class Kernel(BuiltinFilter):
    def __init__(self, size: Tuple[int, int], kernel: Sequence[float], scale: Optional[float] = ..., offset: float = ...) -> None: ...


class RankFilter(Filter):
    def __init__(self, size: int, rank: int) -> None: ...


class MedianFilter(RankFilter):
    def __init__(self, size: int = ...) -> None: ...


class MinFilter(RankFilter):
    def __init__(self, size: int = ...) -> None: ...


class MaxFilter(RankFilter):
    def __init__(self, size: int = ...) -> None: ...


class ModeFilter(RankFilter):
    def __init__(self, size: int = ...) -> None: ...


class BLUR(BuiltinFilter): ...
class CONTOUR(BuiltinFilter): ...
class DETAIL(BuiltinFilter): ...
class EDGE_ENHANCE(BuiltinFilter): ...
class EDGE_ENHANCE_MORE(BuiltinFilter): ...
class EMBOSS(BuiltinFilter): ...
class FIND_EDGES(BuiltinFilter): ...
class SHARPEN(BuiltinFilter): ...
class SMOOTH(BuiltinFilter): ...
class SMOOTH_MORE(BuiltinFilter): ...
