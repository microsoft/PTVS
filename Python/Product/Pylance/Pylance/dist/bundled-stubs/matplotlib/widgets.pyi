# pyright: strict

from typing import Any


class Widget:
    ...

class SubplotTool(Widget):
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
