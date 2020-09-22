# pyright: strict

from typing import List, Optional, Tuple, Union

from PIL.Image import Image


class Stat:
    # These are implemented via __getattr__, but show them as regular members.
    extrema: List[Tuple[int, int]]
    count: List[int]
    sum: List[int]
    sum2: List[int]
    mean: List[float]
    median: List[int]
    rms: List[float]
    var: List[float]
    stddev: List[float]

    def __init__(self, image_or_list: Union[Image, List[int]], mask: Optional[Image] = ...) -> None: ...
