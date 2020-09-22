# pyright: strict
# https://matplotlib.org/api/_as_gen/matplotlib.pyplot.html#module-matplotlib.pyplot

from datetime import tzinfo
from typing import (
    Any, BinaryIO, Callable, ContextManager, Dict, IO, List, Literal, Mapping,
    NewType, Optional, Sequence, Tuple, Type, Union, overload)

from matplotlib._typing import ArrayLike, Scalar, ndarray
from matplotlib.artist import Artist
from matplotlib.axes import Axes
from matplotlib.backend_bases import Event, FigureManagerBase
from matplotlib.cm import Colormap, ScalarMappable, SubplotBase
from matplotlib.collections import (
    BrokenBarHCollection, Collection, EventCollection, LineCollection,
    PathCollection, PolyCollection, QuadMesh)
from matplotlib.colorbar import Colorbar
from matplotlib.colors import Normalize, _ColorLike
from matplotlib.container import BarContainer, ErrorbarContainer, StemContainer
from matplotlib.contour import ContourSet, QuadContourSet
from matplotlib.figure import Figure
from matplotlib.image import AxesImage, FigureImage
from matplotlib.legend import Legend
from matplotlib.lines import Line2D
from matplotlib.markers import MarkerStyle
from matplotlib.patches import FancyArrow, Polygon, Wedge
from matplotlib.quiver import Barbs, Quiver, QuiverKey
from matplotlib.streamploy import StreamplotSet
from matplotlib.table import Table
from matplotlib.text import Annotation, Text
from matplotlib.transforms import Bbox
from matplotlib.widgets import SubplotTool
from PIL.Image import Image

# TODO: data params need to be Dicts/mappings?
# TODO: Are some of these more reasonable in tooltips when split out as overloads?

def acorr(x: ArrayLike, *, data: Optional[Any] = ..., **kwargs: Any) -> Tuple[ndarray, ndarray, Union[LineCollection, Line2D], Optional[Line2D]]: ...

