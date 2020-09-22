# pyright: strict


from typing import Any
from matplotlib.artist import Artist


class Figure(Artist):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
