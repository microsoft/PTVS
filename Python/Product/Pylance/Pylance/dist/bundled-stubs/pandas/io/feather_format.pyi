#from pandas import DataFrame as DataFrame, Int64Index as Int64Index, RangeIndex as RangeIndex
from pandas.core.frame import DataFrame as DataFrame
from pandas._typing import FilePathOrBuffer
from typing import Optional, Sequence

def to_feather(df: DataFrame, path) : ...
def read_feather(p: FilePathOrBuffer, columns: Optional[Sequence] = ..., use_threads: bool = ...) : ...
