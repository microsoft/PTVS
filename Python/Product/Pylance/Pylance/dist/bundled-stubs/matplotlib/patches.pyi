# pyright: strict

from typing import Any

from matplotlib.artist import Artist


class Patch(Artist):
    ...

class Polygon(Patch):
    ...

class FancyArrow(Polygon):
    ...

class Wedge(Patch):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
