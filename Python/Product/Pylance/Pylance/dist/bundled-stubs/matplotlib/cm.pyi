# pyright: strict

from typing import Any


class ScalarMappable:
    ...

class Colormap:
    ...

class SubplotBase:
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
