# pyright: strict

from typing import Any, List, Literal, Optional, Sequence, Tuple, Union

from PIL.Image import Image
from PIL.ImageFont import ImageFont

_XY = Union[Sequence[Tuple[int, int]], Sequence[int]]
_Color = Any # TODO

def Draw(im: Image, mode: Optional[str] = ...) -> None: ...

class ImageDraw:
    def __init__(self, im: Image, mode: Optional[str] = ...) -> None: ...

    def getfont(self) -> ImageFont: ...

    def arc(self, xy: _XY, start: float, end: float, fill: Optional[_Color] = ..., width: int = ...): ...

    def bitmap(self, xy: _XY, bitmap: Image, fill: Optional[_Color] = ...) -> None: ...

    def chord(self, xy: _XY, start: float, end: float, fill: Optional[_Color] = ..., width: int = ...) -> None: ...

    def ellipse(self, xy: _XY, fill: Optional[_Color] = ..., outline: Optional[_Color] = ..., width: int = ...) -> None: ...

    def line(self, xy: _XY, fill: Optional[_Color], width: int = ..., joint: Optional[Literal["curve"]] = ...) -> None: ...

    def pieslice(self, xy: _XY, start: float, end: float, fill: Optional[_Color] = ..., outline: Optional[_Color] = ..., width: int = ...) -> None: ...

    def point(self, xy: _XY, fill: Optional[_Color] = ...) -> None: ...

    def polygon(self, xy: _XY, fill: Optional[_Color] = ..., outline: Optional[_Color] = ...) -> None: ...

    def rectangle(self, xy: _XY, fill: Optional[_Color] = ..., outline: Optional[_Color] = ..., width: int = ...) -> None: ...

    def shape(self, shape: Any, fill: Optional[_Color] = ..., outline: Optional[_Color] = ...) -> None: ...

    def text(
        self,
        xy: _XY,
        text: str,
        fill: Optional[_Color] = ...,
        font: Optional[ImageFont] = ...,
        anchor: Optional[Any] = ..., # Appears to be unused and undocumented.
        spacing: int = ...,
        align: Literal["left", "center", "right"] = ...,
        direction: Optional[Literal["rtl", "tlr", "ttb"]] = ...,
        features: Optional[List[str]] = ...,
        language: Optional[str] = ...,
        stroke_width: int = ...,
        stroke_fill: Optional[_Color] = ...
    ) -> None: ...

    def multiline_text(
        self,
        xy: _XY,
        text: str,
        fill: Optional[_Color] = ...,
        font: Optional[ImageFont] = ...,
        anchor: Optional[Any] = ..., # Appears to be unused and undocumented.
        spacing: int = ...,
        align: Literal["left", "center", "right"] = ...,
        direction: Optional[Literal["rtl", "tlr", "ttb"]] = ...,
        features: Optional[List[str]] = ...,
        language: Optional[str] = ...
    ) -> None: ...

    def textsize(
        self,
        text: str,
        font: Optional[ImageFont] = ...,
        spacing: int = ...,
        direction: Optional[Literal["rtl", "tlr", "ttb"]] = ...,
        features: Optional[List[str]] = ...,
        language: Optional[str] = ...,
        stroke_width: int = ...
    ) -> int: ...

    def multiline_textsize(
        self,
        text: str,
        font: Optional[ImageFont] = ...,
        spacing: int = ...,
        direction: Optional[Literal["rtl", "tlr", "ttb"]] = ...,
        features: Optional[List[str]] = ...,
        language: Optional[str] = ...,
        stroke_width: int = ...
    ) -> int: ...

# TODO: return types
def getdraw(im: Optional[Image] = ..., hints: Sequence[str] = ...) -> Tuple[Any, Any]: ...

def floodfill(image: Image, xy: _XY, value: _Color, border: Optional[_Color], thresh: int = ...) -> None: ...
