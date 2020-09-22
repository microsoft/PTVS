# pyright: strict

from typing import Tuple, Union


def getrgb(color: str) -> Union[
    Tuple[int, int, int],     # (red, green, blue)
    Tuple[int, int, int, int] # (red, green, blue, alpha)
]: ...


def getcolor(color: str, mode: str) -> Union[
    Tuple[int],               # (graylevel)
    Tuple[int, int],          # (graylevel, alpha)
    Tuple[int, int, int],     # (red, green, blue)
    Tuple[int, int, int, int] # (red, green, blue, alpha)
]: ...
