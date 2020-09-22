from typing import List


__version__: str

_plugins: List[str]

class UnidentifiedImageError(IOError):
    pass
