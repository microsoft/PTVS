# pyright: strict

from typing import Any

from matplotlib.artist import Artist


class Table(Artist):
    ...

# INCOMPLETE
def __getattr__(name: str) -> Any: ...
