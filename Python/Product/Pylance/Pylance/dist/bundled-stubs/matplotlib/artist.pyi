# pyright: strict

from typing import Any


class Artist:
    ...


# INCOMPLETE
def __getattr__(name: str) -> Any: ...
