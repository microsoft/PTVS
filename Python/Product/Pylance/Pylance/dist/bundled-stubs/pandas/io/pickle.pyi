import sys
from pandas._typing import FilePathOrBuffer as FilePathOrBuffer
from typing import Optional, Union
if sys.version_info >= (3, 8):
    from typing import Literal
else:
    from typing_extensions import Literal

def to_pickle(obj, filepath_or_buffer: FilePathOrBuffer, compression: Optional[str]=..., protocol: int=...) : ...
def read_pickle(
    filepath_or_buffer_or_reader: FilePathOrBuffer,
    compression: Optional[Union[str, Literal["infer", "gzip", "bz2", "zip", "xz"]]] = ...,
) : ...
