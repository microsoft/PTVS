from pandas._typing import FilePathOrBuffer
from typing import Optional, Sequence

def read_sas(
    path: FilePathOrBuffer,
    format: Optional[str] = ...,
    index: Optional[Sequence] = ...,
    encoding: Optional[str] = ...,
    chunksize: Optional[int] = ...,
    iterator: bool = ...,
) : ...
