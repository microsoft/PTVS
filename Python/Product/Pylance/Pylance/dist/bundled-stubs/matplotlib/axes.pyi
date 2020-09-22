# pyright: strict

from typing import Any, Optional, Sequence, Union

from matplotlib.colors import _ColorLike
from matplotlib.figure import Figure
from matplotlib.transforms import Bbox


class Axes:
    # TODO: Most methods on Axes are the same as pyplot
    # (as pyplot is just global functions onto a shared Axes).
    # Mirror those here.

    # Actually from _AxesBase.
    def __init__(
        self,
        fig: Figure,
        rect: Union[Bbox, Sequence[int]],
        facecolor: Optional[_ColorLike] = ...,
        frameon: bool = ...,
        sharex: Optional[Axes] = ...,
        sharey: Optional[Axes] = ...,
        label: str = ...,
        xscale: Optional[str] = ...,
        yscale: Optional[str] = ...,
        box_aspect: Optional[float] = ...,
        **kwargs: Any
    ) -> None: ...


class SubplotBase:
    # TODO: write overloads for various forms
    def __init__(self, fig: Figure, *args: Any, **kwargs: Any) -> None: ...

# INCOMPLETE
def __getattr__(name: str) -> Any: ...
