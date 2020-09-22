# pyright: strict

import typing

from PIL.Image import Image


class Iterator(typing.Iterator[Image]):
    def __init__(self, im: Image) -> None: ...
    def __getitem__(self, i: int) -> Image: ...

