# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

from __future__ import print_function

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2"

import inspect
import sys
import warnings

INCLUDE_DOCS = 1

# For scraping builtins, we use standardized names that come from
# the BuiltinTypeId enumeration. These are parsed and mapped to
# actual names within AstPythonInterpreter.

DIRECT_MAP = {
    object: "Object",
    type: "Type",
    tuple: "Tuple",
    int: "Int",
    str: "Str",
    None: "None",
    type(int.real): "Property",
    type(complex.real): "Property",
}

T = "type(self)()"
N = "None"
KNOWN_RESTYPES = {
    "__abs__": T,
    "__add__": T,
    "__and__": T,
    "__base__": "Type()",
    "__bases__": "(Type(),)",
    "__bool__": "Bool()",
    "Type.__call__": "cls()",
    "__ceil__": T,
    "__contains__": "Bool()",
    "__delattr__": N,
    "__delitem__": N,
    "__dict__": "{Str(): Unknown()}",
    "__dir__": "[Str()]",
    "__divmod__": "(Int(), Int())",
    "__eq__": "Bool()",
    "__format__": "Str()",
    "__float__": "Float()",
    "__floor__": T,
    "__floordiv__": "Int()",
    "__ge__": "Bool()",
    "__getattribute__": "Unknown()",
    "Float.__getformat__": "Str()",
    "__getitem__": "Unknown()",
    "__getnewargs__": "()",
    "__getnewargs_ex__": "((), Dict())",
    "__gt__": "Bool()",
    "__hash__": "Int()",
    "__iadd__": N,
    "__iand__": N,
    "__imul__": N,
    "__index__": "Int()",
    "__init__": T,
    "__init_subclass__": N,
    "__int__": "Int()",
    "Type.__instancecheck__": "Bool()",
    "__invert__": T,
    "__ior__": N,
    "__isub__": N,
    "Tuple.__iter__": "TupleIterator()",
    "List.__iter__": "ListIterator()",
    "Dict.__iter__": "DictKeys()",
    "Set.__iter__": "SetIterator()",
    "FrozenSet.__iter__": "SetIterator()",
    "Bytes.__iter__": "BytesIterator()",
    "Unicode.__iter__": "UnicodeIterator()",
    "__ixor__": N,
    "__le__": "Bool()",
    "__len__": "Int()",
    "__lshift__": T,
    "__lt__": "Bool()",
    "__mod__": T,
    "__mul__": T,
    "__ne__": "Bool()",
    "__neg__": T,
    "__new__": "cls()",
    "Type.__prepare__": N,
    "__pos__": T,
    "__pow__": T,
    "__or__": T,
    "__radd__": T,
    "__rand__": T,
    "__rdivmod__": "(Int(), Int())",
    "List.__reversed__": "ListIterator()",
    "__rfloordiv__": T,
    "__rlshift__": T,
    "__rmod__": T,
    "__rmul__": T,
    "__ror__": T,
    "__round__": T,
    "__rpow__": T,
    "__rrshift__": T,
    "__rshift__": T,
    "__rsub__": T,
    "__rtruediv__": T,
    "__rxor__": T,
    "__reduce__": ["''", "()"],
    "__reduce_ex__": ["''", "()"],
    "__repr__": "''",
    "__setattr__": N,
    "Float.__setformat__": N,
    "__setitem__": N,
    "__sizeof__": "Int()",
    "__str__": "''",
    "Type.__subclasses__": "(cls,)",
    "__sub__": T,
    "__truediv__": "Float()",
    "__trunc__": T,
    "__xor__": T,
    "Type.__subclasscheck__": "Bool()",
    "__subclasshook__": "Bool()",
    "Set.add": N,
    "List.append": N,
    "Float.as_integer_ratio": "(Int(), Int())",
    "Int.bit_length": "Int()",
    "capitalize": T,
    "casefold": T,
    "center": T,
    "clear": N,
    "conjugate": "Complex()",
    "copy": T,
    "count": "Int()",
    "Bytes.decode": "''",
    "Bytes.encode": "b''",
    "Unicode.encode": "b''",
    "Set.difference": T,
    "FrozenSet.difference": T,
    "Set.difference_update": N,
    "Set.discard": N,
    "endswith": "Bool()",
    "expandtabs": T,
    "List.extend": N,
    "find": "Int()",
    "Unicode.format": T,
    "Unicode.format_map": T,
    "Bool.from_bytes": "Bool()",
    "Int.from_bytes": "Int()",
    "Long.from_bytes": "Long()",
    "Float.fromhex": "Float()",
    "Bytes.fromhex": "b''",
    "Dict.fromkeys": "{}",
    "Dict.get": "self.__getitem__()",
    "hex": "''",
    "index": "Int()",
    "List.insert": N,
    "Set.intersection": T,
    "FrozenSet.intersection": T,
    "Set.intersection_update": N,
    "isalnum": "Bool()",
    "isalpha": "Bool()",
    "isdecimal": "Bool()",
    "isdigit": "Bool()",
    "islower": "Bool()",
    "isidentifier": "Bool()",
    "isnumeric": "Bool()",
    "isprintable": "Bool()",
    "isspace": "Bool()",
    "istitle": "Bool()",
    "isupper": "Bool()",
    "Float.is_integer": "Bool()",
    "Set.isdisjoint": "Bool()",
    "FrozenSet.isdisjoint": "Bool()",
    "Set.issubset": "Bool()",
    "FrozenSet.issubset": "Bool()",
    "Set.issuperset": "Bool()",
    "FrozenSet.issuperset": "Bool()",
    "Dict.items": "DictItems()",
    "Bytes.join": "b''",
    "Unicode.join": "''",
    "Dict.keys": "DictKeys()",
    "lower": T,
    "ljust": T,
    "lstrip": T,
    "Bytes.maketrans": "b''",
    "Unicode.maketrans": "{}",
    "Type.mro": "[Type()]",
    "partition": "(type(self)(), type(self)(), type(self)())",
    "List.pop": "self.__getitem__()",
    "Dict.pop": "self.keys().__getitem__()",
    "Set.pop": "Unknown()",
    "Dict.popitem": "self.items().__getitem__()",
    "remove": N,
    "replace": T,
    "rfind": "Int()",
    "List.reverse": N,
    "rindex": "Int()",
    "rjust": T,
    "rpartition": "(type(self)(), type(self)(), type(self)())",
    "rsplit": "[type(self)()]",
    "rstrip": T,
    "Dict.setdefault": "self.__getitem__()",
    "List.sort": N,
    "split": "[type(self)()]",
    "splitlines": "[self()]",
    "startswith": "Bool()",
    "strip": T,
    "swapcase": T,
    "Set.symmetric_difference": T,
    "FrozenSet.symmetric_difference": T,
    "Set.symmetric_difference_update": N,
    "Bytes.translate": T,
    "Unicode.translate": T,
    "title": T,
    "to_bytes": "b''",
    "Set.union": T,
    "FrozenSet.union": T,
    "Dict.update": N,
    "Set.update": N,
    "upper": T,
    "Dict.values": "DictValues()",
    "zfill": T,
}

