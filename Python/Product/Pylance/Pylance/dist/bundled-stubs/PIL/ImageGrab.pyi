# pyright: strict

import sys
from typing import List, Optional, Tuple, Union

from PIL.Image import Image


def grab(bbox: Optional[Tuple[int, int, int, int]], include_layered_windows: bool = ..., all_screens: bool = ...) -> Image: ...

# TODO: Conditionally define this based on Windows/macOS.
def grabclipboard() -> Optional[Union[Image, List[str]]]: ...
