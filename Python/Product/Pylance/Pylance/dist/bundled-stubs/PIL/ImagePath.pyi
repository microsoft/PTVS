# pyright: strict

from typing import Any, Callable, List, Sequence, Tuple, Union


_XY = Union[Sequence[Tuple[int, int]], Sequence[int]]


class Path:
    def __init__(self, xy: _XY) -> None: ...

    def compact(self, distance: int = ...) -> int: ...

    def getbbox(self) -> Tuple[int, int, int, int]: ...

    def map(self, function: Callable) -> Any: ...

    # TODO: Is flat a boolean?
    # TODO: overloads based on flat
    def tolist(self, flat: int = ...) -> List[Union[Tuple[int, int], int]]: ...
