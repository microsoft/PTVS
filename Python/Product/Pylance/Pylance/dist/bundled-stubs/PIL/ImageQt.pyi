# pyright: strict

# TODO: Subclass of QtGui.QImage
from PIL.Image import Image


class ImageQt:
    def __init__(self, image: Image) -> None: ...
