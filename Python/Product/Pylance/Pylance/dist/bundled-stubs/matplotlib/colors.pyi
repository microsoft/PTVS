# pyright: strict

from typing import Any, Tuple, Union


_ColorLike = Union[
    str,
    Tuple[float, float, float],
    Tuple[float, float, float, float],
]

class Normalize:
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