def angle_spectrum(
    x: ArrayLike,
    Fs: Optional[Scalar] = ...,
    Fc: Optional[int] = ...,
    window: Optional[Union[Callable[[Any], Any], ndarray]] = ...,
    pad_to: Optional[int] = ...,
    sides: Optional[Literal["default", "onesides", "twosided"]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ArrayLike, ArrayLike, Line2D]: ...

def annotate(s: str, xy: Tuple[float, float], *args: Any, **kwargs: Any) -> Annotation: ...

def arrow(x: float, y: float, dx: float, dy: float, **kwargs: Any) -> FancyArrow: ...

def autoscale(enable: Optional[bool] = ..., axis: Optional[Literal["both", "x", "y"]] = ..., tight: Optional[bool] = ...) -> None: ...

def autumn() -> None: ...

def axes(arg: Optional[Tuple[float, float, float, float]] = ..., **kwargs: Any) -> Axes: ...

def axhline(y: Optional[Scalar] = ..., xmin: Optional[Scalar] = ..., xmax: Optional[Scalar] = ..., **kwargs: Any) -> Line2D: ...

def axhspan(ymin: float, ymax: float, xmin: Optional[int] = ..., xmax: Optional[int] = ..., **kwargs: Any) -> Polygon: ...

# TODO: write overloads for various forms
def axis(*args: Any, **kwargs: Any) -> Tuple[float, float, float, float]: ...

def axvline(x: Optional[Scalar] = ..., ymin: Optional[Scalar] = ..., ymax: Optional[Scalar] = ..., **kwargs: Any) -> Line2D: ...

def axvspan(xmin: Scalar, xmax: Scalar, ymin: Optional[Scalar] = ..., ymax: Optional[Scalar] = ..., **kwargs: Any) -> Polygon: ...

# Docs are misleading about this
def bar(
    x: Union[Scalar, ArrayLike],
    height: Union[Scalar, ArrayLike],
    width: Optional[Union[Scalar, ArrayLike]] = ...,
    bottom: Optional[Union[Scalar, ArrayLike]] = ...,
    *,
    align: Literal["center", "edge"] = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> BarContainer: ...

# TODO: write overloads for various forms
def barbs(*args: Any, data: Optional[Any] = ..., **kwargs: Any) -> Barbs: ...

# barh is just bar, but x=left and bottom=y
def barh(
    y: Union[Scalar, ArrayLike],
    width: Union[Scalar, ArrayLike],
    height: Optional[Union[Scalar, ArrayLike]] = ...,
    left: Optional[Union[Scalar, ArrayLike]] = ...,
    *,
    align: Literal["center", "edge"] = ...,
    **kwargs: Any
) -> BarContainer: ...

def bone() -> None: ...

def box(On: Optional[bool] = ...) -> None: ...

def boxplot(
    x: Union[ArrayLike, Sequence[ArrayLike]],
    notch: Optional[bool] = ...,
    sym: Optional[str] = ...,
    vert: Optional[bool] = ...,
    whis: Optional[Union[float, ArrayLike, str]] = ...,
    positions: Optional[ArrayLike] = ...,
    widths: Optional[Union[Scalar, ArrayLike]] = ...,
    patch_artist: Optional[bool] = ...,
    bootstrap: Optional[int] = ...,
    usermedians: Optional[ArrayLike] = ...,
    conf_intervals: Optional[ArrayLike] = ...,
    meanline: Optional[bool] = ...,
    showmeans: Optional[bool] = ...,
    showcaps: Optional[bool] = ...,
    showbox: Optional[bool] = ...,
    showfliers: Optional[bool] = ...,
    boxprops: Optional[Dict[Any, Any]] = ...,
    labels: Optional[Sequence[Any]] = ...,
    flierprops: Optional[Any] = ...,
    medianprops: Optional[Dict[Any, Any]] = ...,
    meanprops: Optional[Dict[Any, Any]] = ...,
    capprops: Optional[Dict[Any, Any]] = ...,
    whiskerprops: Optional[Dict[Any, Any]] = ...,
    manage_ticks: Optional[bool] = ...,
    autorange: Optional[bool] = ...,
    zorder: Optional[Scalar] = ...,
    *,
    data: Optional[Any] = ...
) -> Dict[str, Line2D]: ...

def broken_barh(xranges: Sequence[Tuple[float, float]], yrange: Tuple[float, float], *, data: Optional[Any] = ..., **kwargs: Any) -> BrokenBarHCollection: ...

def cla() -> None: ...

def clabel(CS: ContourSet, *args: Any, **kwargs: any) -> List[Text]: ...

def clf() -> None: ...

def clim(vmin: Optional[float] = ..., vmax: Optional[float] = ...) -> None: ...

def close(fig: Optional[Union[int, str, Figure]] = ...) -> None: ...

def cohere(
    x: ArrayLike, y: ArrayLike,
    NFFT: int = ...,
    Fs: Scalar = ...,
    Fc: int = ...,
    detrend: Union[Literal["none", "mean", "linear"], Callable] = ...,
    window: Union[Callable, ndarray] = ...,
    noverlap: int = ...,
    pad_to: Optional[int] = ...,
    sides: Literal["default", "onesided", "twosided"] = ...,
    scale_by_freq: Optional[bool] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ndarray, ndarray]: ... # ArrayLike?

def colorbar(mappable: Optional[ScalarMappable] = ..., cax: Optional[Axes] = ..., ax: Optional[Union[Axes, Sequence[Axes]]] = ..., **kwargs: Any) -> Colorbar: ...

def connect(s: str, func: Callable[[Event], None]) -> int: ...

# TODO: write overloads for various forms
def contour(*args: Any, data: Optional[Any] = ..., **kwargs: Any) -> QuadContourSet: ...
def contourf(*args: Any, data: Optional[Any] = ..., **kwargs: Any) -> QuadContourSet: ...

def cool() -> None: ...

def copper() -> None: ...

def csd(
    x: ArrayLike, y: ArrayLike,
    NFFT: int = ...,
    Fs: Scalar = ...,
    Fc: int = ...,
    detrend: Union[Literal["none", "mean", "linear"], Callable] = ...,
    window: Union[Callable, ndarray] = ...,
    noverlap: int = ...,
    pad_to: Optional[int] = ...,
    sides: Literal["default", "onesided", "twosided"] = ...,
    scale_by_freq: Optional[bool] = ...,
    return_line: Optional[bool] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ndarray, ndarray, Line2D]: ... # ArrayLike?

def delaxes(ax: Optional[Axes]) -> None: ...

def disconnect(cid: int) -> None: ...

def draw() -> None: ...

def errorbar(
    x: ArrayLike, y: ArrayLike,
    yerr: Optional[Union[Scalar, ArrayLike]] = ...,
    xerr: Optional[Union[Scalar, ArrayLike]] = ...,
    fmt: str = ...,
    ecolor: Optional[_ColorLike] = ...,
    elinewidth: Optional[Scalar] = ...,
    capsize: Optional[Scalar] = ...,
    barsabove: bool = ...,
    lolims: bool = ...,
    uplims: bool = ...,
    xlolims: bool = ...,
    xuplims: bool = ...,
    errorevery: int = ...,
    capthick: Optional[Scalar] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> ErrorbarContainer: ...

def eventplot(
    positions: ArrayLike,
    orientation: Optional[Literal["horizontal", "vertical"]],
    lineoffsets: Optional[Union[Scalar, ArrayLike]] = ...,
    linelengths: Optional[Union[Scalar, ArrayLike]] = ...,
    linewidths: Optional[Union[Scalar, ArrayLike]] = ...,
    colors: Optional[Union[_ColorLike, Sequence[_ColorLike]]] = ...,
    linestyles: Union[str, Tuple[str, ...], Sequence[Any]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> List[EventCollection]: ...

# TODO: write overloads for various forms
def figimage(*args: Any, **kwargs: Any) -> FigureImage: ...

# TODO: write overloads for various forms
def figlegend(*args: Any, **kwargs: Any) -> Legend: ...

def fignum_exists(num: Any) -> bool: ...

def figtext(x: float, y: float, s: str, *args: Any, **kwargs: Any) -> Text: ...

def figure(
    num: Optional[Union[int, str]] = ...,
    figsize: Optional[Tuple[float, float]] = ...,
    dpi: Optional[int] = ...,
    facecolor: Optional[_ColorLike] = ...,
    edgecolor: Optional[_ColorLike] = ...,
    frameon: bool = ...,
    FigureClass: Type[Figure] = ...,
    clear: bool = ...,
    **kwargs: Any
) -> Figure: ...

# TODO: write overloads for various forms
def fill(*args: Any, data: Optional[Mapping[Any, Any]] = ..., **kwargs: Any) -> List[Polygon]: ...

def fill_between(
    x: ArrayLike, y1: ArrayLike,
    y2: Union[ArrayLike, Scalar] = ...,
    where: Optional[ArrayLike] = ...,
    interpolate: bool = ...,
    step: Optional[Literal["pre", "post", "mid"]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> PolyCollection: ...

def fill_betweenx(
    y: ArrayLike, x1: ArrayLike,
    x2: Union[ArrayLike, Scalar] = ...,
    where: Optional[ArrayLike] = ...,
    interpolate: bool = ...,
    step: Optional[Literal["pre", "post", "mid"]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> PolyCollection: ...

def findobj(
    o: Optional[Any] = ...,
    match: Optional[Union[
        Callable[[Artist], bool],
        Line2D,
    ]] = ...,
    include_self: bool = ...
) -> List[Artist]: ...

def flag() -> None: ...

def gca(**kwargs: Any) -> Axes: ...

def gcf() -> Figure: ...

def gci() -> Optional[ScalarMappable]: ...

def get_current_fig_manager() -> FigureManagerBase: ...

def get_figlabels() -> List[Any]: ...

def get_fignums() -> List[Any]: ...

def get_plot_commands() -> List[str]: ...

# TODO: write overloads for various forms
def ginput(*args: Any, **kwargs: Any) -> List[Tuple[float, float]]: ...

def grat() -> None: ...

def grid(b: Optional[bool] = ..., which: Literal["major", "minor", "both"] = ..., axis: Literal["both", "x", "y"] = ..., **kwargs: Any): ...

def hexbin(
    x: ArrayLike, y: ArrayLike,
    C: Optional[ArrayLike] = ...,
    gridsize: Union[int, Tuple[int, int]] = ...,
    bins: Optional[Union[Literal["log"], int, Sequence[Any]]] = ...,
    xscale: Literal["linear", "log"] = ...,
    yscale: Literal["linear", "log"] = ...,
    extent: Optional[float] = ...,
    cmap: Optional[Union[str, Colormap]] = ...,
    norm: Optional[Normalize] = ...,
    vmin: Optional[float] = ...,
    vmax: Optional[float] = ...,
    alpha: Optional[float] = ...,
    linewidths: Optional[float] = ...,
    edgecolors: Optional[Union[Literal["face", "none"], _ColorLike]] = ...,
    reduce_C_function: Callable[[ArrayLike], float] = ...,
    mincnt: Optional[int] = ...,
    marginals: bool = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> PolyCollection: ...

def hist(
    x: Union[ArrayLike, Sequence[ArrayLike]],
    bins: Optional[Union[int, str, Sequence[Any]]],
    range: Optional[Tuple] = ...,
    density: Optional[bool] = ...,
    weights: Optional[ArrayLike] = ...,
    cumulative: bool = ...,
    bottom: Optional[Union[ArrayLike, Scalar]] = ...,
    histtype: Literal["bar", "barstacked", "step", "stepfilled"] = ...,
    align: Literal["left", "mid", "right"] = ...,
    orientation: Literal["vertical", "horizontal"] = ...,
    rwidth: Optional[Scalar] = ...,
    log: bool = ...,
    color: Optional[Union[_ColorLike, Sequence[_ColorLike]]] = ...,
    label: Optional[str] = ...,
    stacked: bool = ...,
    normed: Optional[bool] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[Union[ArrayLike, List[ArrayLike]], ArrayLike, Union[List[Any], List[List[Any]]]]: ...

def hist2d(
    x: ArrayLike, y: ArrayLike,
    bins: Optional[Union[
        int,
        Tuple[int, int],
        ArrayLike,
        Tuple[ArrayLike, ArrayLike],
    ]] = ...,
    range: Optional[ArrayLike] = ...,
    density: bool = ...,
    weights: Optional[ArrayLike] = ...,
    cmin: Optional[Scalar] = ...,
    cmax: Optional[Scalar] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ArrayLike, ArrayLike, ArrayLike, QuadMesh]: ...

def hlines(
    y: Union[Scalar, ArrayLike],
    xmin: Union[Scalar, ArrayLike],
    xmax: Union[Scalar, ArrayLike],
    colors: _ColorLike = ...,
    linestyles: Literal['solid', 'dashed', 'dashdot', 'dotted'] = ...,
    label: str = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> LineCollection: ...

def hot() -> None: ...

def hsv() -> None: ...

def imread(fname: Union[str, BinaryIO], format: Optional[str] = ...) -> ndarray: ...

def imsave(fname: Union[str, BinaryIO], arr: ArrayLike, **kwargs: Any) -> None: ...

def imshow(
    X: Union[ArrayLike, Image],
    cmap: Optional[Union[str, Colormap]] = ...,
    norm: Optional[Normalize] = ...,
    aspect: Optional[Union[Literal["equal", "auto"], float]] = ...,
    interpolation: Optional[str] = ...,
    alpha: Optional[Scalar] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    origin: Optional[Literal["upper", "lower"]] = ...,
    extent: Optional[Tuple[Scalar, Scalar, Scalar, Scalar]] = ...,
    shape: Any = ..., # deprecated
    filternorm: bool = ...,
    filterrad: float = ...,
    imlim: Any = ..., # deprecated
    resample: Optional[bool] = ...,
    url: Optional[str] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> AxesImage: ...

def inferno() -> None: ...

def install_repl_displayhook() -> None: ...

def ioff() -> None: ...

def ion() -> None: ...

def isinteractive() -> None: ...

def jet() -> None: ...

# TODO: write overloads for various forms
def legend(*args: Any, **kwargs: Any) -> Legend: ...

def locator_params(axis: Optional[Literal["both", "x", "y"]] = ..., tight: Optional[bool] = ..., **kwargs: Any) -> None: ...

# TODO: write overloads for various forms
def loglog(*args: Any, **kwargs: Any) -> List[Line2D]: ...

def magnitude_spectrum(
    x: ArrayLike,
    Fs: Optional[Scalar] = ...,
    Fc: Optional[int] = ...,
    window: Optional[Union[Callable[[Any], Any], ndarray]] = ...,
    pad_to: Optional[int] = ...,
    sides: Optional[Literal["default", "onesides", "twosided"]] = ...,
    scale: Optional[Literal["default", "linear", "dB"]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ArrayLike, ArrayLike, Line2D]: ...

def margins(*margins: float, x: Optional[float] = ..., y: Optional[float] = ..., tight: Optional[bool] = ...) -> Tuple[float, float]: ...

def matshow(A: ArrayLike, fignum: Optional[Union[int, Literal[False]]] = None, **kwargs: Any) -> AxesImage: ...

def minorticks_off() -> None: ...

def minorticks_on() -> None: ...

def nipy_spectral() -> None: ...

def pause(interval: int) -> None: ...

# TODO: write overloads for various forms
def pcolor(
    *args: Any,
    alpha: Optional[Scalar] = ...,
    norm: Optional[Normalize] = ...,
    cmap: Optional[Union[str, Colormap]] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Collection: ...

# TODO: write overloads for various forms
def pcolormesh(
    *args: Any,
    alpha: Optional[Scalar] = ...,
    norm: Optional[Normalize] = ...,
    cmap: Optional[Union[str, Colormap]] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    shading: Literal["flat", "gouraud"] = ...,
    antialiased: Union[bool, Sequence[bool]] = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> QuadMesh: ...

def phase_spectrum(
    x: ArrayLike,
    Fs: Optional[Scalar] = ...,
    Fc: Optional[int] = ...,
    window: Optional[Union[Callable[[Any], Any], ndarray]] = ...,
    pad_to: Optional[int] = ...,
    sides: Optional[Literal["default", "onesides", "twosided"]] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) ->  Tuple[ArrayLike, ArrayLike, Line2D]: ...

def pie(
    x: ArrayLike,
    explode: Optional[ArrayLike]= ...,
    labels: Optional[Sequence[str]] = ...,
    colors: Optional[Sequence[_ColorLike]] = ...,
    autopct: Optional[Union[str, Callable[..., str]]] = ...,
    pctdistance: float = ...,
    shadow: bool = ...,
    labeldistance: Optional[float] = ...,
    startangle: Optional[float] = ...,
    radius: Optional[float] = ...,
    counterclock: bool = ...,
    wedgeprops: Optional[Dict[Any, Any]] = ...,
    textprops: Optional[Dict[Any, Any]] = ...,
    center: Sequence[float] = ...,
    frame: bool = ...,
    rotatelabels: bool = ...,
    *,
    data: Optional[Any] = ...,
) -> Tuple[List[Wedge], List[Text], List[Text]]: ...

def pink() -> None: ...

def plasma() -> None: ...

# TODO: write overloads for various forms
def plot(*args: Any, scalex: bool = ..., scaley: bool = ..., data: Optional[Any] = ..., **kwargs: Any) -> List[Line2D]: ...

def plot_date(
    x: ArrayLike,
    y: ArrayLike,
    fmt: str = ...,
    tz: Optional[Union[str, tzinfo]] = ...,
    xdate: bool = ...,
    ydate: bool = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> List[Line2D]: ...

def plotfile(
    fname: str,
    cols: Sequence[Union[int, str]] = ...,
    plotfuncs: Mapping[Union[int, str], str] = ...,
    comments: Optional[str] = ...,
    skiprows: int = ...,
    checkrows: int = ...,
    delimiter: str = ...,
    names: Optional[Sequence[str]] = ...,
    subplots: bool = ...,
    newfig: bool = ...,
    **kwargs: Any
) -> None: ...

# TODO: write overloads for various forms
def polar(*args: Any, **kwargs: Any) -> None: ...

def prism() -> None: ...

def psd(
    x: ArrayLike,
    NFFT: int = ...,
    Fs: Scalar = ...,
    Fc: int = ...,
    detrend: Union[Literal["none", "mean", "linear"], Callable] = ...,
    window: Union[Callable, ndarray] = ...,
    noverlap: int = ...,
    pad_to: Optional[int] = ...,
    sides: Literal["default", "onesided", "twosided"] = ...,
    scale_by_freq: Optional[bool] = ...,
    return_line: Optional[bool] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ArrayLike, ArrayLike, Line2D]: ...

# TODO: write overloads for various forms
def quiver(*args: Any, data: Optional[Any] = ..., **kw: Any) -> Quiver: ...

def quiverkey(Q: Quiver, X: float, Y: float, U: float, label: str, **kw: Any) -> QuiverKey: ...

# Same as matplotlib.rc.
def rc(group: Union[str, Sequence[str]], **kwargs: Any) -> None: ...

# Same as matplotlib.rc_context.
def rc_context(rc: Optional[Mapping[Any, Any]] = ..., fname: Optional[str] = ...) -> ContextManager: ...

# Same as matplotlib.rcdefaults.
def rcdefaults() -> None: ...

# TODO: write overloads for various forms (below is a try at it)
@overload
def rgrids() -> Tuple[List[Line2D], List[Text]]: ...
@overload
def rgrids(radii: Tuple[float, ...], labels: Optional[Tuple[str, ...]] = ..., angle: float = ..., fmt: Optional[str] = ..., **kwargs: Any) -> Tuple[List[Line2D], List[Text]]: ...

# TODO: Need this when the above are present?
def rgrids(*args: Any, **kwargs: Any) -> Tuple[List[Line2D], List[Text]]: ...

# TODO: write overloads for various forms
def savefig(*args: Any, **kwargs: Any) -> None: ...

def sca(ax: Axes) -> None: ...

def scatter(
    x: ArrayLike,
    y: ArrayLike,
    s: Optional[Union[Scalar, ArrayLike]] = ...,
    c: Optional[Union[_ColorLike, Sequence[float], Sequence[_ColorLike]]] = ...,
    marker: Optional[MarkerStyle] = ...,
    cmap: Optional[Colormap] = ...,
    norm: Optional[Normalize] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    alpha: Optional[Scalar] = ...,
    linewidths: Optional[Union[Scalar, ArrayLike]] = ...,
    verts: Optional[Any] = ..., # not documented?
    edgecolors: Optional[Union[Literal["face", "none"], _ColorLike, Sequence[_ColorLike]]] = ...,
    *,
    plotnonfinite: bool = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> PathCollection: ...

# TODO: What is im supposed to be?
def sci(im: Any) -> None: ...

# TODO: write overloads for various forms (below is a try at it)
def semilogx(*args: Any, **kwargs: Any) -> List[Line2D]: ...

# TODO: write overloads for various forms (below is a try at it)
def semilogy(*args: Any, **kwargs: Any) -> List[Line2D]: ...

def set_cmap(cmap: Union[str, Colormap]) -> None: ...

def setp(obj: Artist, *args: Any, **kwargs: Any) -> None: ...

def show(*args: Any, **kw: Any) -> None: ...

def specgram(
    x: ArrayLike,
    NFFT: int = ...,
    Fs: Scalar = ...,
    Fc: int = ...,
    detrend: Union[Literal["none", "mean", "linear"], Callable] = ...,
    window: Union[Callable, ndarray] = ...,
    noverlap: int = ...,
    cmap: Optional[Colormap] = ...,
    xextent: Optional[Tuple[float, float]] = ...,
    pad_to: Optional[int] = ...,
    sides: Literal["default", "onesided", "twosided"] = ...,
    scale_by_freq: Optional[bool] = ...,
    mode: Optional[Literal["default", "psd", "magnitude", "angle", "phase"]] = ...,
    scale: Optional[Literal["default", "linear", "dB"]] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ndarray, ndarray, ndarray, AxesImage]: ...

def spring() -> None: ...

def spy(
    Z: ArrayLike,
    precision: Union[float, Literal["present"]] = ...,
    marker: Optional[Any] = ...,  # TODO
    markersize: Optional[float] = ...,
    aspect: Optional[Union[Literal["equal", "auto"], float]] = ...,
    origin: Literal["upper", "lower"] = ...,
    **kwargs: Any
) -> Union[AxesImage, Line2D]: ...

def stackplot(
    x: ArrayLike,
    *args: ArrayLike,
    labels: Sequence[str] = ...,
    colors: Optional[Sequence[_ColorLike]] = ...,
    baseline: Literal["zero", "sym", "wiggle", "weighted_wiggle"] = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> List[PolyCollection]: ...

# TODO: write overloads for various forms
def stem(
    *args: ArrayLike,
    linefmt: Optional[str] = ...,
    markerfmt: Optional[str] = ...,
    basefmt: Optional[str] = ...,
    bottom: float = ...,
    label: Optional[str] = ...,
    use_line_collection: bool = ...,
    data: Optional[Any] = ...
) -> StemContainer: ...

# TODO: write overloads for various forms
def step(
    x: ArrayLike,
    y: ArrayLike,
    *args: Any,
    where: Literal["pre", "post", "mid"] = ...,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> List[Line2D]: ...

def streamplot(
    x: ArrayLike,
    y: ArrayLike,
    u: ArrayLike,
    v: ArrayLike,
    density: Union[float, Tuple[float, float]] = ...,
    linewidth: Optional[Union[float, ArrayLike]] = ...,
    color: Optional[Union[_ColorLike, ArrayLike]] = ...,
    cmap: Optional[Colormap] = ...,
    norm: Optional[Normalize] = ...,
    arrowsize: float = ...,
    arrowstyle: str = ...,
    minlength: float = ...,
    transform: Optional[Any] = ..., # TODO: what is this?
    zorder: Optional[int] = ...,
    start_points: Optional[ArrayLike] = ...,
    maxlength: float = ...,
    integration_direction: Literal["forward", "backward", "both"] = ...,
    *,
    data: Optional[Any] = ...,
) -> StreamplotSet: ... # TODO: does this type exist?

# TODO: write overloads for various forms
def subplot(*args: Any, **kwargs: Any) -> SubplotBase: ...

def subplot2grid(shape: Sequence[int], loc: Sequence[int], rowspan: int = ..., colspan: int = ..., fig: Optional[Figure] = ..., **kwargs: Any) -> None: ...

def subplot_tool(targetfig: Optional[Figure]) -> SubplotTool: ...

def subplots(
    nrows: int = ...,
    ncols: int = ...,
    sharex: Union[bool, Literal["none", "all", "row", "col"]] = ...,
    sharey: Union[bool, Literal["none", "all", "row", "col"]] = ...,
    squeeze: bool = ...,
    subplot_kw: Optional[Dict[Any, Any]] = ...,
    gridspec_kw: Optional[Dict[Any, Any]] = ...,
    **fig_kw: Any
) -> Tuple[Figure, Axes]: ...

def subplots_adjust(left: Optional[float] = ..., bottom: Optional[float] = ..., right: Optional[float] = ..., top: Optional[float] = ..., wspace: Optional[float] = ..., hspace: Optional[float] = ...) -> None: ...

def summer() -> None: ...

def suptitle(t: str, **kwargs: Any) -> Text: ...

def switch_backend(newbackend: str) -> None: ...

# TODO: resolve list vs sequence
def table(
    cellText: Optional[Sequence[Sequence[str]]] = ...,
    cellColours: Optional[Sequence[Sequence[_ColorLike]]] = ...,
    cellLoc: Literal["left", "center", "right"] = ...,
    colWidths: Optional[Sequence[float]] = ...,
    rowLabels: Literal["left", "center", "right"] = ...,
    rowColours: Literal["left", "center", "right"] = ...,
    rowLoc: Literal["left", "center", "right"] = ...,
    colLabels: Literal["left", "center", "right"] = ...,
    colColours: Literal["left", "center", "right"] = ...,
    colLoc: Literal["left", "center", "right"] = ...,
    loc: str = ...,
    bbox: Optional[Bbox] = ...,
    edges: str = ..., # TODO: be more exact
    **kwargs: Any
) -> Table: ...

def text(x: Scalar, y: Scalar, s: str, fontdict: Dict[Any, Any] = ..., withdash: Any = ..., **kwargs: Any) -> Text: ...

# TODO: write overloads for various forms
def thetagrids(*args: Any, **kwargs: Any) -> Tuple[List[Line2D], List[Text]]: ...

def tick_params(axis: Literal["x", "y", "both"] = ..., **kwargs: Any) -> None: ...

def ticklabel_format(
    *,
    axis: Literal["x", "y", "both"] = ...,
    style: str = ...,
    scilimits: Optional[Tuple[int, int]] = ...,
    useOffset: Optional[Union[bool, int]] = ...,
    useLocale: Optional[bool] = ...,
    useMathText: Optional[bool] = ...,
) -> None: ...

def tight_layout(pad: float = ..., h_pad: Optional[float] = ..., w_pad: Optional[float] = ..., rect: Optional[Tuple[float, float, float, float]] = ...) -> None: ...

def title(label: str, fontdict: Optional[Dict[Any, Any]] = ..., loc: Literal["center", "left", "right"] = ..., pad: Optional[float] = ..., **kwargs: Any) -> Text: ...

# TODO: write overloads for various forms
def tricontour(*args: Any, **kwargs: Any) -> None: ...

# TODO: write overloads for various forms
def tricontourf(*args: Any, **kwargs: Any) -> None: ...

# TODO: write overloads for various forms
def tripcolor(
    *args: Any,
    alpha: Optional[Scalar] = ...,
    norm: Optional[Normalize] = ...,
    cmap: Optional[Union[str, Colormap]] = ...,
    vmin: Optional[Scalar] = ...,
    vmax: Optional[Scalar] = ...,
    shading: Literal["flat", "gouraud"] = ...,
    facecolors: Optional[_ColorLike] = ..., # TODO: not sure if this is correct, the option is undocumented
    **kwargs: Any
) -> None: ...

# TODO: write overloads for various forms
def triplot(*args: Any, **kwargs: Any) -> List[Line2D]: ...

def twinx(ax: Optional[Axes] = ...) -> Axes: ...
def twiny(ax: Optional[Axes] = ...) -> Axes: ...

def uninstall_repl_displayhook() -> None: ...

def violinplot(
    dataset: ArrayLike,
    positions: Optional[ArrayLike] = ...,
    vert: bool = ...,
    widths: ArrayLike = ..., # Default is 0.5, which is "array-like" even though it's a scalar.
    showmeans: bool = ...,
    showextrema: bool = ...,
    showmedians: bool = ...,
    points: Scalar = ...,
    bw_method: Optional[Union[Literal["scott", "silverman"], Scalar, Callable]] = ...,
    *,
    data: Optional[Any] = ...,
) -> Dict[str, Any]: ... # TODO: TypedDict for this

def viridis() -> None: ...

def vlines(
    x: Union[Scalar, ArrayLike],
    ymin: Union[Scalar, ArrayLike],
    ymax: Union[Scalar, ArrayLike],
    colors: Optional[Union[_ColorLike, Sequence[_ColorLike]]] = ..., # TODO: This may not be the right type for colors
    linestyles: Optional[Literal["solid", "dashed", "dashdot", "dotted"]] = ...,
    label: str = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> LineCollection: ...

def waitforbuttonpress(*args: Any, **kwargs: Any) -> None: ...

def winter() -> None: ...

def xcorr(
    x: ArrayLike,
    y: ArrayLike,
    normed: bool = ...,
    detrend: Callable = ...,
    usevlines: bool = ...,
    maxlags: int = ...,
    *,
    data: Optional[Any] = ...,
    **kwargs: Any
) -> Tuple[ndarray, ndarray, Union[LineCollection, Line2D], Optional[Line2D]]: ...

def xkcd(scale: float = ..., length: float = ..., randomness: float = ...) -> None: ...

def xlabel(xlabel: str, fontdict: Optional[Dict[Any, Any]] = ..., labelpad: Optional[Scalar] = ..., **kwargs: Any) -> None: ...

# TODO: write overloads for various forms
def xlim(*args: Any, **kwargs: Any) -> Tuple[float, float]: ...

def xscale(value: str, **kwargs: Any) -> None: ...

def xticks(ticks: Optional[ArrayLike] = ..., labels: Optional[ArrayLike] = ..., **kwargs: Any) -> Tuple[ArrayLike, List[Text]]: ... # TODO: What is "an array of label locations?

def ylabel(ylabel: str, fontdict: Optional[Dict[Any, Any]] = ..., labelpad: Optional[Scalar] = ..., **kwargs: Any) -> None: ...

# TODO: write overloads for various forms
def ylim(*args: Any, **kwargs: Any) -> Tuple[float, float]: ...

def yscale(value: str, **kwargs: Any) -> None: ...

def yticks(ticks: Optional[ArrayLike] = ..., labels: Optional[ArrayLike] = ..., **kwargs: Any) -> Tuple[ArrayLike, List[Text]]: ... # TODO: What is "an array of label locations?

# Should be colormap?
def colormaps() -> Dict[str, Colormap]: ...
