# pyright: strict

from typing import Any

from matplotlib._typing import ArrayLike


class TransformNode:
    ...

class BboxBase(TransformNode):
    ...

class Bbox(BboxBase):
    # TODO: Incomplete

    def __init__(self, points: ArrayLike, **kwargs: Any) -> None: ...

    @staticmethod
    def from_bounds(x0: int, y0: int, width: int, height: int) -> Bbox: ...



# INCOMPLETE
def __getattr__(name: str) -> Any: ...