SELF = "(self)"
KNOWN_ARGSPECS = {
    "Type.__call__": "(cls, *args, **kwargs)",
    "Int.__ceil__": SELF,
    "__contains__": "(self, value)",
    "__dir__": SELF,
    "Int.__floor__": SELF,
    "__format__": "(self, format_spec)",
    "Float.__getformat__": "(typestr)",
    "__getitem__": "(self, index)",
    "Dict.__getitem__": "(self, key)",
    "__getnewargs__": SELF,
    "__getnewargs_ex__": SELF,
    "__init_subclass__": "(cls)",
    "Type.__instancecheck__": "(self, instance)",
    "Bool.__init__": "(self, x)",
    "Int.__init__": "(self, x=0)",
    "__new__": "(cls, *args, **kwargs)",
    "Type.__prepare__": "(cls, name, bases, **kwds)",
    "Int.__round__": "(self, ndigits=0)",
    "Float.__round__": "(self, ndigits=0)",
    "__reduce__": SELF,
    "__reduce_ex__": "(self, protocol)",
    "List.__reversed__": SELF,
    "Float.__setformat__": "(typestr, fmt)",
    "List.__setitem__": "(self, index, value)",
    "Dict.__setitem__": "(self, key, value)",
    "__sizeof__": SELF,
    "Type.__subclasses__": "(cls)",
    "Type.__subclasscheck__": "(cls, subclass)",
    "__subclasshook__": "(cls, subclass)",
    "__trunc__": SELF,
    "Set.add": "(self, value)",
    "List.append": "(self, value)",
    "Float.as_integer_ratio": SELF,
    "Int.bit_length": SELF,
    "capitalize": SELF,
    "casefold": SELF,
    "Bytes.center": ["(self, width)", "(self, width, fillbyte)"],
    "Unicode.center": ["(self, width)", "(self, width, fillchar)"],
    "clear": SELF,
    "conjugate": SELF,
    "copy": SELF,
    "count": "(self, x)",
    "Bytes.count": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Unicode.count": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Bytes.decode": "(self, encoding='utf-8', errors='strict')",
    "Unicode.encode": "(self, encoding='utf-8', errors='strict')",
    "Set.difference": "(self, other)",
    "FrozenSet.difference": "(self, other)",
    "Set.difference_update": "(self, *others)",
    "Set.discard": "(self, elem)",
    "endswith": ["(self, suffix)", "(self, suffix, start)", "(self, suffix, start, end)"],
    "expandtabs": "(self, tabsize=8)",
    "List.extend": "(self, iterable)",
    "find": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Unicode.format": "(self, *args, **kwargs)",
    "Unicode.format_map": "(self, mapping)",
    "Bool.from_bytes": "(bytes, byteorder, *, signed=False)",
    "Int.from_bytes": "(bytes, byteorder, *, signed=False)",
    "Float.fromhex": "(string)",
    "Dict.get": "(self, key, d=Unknown())",
    "hex": SELF,
    "List.insert": "(self, index, value)",
    "index": "(self, v)",
    "Bytes.index": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Unicode.index": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Set.intersection": "(self, other)",
    "FrozenSet.intersection": "(self, other)",
    "Set.intersection_update": "(self, *others)",
    "isalnum": SELF,
    "isalpha": SELF,
    "isdecimal": SELF,
    "isdigit": SELF,
    "isidentifier": SELF,
    "islower": SELF,
    "isnumeric": SELF,
    "isprintable": SELF,
    "isspace": SELF,
    "istitle": SELF,
    "isupper": SELF,
    "Float.is_integer": SELF,
    "Set.isdisjoint": "(self, other)",
    "FrozenSet.isdisjoint": "(self, other)",
    "Set.issubset": "(self, other)",
    "FrozenSet.issubset": "(self, other)",
    "Set.issuperset": "(self, other)",
    "FrozenSet.issuperset": "(self, other)",
    "Dict.items": SELF,
    "Bytes.join": "(self, iterable)",
    "Unicode.join": "(self, iterable)",
    "Dict.keys": SELF,
    "lower": SELF,
    "Bytes.ljust": ["(self, width)", "(self, width, fillbyte)"],
    "Unicode.ljust": ["(self, width)", "(self, width, fillchar)"],
    "lstrip": ["(self)", "(self, chars)"],
    "Bytes.maketrans": "(from_, to)",
    "Unicode.maketrans": ["(x)", "(x, y)", "(x, y, z)"],
    "Type.mro": "(cls)",
    "Bytes.partition": "(self, sep)",
    "Unicode.partition": "(self, sep)",
    "List.pop": "(self, index=-1)",
    "Dict.pop": "(self, k, d=Unknown())",
    "Set.pop": SELF,
    "Dict.popitem": "(self, k, d=Unknown())",
    "List.remove": "(self, value)",
    "Set.remove": "(self, elem)",
    "replace": ["(self, old, new)", "(self, old, new, count)"],
    "List.reverse": SELF,
    "rfind": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "rindex": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "Bytes.rjust": ["(self, width)", "(self, width, fillbyte)"],
    "Unicode.rjust": ["(self, width)", "(self, width, fillchar)"],
    "Bytes.rpartition": "(self, sep)",
    "Unicode.rpartition": "(self, sep)",
    "rsplit": "(self, sep=None, maxsplit=-1)",
    "rstrip": ["(self)", "(self, chars)"],
    "Dict.setdefault": "(self, k, d)",
    "List.sort": SELF,
    "split": "(self, sep=None, maxsplit=-1)",
    "splitlines": "(self, keepends=False)",
    "strip": ["(self)", "(self, chars)"],
    "startswith": ["(self, prefix)", "(self, prefix, start)", "(self, prefix, start, end)"],
    "swapcase": SELF,
    "Set.symmetric_difference": "(self, other)",
    "FrozenSet.symmetric_difference": "(self, other)",
    "Set.symmetric_difference_update": "(self, *others)",
    "title": SELF,
    "Int.to_bytes": "(bytes, byteorder, *, signed=False)",
    "Bytes.translate": "(self, table, delete=b'')",
    "Unicode.translate": "(self, table)",
    "Set.union": "(self, *others)",
    "FrozenSet.union": "(self, *others)",
    "Dict.update": "(self, d)",
    "Set.update": "(self, *others)",
    "upper": SELF,
    "Dict.values": SELF,
    "zfill": "(self, width)",
}

