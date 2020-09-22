# pyright: strict


from typing import Any
from matplotlib.artist import Artist
from matplotlib.cm import ScalarMappable


class Collection(Artist, ScalarMappable):
    ...

class _CollectionWithSizes(Collection):
    ...

class LineCollection(Collection):
    ...

class PolyCollection(_CollectionWithSizes):
    ...

class BrokenBarHCollection(PolyCollection):
    ...

class EventCollection(LineCollection):
    ...

class QuadMesh(Collection):
    ...

class PathCollection(_CollectionWithSizes):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
