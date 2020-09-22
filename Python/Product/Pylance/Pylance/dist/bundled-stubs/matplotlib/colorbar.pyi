# pyright: strict

from typing import Any


class ColorbarBase:
    ...

class Colorbar(ColorbarBase):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
