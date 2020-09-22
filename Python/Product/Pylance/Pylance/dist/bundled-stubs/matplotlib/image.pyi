# pyright: strict


from typing import Any


class _ImageBase:
    ...

class FigureImage(_ImageBase):
    ...

class AxesImage(_ImageBase):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
