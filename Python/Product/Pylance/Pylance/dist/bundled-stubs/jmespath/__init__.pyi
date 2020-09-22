# pyright: strict

from decimal import Decimal
from jmespath.parser import ParsedResult
from jmespath.visitor import Options
from typing import (
    Any,
    List,
    Optional,
    Union,
)

def compile(expression: str) -> ParsedResult: ...
def search(
    expression: str, data: Any, options: Optional[Options] = ...
) -> Union[List[str], str, List[Decimal], Decimal]: ...
