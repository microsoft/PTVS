# pyright: strict

from typing import Any, List

from matplotlib.lines import Line2D


class Container(tuple):
    ...

class BarContainer(Container):
    ...

class ErrorbarContainer(Container):
    ...

class StemContainer(Container):
    # TODO: init
    markerline: Line2D
    stemlines: List[Line2D] # Not correct?
    baseline: Line2D


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
