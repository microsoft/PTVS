# pyright: strict

from jmespath.functions import Functions
from typing import Any, Type, TypeVar, Mapping

_T = TypeVar("_T", bound=Functions)

class Options:
    def __init__(
        self, dict_cls: Type[Mapping[Any, Any]] = ..., custom_functions: _T = ...
    ) -> None: ...
