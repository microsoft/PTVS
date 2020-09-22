from pandas._typing import FilePathOrBuffer as FilePathOrBuffer
from pandas.core.frame import DataFrame as DataFrame
from typing import Optional, Sequence

def read_spss(
    path: FilePathOrBuffer, usecols: Optional[Sequence[str]] = ..., convert_categoricals: bool = ...,
) -> DataFrame: ...
