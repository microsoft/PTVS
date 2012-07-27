from sys import winver
from sys import winver as baz
from sys.foo import winver
from sys.foo import winver as baz
from ...foo import bar
from ....foo import bar
from ......foo import bar
from .......foo import bar
from foo import (foo as bar, baz as quox)