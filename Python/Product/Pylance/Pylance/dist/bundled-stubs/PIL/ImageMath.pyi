# pyright: strict

from PIL.Image import Image
from typing import Any, Dict, Mapping, Tuple, Union


# TODO: Docs say eval(expression, environment), implementation looks as below.
def eval(expression: str, _dict: Dict[str, Image] = ..., **kw: Image) -> Any: ... # This function is a wrapper around builtins.eval, so could produce anything.
