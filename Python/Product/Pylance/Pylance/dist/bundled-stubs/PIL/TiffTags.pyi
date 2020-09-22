# pyright: strict

from typing import Dict, NamedTuple, Optional, Tuple


BYTE: int
ASCII: int
SHORT: int
LONG: int
RATIONAL: int
SIGNED_BYTE: int
UNDEFINED: int
SIGNED_SHORT: int
SIGNED_LONG: int
SIGNED_RATIONAL: int
FLOAT: int
DOUBLE: int

class TagInfo(NamedTuple):
    value: int
    name: str
    type: Optional[int]
    length: Optional[int]
    enum: Optional[Dict[str, int]]

    def cvt_enum(self, value: str) -> int: ...

TAGS_V2: Dict[int, TagInfo]
TAGS: Dict[int, str]
TYPES: Dict[int, str]
