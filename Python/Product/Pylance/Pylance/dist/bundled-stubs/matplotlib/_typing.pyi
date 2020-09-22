# Internally defined types, which should be moved elsewhere.

# This should come from numpy.
from typing import Any, List, NewType, Sequence, Union


ndarray = NewType("ndarray", object)

# TODO: This should come from numpy, whatever np.isscalar() accepts.
Scalar = Union[
    int,
    float,
    bool,
    complex,
]
# Scalar = Any


# TODO: This should come from numpy, whatever np.array() accepts.
ArrayLike = Union[
    ndarray,
    List[Any],
    Sequence[Scalar],
    Scalar, # TODO: Should this be here?
]
# ArrayLike = Any
