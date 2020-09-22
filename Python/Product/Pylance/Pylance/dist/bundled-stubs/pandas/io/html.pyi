from pandas._typing import FilePathOrBuffer as FilePathOrBuffer
from pandas.core.frame import DataFrame as DataFrame
from typing import Any, Callable, Iterable, List, Mapping, Optional, Sequence, Union

class _HtmlFrameParser:
    io = ...
    match = ...
    attrs = ...
    encoding = ...
    displayed_only = ...
    def __init__(self, io, match, attrs, encoding, displayed_only) -> None: ...
    def parse_tables(self) : ...

class _BeautifulSoupHtml5LibFrameParser(_HtmlFrameParser):
    def __init__(self, *args, **kwargs) -> None: ...

class _LxmlFrameParser(_HtmlFrameParser):
    def __init__(self, *args, **kwargs) -> None: ...

def read_html(
    io: FilePathOrBuffer,
    match: str = ...,
    flavor: Optional[str] = ...,
    header: Optional[Union[int, Sequence[int]]] = ...,
    index_col: Optional[Union[int, Sequence[Any]]] = ...,
    skiprows: Optional[Union[int, Sequence[Any], slice]] = ...,
    attrs: Optional[Mapping[str, str]] = ...,
    parse_dates: bool = ...,
    thousands: str = ...,
    encoding: Optional[str] = ...,
    decimal: str = ...,
    converters: Optional[Mapping[Union[int, str], Callable]] = ...,
    na_values: Optional[Iterable[Any]] = ...,
    keep_default_na: bool = ...,
    displayed_only: bool = ...,
) -> List[DataFrame]: ...