class InspectWarning(UserWarning): pass

def _get_restype(typename, membername):
    return (KNOWN_RESTYPES.get(typename + '.' + membername) or
            KNOWN_RESTYPES.get(membername) or
            None)

def _render_args(member, typename, membername):
    try:
        return KNOWN_ARGSPECS[typename + "." + membername]
    except LookupError:
        try:
            return KNOWN_ARGSPECS[membername]
        except LookupError:
            pass
    try:
        return inspect.formatargspec(*inspect.getargspec(member))
    except:
        warnings.warn("failed to getargspec for " + repr(member), InspectWarning)
        return "(self)"

def _triple_quote(s):
    if "'" not in s:
        return "'''" + s + "'''"
    if '"' not in s:
        return '"""' + s + '"""'
    if not s.startswith("'"):
        return "'''" + s.replace("'''", "\\'\\'\\'") + " '''"
    if not s.startswith('"'):
        return '"""' + s.replace('"""', '\\"\\"\\"') + ' """'
    return "''' " + s.replace("'''", "\\'\\'\\'") + " '''"

_SKIP_MEMBERS = ["__doc__"]

def _each(s, name):
    if s == "T":
        return [name]
    if s == "T()":
        return [name + "()"]
    if isinstance(s, str):
        return [s]
    if hasattr(s, '__iter__'):
        res = []
        for i in s:
            res.extend(_each(i, name))
        return res
    return [s]

