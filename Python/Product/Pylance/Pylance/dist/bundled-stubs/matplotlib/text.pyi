# pyright: strict

from typing import Any

from matplotlib.artist import Artist


class Text(Artist):
    ...

class Annotation(Text):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
