# coding: utf-8

from __future__ import print_function, absolute_import, division, unicode_literals

if False:  # MYPY
    from typing import Text, Any, Dict, List  # NOQA

__all__ = ["ScalarInt", "BinaryInt", "OctalInt", "HexInt", "HexCapsInt"]

from .compat import no_limit_int  # NOQA


class ScalarInt(no_limit_int):
    def __new__(cls, *args, **kw):
        # type: (Any, Any, Any) -> Any
        width = kw.pop('width', None)            # type: ignore
        underscore = kw.pop('underscore', None)  # type: ignore
        v = no_limit_int.__new__(cls, *args, **kw)        # type: ignore
        v._width = width
        v._underscore = underscore
        return v

    def __iadd__(self, a):  # type: ignore
        # type: (Any) -> Any
        x = type(self)(self + a)
        x._width = self._width  # type: ignore
        x._underscore = self._underscore[:] if self._underscore is not None else None  # type: ignore  # NOQA
        return x

    def __ifloordiv__(self, a):  # type: ignore
        # type: (Any) -> Any
        x = type(self)(self // a)
        x._width = self._width  # type: ignore
        x._underscore = self._underscore[:] if self._underscore is not None else None  # type: ignore  # NOQA
        return x

    def __imul__(self, a):  # type: ignore
        # type: (Any) -> Any
        x = type(self)(self * a)
        x._width = self._width  # type: ignore
        x._underscore = self._underscore[:] if self._underscore is not None else None  # type: ignore  # NOQA
        return x

    def __ipow__(self, a):  # type: ignore
        # type: (Any) -> Any
        x = type(self)(self ** a)
        x._width = self._width  # type: ignore
        x._underscore = self._underscore[:] if self._underscore is not None else None  # type: ignore  # NOQA
        return x

    def __isub__(self, a):  # type: ignore
        # type: (Any) -> Any
        x = type(self)(self - a)
        x._width = self._width  # type: ignore
        x._underscore = self._underscore[:] if self._underscore is not None else None  # type: ignore  # NOQA
        return x


class BinaryInt(ScalarInt):
    def __new__(cls, value, width=None, underscore=None):
        # type: (Any, Any, Any) -> Any
        return ScalarInt.__new__(cls, value, width=width, underscore=underscore)


class OctalInt(ScalarInt):
    def __new__(cls, value, width=None, underscore=None):
        # type: (Any, Any, Any) -> Any
        return ScalarInt.__new__(cls, value, width=width, underscore=underscore)


# mixed casing of A-F is not supported, when loading the first non digit
# determines the case

class HexInt(ScalarInt):
    """uses lower case (a-f)"""
    def __new__(cls, value, width=None, underscore=None):
        # type: (Any, Any, Any) -> Any
        return ScalarInt.__new__(cls, value, width=width, underscore=underscore)


class HexCapsInt(ScalarInt):
    """uses upper case (A-F)"""
    def __new__(cls, value, width=None, underscore=None):
        # type: (Any, Any, Any) -> Any
        return ScalarInt.__new__(cls, value, width=width, underscore=underscore)
