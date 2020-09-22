from matplotlib.axes import Axes as PlotAxes
from matplotlib.pyplot import Figure
import numpy as np
from pandas.core.series import Series
from pandas.core.frame import DataFrame
from typing import Any, Dict, Optional, Sequence, Tuple, Union

def table(ax, data, rowLabels=None, colLabels=None,) : ... 
def register() -> None: ...
def deregister() -> None: ...
def scatter_matrix(
    frame: DataFrame,
    alpha: float = ...,
    figsize: Optional[Tuple[float, float]] = ...,
    ax: Optional[PlotAxes] = ...,
    grid: bool = ...,
    diagonal: str = ...,
    marker: str = ...,
    density_kwds = ...,
    hist_kwds = ...,
    range_padding: float = ...,
) -> np.ndarray: ...
def radviz(
    frame: DataFrame,
    class_column: str,
    ax: Optional[PlotAxes] = ...,
    color: Optional[Union[Sequence[str], Tuple[str]]] = ...,
    colormap = ...,
) -> PlotAxes: ...
def andrews_curves(
    frame: DataFrame,
    class_column: str,
    ax: Optional[PlotAxes] = ...,
    samples: int = ...,
    color: Optional[Union[Sequence[str], Tuple[str]]] = ...,
    colormap = ...,
) -> PlotAxes: ...
def bootstrap_plot(
    series: Series, fig: Optional[Figure] = ..., size: int = ..., samples: int = ...,
) -> Figure: ...
def parallel_coordinates(
    frame: DataFrame,
    class_column: str,
    cols: Optional[Sequence[str]] = ...,
    ax: Optional[PlotAxes] = ...,
    color: Optional[Union[Sequence[str], Tuple[str]]] = ...,
    use_columns: bool = ...,
    xticks: Optional[Union[Sequence, Tuple]] = ...,
    colormap = ...,
    axvlines: bool = ...,
    axvlines_kwds = ...,
    sort_labels: bool = ...,
) -> PlotAxes: ...
def lag_plot(series: Series, lag: int = ..., ax: Optional[PlotAxes] = ...,) -> PlotAxes: ...
def autocorrelation_plot(series: Series, ax: Optional[PlotAxes] = ...,) -> PlotAxes: ...

class _Options(dict):
    def __init__(self, deprecated: bool = ...) -> None: ...
    def __getitem__(self, key): ...
    def __setitem__(self, key, value): ...
    def __delitem__(self, key): ...
    def __contains__(self, key) -> bool: ...
    def reset(self) -> None: ...
    def use(self, key, value) -> None: ...

plot_params: Dict[str, Any]