def _print_type(name, obj, basename=None, base=None):
    if base:
        base_members = list(dir(base))
        base_members.extend(getattr(base, "__dict__", {}).keys())
        base_members.sort()

        print("class " + name + "(" + basename + "):")
    else:
        base_members = []

        print("class " + name + ":")

    members = list(dir(obj))
    members.extend(getattr(obj, "__dict__", {}).keys())
    members.sort()

    if INCLUDE_DOCS:
        docstring = getattr(obj, "__doc__", None)
        if docstring:
            print("    " + _triple_quote(docstring))

    last_member = None
    while members:
        member = str(members.pop(0))
        if member == last_member or member in _SKIP_MEMBERS:
            continue
        last_member = member

        if member == "__class__":
            print("    __class__ = " + name)
            continue

        try:
            value = getattr(obj, member)
        except AttributeError:
            continue
        except:
            print("    " + member + " = Unknown")
            continue

        if member in base_members:
            try:
                base_value = getattr(base, member)
            except:
                pass
            else:
                if value is base_value:
                    continue

        try:
            value_type = DIRECT_MAP[value]
        except (TypeError, LookupError):
            pass
        else:
            print("    " + member + " = " + value_type)
            continue

        if not type(value) is type:
            try:
                value_type = DIRECT_MAP[type(value)]
            except (TypeError, LookupError):
                pass
            else:
                print("    " + member + " = " + value_type + "()")
                continue

        # Builtins do not have nested types
        if hasattr(value, "__call__") and not isinstance(value, type):
            args = _render_args(value, name, member)
            restype = _get_restype(name, member) or ""
            if restype == "T":
                restype = name
            if not restype:
                warnings.warn("unknown return value for " + name + "." + member + " = " + repr(value), InspectWarning)
                restype = "Unknown()"

            for a in _each(args, name):
                print("    def " + member + a + ":")
                if INCLUDE_DOCS:
                    try:
                        doc = str(getattr(value, "__doc__"))
                    except:
                        pass
                    else:
                        print("        " + _triple_quote(doc))
                for r in _each(restype, name):
                    print("        return " + r)
            continue

        value_type = _get_restype(name, member)
        if not value_type:
            warnings.warn("unknown value for " + name + "." + member + " = " +type(value).__name__, InspectWarning)
            value_type = "Unknown()"

        for r in _each(value_type, name):
            print("    " + member + " = " + r)

    print("")

# Unknown
print("class Unknown:")
print("    '''<unknown type>'''")
print("")

# NoneType
print("class NoneType:")
print("    '''Type of the None object'''")
print("")

print("None = NoneType()")
print("")

# Object
_print_type("Object", object)

# Type
_print_type("Type", type)

# Bool
if type(bool()) is int:
    print("Bool = Int")
    print("")
else:
    _print_type("Bool", bool, "Int", int)

# Int
_print_type("Int", int)

# Long
try:
    long
except NameError:
    print("Long = Int")
    print("")
else:
    _print_type("Long", long)

# Float
_print_type("Float", float)

# Complex
_print_type("Complex", complex)

# Tuple
_print_type("Tuple", tuple)

# List
_print_type("List", list)

# Dict
_print_type("Dict", dict)

# Set
_print_type("Set", set)

# FrozenSet
_print_type("FrozenSet", frozenset)

try:
    bytes
except NameError:
    bytes = str

if bytes is not str:
    # Bytes
    _print_type("Bytes", bytes)

    # Unicode
    _print_type("Unicode", str)

    # Str
    print("Str = Unicode")
    print("")
else:
    # Bytes
    _print_type("Bytes", str)

    # Unicode
    _print_type("Unicode", unicode)

    # Str
    print("Str = Bytes")
    print("")

# StrIterator
# BytesIterator
# UnicodeIterator
# Module
# Function
# BuiltinMethodDescriptor
# BuiltinFunction
# Generator
# Property
# ClassMethod
# StaticMethod
# Ellipsis
# TupleIterator
# ListIterator
# DictKeys
# DictValues
# DictItems
# SetIterator
# CallableIterator
