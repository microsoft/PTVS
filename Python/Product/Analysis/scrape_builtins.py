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
    object: "__Object",
    type: "__Type",
    tuple: "__Tuple",
    int: "__Int",
    str: "__Str",
    None: "None",
    type(int.real): "__Property",
    type(complex.real): "__Property",
}

# These two dictionaries start with Python 3 values.
# There is an update below for Python 2 differences.

T = "__Type(self)()"
N = "None"
KNOWN_RESTYPES = {
    "__abs__": T,
    "__add__": T,
    "__and__": T,
    "__annotations__": "{}",
    "__base__": "__Type()",
    "__bases__": "(__Type(),)",
    "__bool__": "__Bool()",
    "__call__": "__Unknown()",
    "__Type.__call__": "cls()",
    "__ceil__": T,
    "__code__": "__Object()",
    "__contains__": "__Bool()",
    "__del__": N,
    "__delattr__": N,
    "__Property.__delete__": N,
    "__delitem__": N,
    "__dict__": "{'': __Unknown()}",
    "__dir__": "['']",
    "__divmod__": "(0, 0)",
    "__eq__": "__Bool()",
    "__format__": "''",
    "__float__": "__Float()",
    "__floor__": T,
    "__floordiv__": "0",
    "__ge__": "__Bool()",
    "__get__": T,
    "__getattribute__": "__Unknown()",
    "__Float.__getformat__": "''",
    "__getitem__": "__Unknown()",
    "__getnewargs__": "()",
    "__getnewargs_ex__": "((), {})",
    "__globals__": "{}",
    "__gt__": "__Bool()",
    "__hash__": "0",
    "__iadd__": N,
    "__iand__": N,
    "__imul__": N,
    "__index__": "0",
    "__init__": T,
    "__init_subclass__": N,
    "__int__": "0",
    "__Type.__instancecheck__": "__Bool()",
    "__invert__": T,
    "__ior__": N,
    "__isub__": N,
    "__iter__": T,
    "__Tuple.__iter__": "__TupleIterator()",
    "__List.__iter__": "__ListIterator()",
    "__Dict.__iter__": "__DictKeys()",
    "__Set.__iter__": "__SetIterator()",
    "__FrozenSet.__iter__": "__SetIterator()",
    "__Bytes.__iter__": "__BytesIterator()",
    "__Unicode.__iter__": "__UnicodeIterator()",
    "__ixor__": N,
    "__le__": "__Bool()",
    "__len__": "0",
    "__length_hint__": "0",
    "__lshift__": T,
    "__lt__": "__Bool()",
    "__mod__": T,
    "__mul__": T,
    "__ne__": "__Bool()",
    "__neg__": T,
    "__new__": "cls()",
    "__next__": "__Unknown()",
    "__BytesIterator.__next__": "0",
    "__UnicodeIterator.__next__": "__Unicode()",
    "__Type.__prepare__": N,
    "__pos__": T,
    "__pow__": T,
    "__or__": T,
    "__radd__": T,
    "__rand__": T,
    "__rdivmod__": "(0, 0)",
    "__List.__reversed__": "__ListIterator()",
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
    "__set__": N,
    "__setattr__": N,
    "__Float.__setformat__": N,
    "__setitem__": N,
    "__setstate__": N,
    "__sizeof__": "0",
    "__str__": "''",
    "__Type.__subclasses__": "(cls,)",
    "__sub__": T,
    "__truediv__": "Float()",
    "__trunc__": T,
    "__xor__": T,
    "__Type.__subclasscheck__": "__Bool()",
    "__subclasshook__": "__Bool()",
    "__text_signature__": "''",
    "__Set.add": N,
    "__List.append": N,
    "__Float.as_integer_ratio": "(0, 0)",
    "__Int.bit_length": "0",
    "capitalize": T,
    "casefold": T,
    "center": T,
    "clear": N,
    "__Generator.close": N,
    "conjugate": "__Complex()",
    "copy": T,
    "count": "0",
    "__Bytes.decode": "''",
    "__Property.deleter": "func",
    "__Set.difference": T,
    "__FrozenSet.difference": T,
    "__Set.difference_update": N,
    "__Set.discard": N,
    "__Bytes.encode": "b''",
    "__Unicode.encode": "b''",
    "endswith": "__Bool()",
    "expandtabs": T,
    "__List.extend": N,
    "find": "0",
    "__Unicode.format": T,
    "__Unicode.format_map": T,
    "__Bool.from_bytes": "__Bool()",
    "__Int.from_bytes": "0",
    "__Long.from_bytes": "__Long()",
    "__Float.fromhex": "__Float()",
    "__Bytes.fromhex": "b''",
    "__Dict.fromkeys": "{}",
    "__Dict.get": "self[0]",
    "__Property.getter": "func",
    "hex": "''",
    "index": "0",
    "__List.insert": N,
    "__Set.intersection": T,
    "__FrozenSet.intersection": T,
    "__Set.intersection_update": N,
    "isalnum": "__Bool()",
    "isalpha": "__Bool()",
    "isdecimal": "__Bool()",
    "isdigit": "__Bool()",
    "islower": "__Bool()",
    "isidentifier": "__Bool()",
    "isnumeric": "__Bool()",
    "isprintable": "__Bool()",
    "isspace": "__Bool()",
    "istitle": "__Bool()",
    "isupper": "__Bool()",
    "__Float.is_integer": "__Bool()",
    "__Set.isdisjoint": "__Bool()",
    "__FrozenSet.isdisjoint": "__Bool()",
    "__DictKeys.isdisjoint": "__Bool()",
    "__DictItems.isdisjoint": "__Bool()",
    "__Set.issubset": "__Bool()",
    "__FrozenSet.issubset": "__Bool()",
    "__Set.issuperset": "__Bool()",
    "__FrozenSet.issuperset": "__Bool()",
    "__Dict.items": "__DictItems()",
    "__Bytes.join": "b''",
    "__Unicode.join": "''",
    "__Dict.keys": "__DictKeys()",
    "lower": T,
    "ljust": T,
    "lstrip": T,
    "__Bytes.maketrans": "b''",
    "__Unicode.maketrans": "{}",
    "__Type.mro": "[__Type()]",
    "partition": "(__Type(self)(), __Type(self)(), __Type(self)())",
    "__List.pop": "self[0]",
    "__Dict.pop": "self.keys()[0]",
    "__Set.pop": "__Unknown()",
    "__Dict.popitem": "self.items()[0]",
    "remove": N,
    "replace": T,
    "rfind": "0",
    "__List.reverse": N,
    "rindex": "0",
    "rjust": T,
    "rpartition": "(__Type(self)(), __Type(self)(), __Type(self)())",
    "rsplit": "[__Type(self)()]",
    "rstrip": T,
    "__Generator.send": "self.__next__()",
    "__Dict.setdefault": "self[0]",
    "__Property.setter": "func",
    "__List.sort": N,
    "split": "[__Type(self)()]",
    "splitlines": "[self()]",
    "startswith": "__Bool()",
    "strip": T,
    "swapcase": T,
    "__Set.symmetric_difference": T,
    "__FrozenSet.symmetric_difference": T,
    "__Set.symmetric_difference_update": N,
    "__Bytes.translate": T,
    "__Unicode.translate": T,
    "__Generator.throw": N,
    "title": T,
    "to_bytes": "b''",
    "__Set.union": T,
    "__FrozenSet.union": T,
    "__Dict.update": N,
    "__Set.update": N,
    "upper": T,
    "__Dict.values": "__DictValues()",
    "zfill": T,
}

SELF = "(self)"
KNOWN_ARGSPECS = {
    "__Type.__call__": "(cls, *args, **kwargs)",
    "__Int.__ceil__": SELF,
    "__contains__": "(self, value)",
    "__del__": SELF,
    "__dir__": SELF,
    "__Int.__floor__": SELF,
    "__format__": "(self, format_spec)",
    "__Float.__getformat__": "(typestr)",
    "__getitem__": "(self, index)",
    "__Dict.__getitem__": "(self, key)",
    "__getnewargs__": SELF,
    "__getnewargs_ex__": SELF,
    "__init_subclass__": "(cls)",
    "__Type.__instancecheck__": "(self, instance)",
    "__Bool.__init__": "(self, x)",
    "__Int.__init__": "(self, x=0)",
    "__length_hint__": SELF,
    "__new__": "(cls, *args, **kwargs)",
    "__Type.__prepare__": "(cls, name, bases, **kwds)",
    "__Int.__round__": "(self, ndigits=0)",
    "__Float.__round__": "(self, ndigits=0)",
    "__reduce__": SELF,
    "__reduce_ex__": "(self, protocol)",
    "__List.__reversed__": SELF,
    "__Float.__setformat__": "(typestr, fmt)",
    "__List.__setitem__": "(self, index, value)",
    "__Dict.__setitem__": "(self, key, value)",
    "__setstate__": "(self, state)",
    "__sizeof__": SELF,
    "__Type.__subclasses__": "(cls)",
    "__Type.__subclasscheck__": "(cls, subclass)",
    "__subclasshook__": "(cls, subclass)",
    "__trunc__": SELF,
    "__Set.add": "(self, value)",
    "__List.append": "(self, value)",
    "__Float.as_integer_ratio": SELF,
    "__Int.bit_length": SELF,
    "capitalize": SELF,
    "casefold": SELF,
    "__Bytes.center": ["(self, width)", "(self, width, fillbyte)"],
    "__Unicode.center": ["(self, width)", "(self, width, fillchar)"],
    "clear": SELF,
    "__Generator.close": SELF,
    "conjugate": SELF,
    "copy": SELF,
    "count": "(self, x)",
    "__Bytes.count": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Unicode.count": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Bytes.decode": "(self, encoding='utf-8', errors='strict')",
    "__Property.deleter": "(self, func)",
    "__Set.difference": "(self, other)",
    "__FrozenSet.difference": "(self, other)",
    "__Set.difference_update": "(self, *others)",
    "__Set.discard": "(self, elem)",
    "__Unicode.encode": "(self, encoding='utf-8', errors='strict')",
    "endswith": ["(self, suffix)", "(self, suffix, start)", "(self, suffix, start, end)"],
    "expandtabs": "(self, tabsize=8)",
    "__List.extend": "(self, iterable)",
    "find": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Unicode.format": "(self, *args, **kwargs)",
    "__Unicode.format_map": "(self, mapping)",
    "__Bool.from_bytes": "(bytes, byteorder, *, signed=False)",
    "__Int.from_bytes": "(bytes, byteorder, *, signed=False)",
    "__Float.fromhex": "(string)",
    "__Dict.get": "(self, key, d=Unknown())",
    "__Property.getter": "(self, func)",
    "hex": SELF,
    "__List.insert": "(self, index, value)",
    "index": "(self, v)",
    "__Bytes.index": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Unicode.index": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Set.intersection": "(self, other)",
    "__FrozenSet.intersection": "(self, other)",
    "__Set.intersection_update": "(self, *others)",
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
    "__Float.is_integer": SELF,
    "__Set.isdisjoint": "(self, other)",
    "__FrozenSet.isdisjoint": "(self, other)",
    "__DictKeys.isdisjoint": "(self, other)",
    "__DictItems.isdisjoint": "(self, other)",
    "__Set.issubset": "(self, other)",
    "__FrozenSet.issubset": "(self, other)",
    "__Set.issuperset": "(self, other)",
    "__FrozenSet.issuperset": "(self, other)",
    "__Dict.items": SELF,
    "__Bytes.join": "(self, iterable)",
    "__Unicode.join": "(self, iterable)",
    "__Dict.keys": SELF,
    "lower": SELF,
    "__Bytes.ljust": ["(self, width)", "(self, width, fillbyte)"],
    "__Unicode.ljust": ["(self, width)", "(self, width, fillchar)"],
    "lstrip": ["(self)", "(self, chars)"],
    "__Bytes.maketrans": "(from_, to)",
    "__Unicode.maketrans": ["(x)", "(x, y)", "(x, y, z)"],
    "__Type.mro": "(cls)",
    "__Bytes.partition": "(self, sep)",
    "__Unicode.partition": "(self, sep)",
    "__List.pop": "(self, index=-1)",
    "__Dict.pop": "(self, k, d=Unknown())",
    "__Set.pop": SELF,
    "__Dict.popitem": "(self, k, d=Unknown())",
    "__List.remove": "(self, value)",
    "__Set.remove": "(self, elem)",
    "replace": ["(self, old, new)", "(self, old, new, count)"],
    "__List.reverse": SELF,
    "rfind": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "rindex": ["(self, sub)", "(self, sub, start)", "(self, sub, start, end)"],
    "__Bytes.rjust": ["(self, width)", "(self, width, fillbyte)"],
    "__Unicode.rjust": ["(self, width)", "(self, width, fillchar)"],
    "__Bytes.rpartition": "(self, sep)",
    "__Unicode.rpartition": "(self, sep)",
    "rsplit": "(self, sep=None, maxsplit=-1)",
    "rstrip": ["(self)", "(self, chars)"],
    "__Generator.send": "(self, value)",
    "__Dict.setdefault": "(self, k, d)",
    "__Property.setter": "(self, func)",
    "__List.sort": SELF,
    "split": "(self, sep=None, maxsplit=-1)",
    "splitlines": "(self, keepends=False)",
    "strip": ["(self)", "(self, chars)"],
    "startswith": ["(self, prefix)", "(self, prefix, start)", "(self, prefix, start, end)"],
    "swapcase": SELF,
    "__Set.symmetric_difference": "(self, other)",
    "__FrozenSet.symmetric_difference": "(self, other)",
    "__Set.symmetric_difference_update": "(self, *others)",
    "__Generator.throw": ["(self, type)", "(self, type, value)", "(self, type, value, traceback)"],
    "title": SELF,
    "__Int.to_bytes": "(bytes, byteorder, *, signed=False)",
    "__Bytes.translate": "(self, table, delete=b'')",
    "__Unicode.translate": "(self, table)",
    "__Set.union": "(self, *others)",
    "__FrozenSet.union": "(self, *others)",
    "__Dict.update": "(self, d)",
    "__Set.update": "(self, *others)",
    "upper": SELF,
    "__Dict.values": SELF,
    "zfill": "(self, width)",
}

if sys.version[0] == '2':
    KNOWN_RESTYPES.update({
        "__BytesIterator.__next__": None,
        "__BytesIterator.next": "b''",
        "__UnicodeIterator.__next__": None,
        "__UnicodeIterator.next": "u''",
        "__Generator.send": "self.next()",
    })

    KNOWN_ARGSPECS.update({
        "__BytesIterator.next": SELF,
        "__UnicodeIterator.next": SELF,
    })


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

_SKIP_MEMBERS = frozenset(["__class__"])

_ALREADY_WRITTEN = set()

def _print_type(alias, objtype, basename=None, base=None):
    name = objtype.__name__
    
    if name in _ALREADY_WRITTEN:
        if alias != name:
            print(alias + " = " + name)
            print("")
        return
    _ALREADY_WRITTEN.add(name)

    if getattr(objtype, '__bases__', None) == (object,):
        basename = "Object"
        base = object

    if base:
        if base not in objtype.__bases__:
            warnings.warn(basename + " not in " + name + ".__bases__ = " + repr(objtype.__bases__), InspectWarning)

        base_members = list(dir(base))
        base_members.extend(getattr(base, "__dict__", {}).keys())
        base_members.sort()

        print("class " + name + "(" + basename + "):")
    else:
        base_members = []

        print("class " + name + ":")

    members = list(dir(objtype))
    members.extend(getattr(objtype, "__dict__", {}).keys())
    members.sort()

    if INCLUDE_DOCS:
        docstring = getattr(objtype, "__doc__", None)
        if isinstance(docstring, str):
            print("    " + _triple_quote(docstring))
            members = [m for m in members if m != '__doc__']

    last_member = None
    while members:
        member = str(members.pop(0))
        if member == last_member or member in _SKIP_MEMBERS:
            continue
        last_member = member

        try:
            value = getattr(objtype, member)
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
            args = _render_args(value, alias, member)
            restype = _get_restype(alias, member) or ""
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

        value_type = _get_restype(alias, member)
        if not value_type:
            warnings.warn("unknown value for " + name + "." + member + " = " +type(value).__name__, InspectWarning)
            value_type = "Unknown()"

        for r in _each(value_type, name):
            print("    " + member + " = " + r)

    if alias != name:
        print(alias + " = " + name)
    print("")

# Unknown
print("class __Unknown:")
print("    '''<unknown type>'''")
print("")

# NoneType
print("class __NoneType:")
print("    '''Type of the None object'''")
print("")

print("None = __NoneType()")
print("")

# Object
_print_type("__Object", object)

# Type
_print_type("__Type", type)

# Bool
if type(bool()) is int:
    print("__Bool = __Int")
    print("")
else:
    _print_type("__Bool", bool, "__Int", int)

# Int
_print_type("__Int", int)

# Long
try:
    long
except NameError:
    print("__Long = __Int")
    print("")
else:
    _print_type("__Long", long)

# Float
_print_type("__Float", float)

# Complex
_print_type("__Complex", complex)

# Tuple
_print_type("__Tuple", tuple)

# List
_print_type("__List", list)

# Dict
_print_type("__Dict", dict)

# Set
_print_type("__Set", set)

# FrozenSet
_print_type("__FrozenSet", frozenset)

try:
    bytes
except NameError:
    bytes = str

if bytes is not str:
    # Bytes
    _print_type("__Bytes", bytes)

    # BytesIterator
    _print_type("__BytesIterator", type(iter(bytes())))

    # Unicode
    _print_type("__Unicode", str)

    # UnicodeIterator
    _print_type("__UnicodeIterator", type(iter(str())))

    # Str
    print("__Str = __Unicode")
    print("")

    # StrIterator
    print("__StrIterator = __UnicodeIterator")
    print("")

else:
    # Bytes
    _print_type("__Bytes", str)

    # BytesIterator
    _print_type("__BytesIterator", type(iter(str())))

    # Unicode
    _print_type("__Unicode", unicode)

    # UnicodeIterator
    _print_type("__UnicodeIterator", type(iter(unicode())))

    # Str
    print("__Str = __Bytes")
    print("")

    # StrIterator
    print("__StrIterator = __BytesIterator")
    print("")

# Module
_print_type("__Module", type(inspect))

# Function

# These functions will be exec'd in order until one succeeds.
# This ensures the maximum number of attributes will be initialized.

_FUNCTIONS = [
"""
def _in_closure():
    x = 1
    def Function(a:'a', b=None, *args, **kwds) -> 'rv': x
    return Function
Function = _in_closure()
del _in_closure""",
"def Function(a, b=None, *args, **kwds): pass",
"def Function(): pass"
]

for f in _FUNCTIONS:
    try:
        exec(f)
        break
    except SyntaxError:
        pass
else:
    raise RuntimeError("no valid function could be defined")

_print_type("__Function", Function)

# BuiltinMethodDescriptor
_print_type("__BuiltinMethodDescriptor", type(object.__hash__))

# BuiltinFunction
_print_type("__BuiltinFunction", type(abs))

# Generator
_print_type("__Generator", type((_ for _ in [])))

# Property
_print_type("__Property", property)

# ClassMethod
_print_type("__ClassMethod", classmethod)

# StaticMethod
_print_type("__StaticMethod", staticmethod)

# Ellipsis
_print_type("__Ellipsis", type(Ellipsis))

# TupleIterator
_print_type("__TupleIterator", type(iter(())))

# ListIterator
_print_type("__ListIterator", type(iter([])))

# DictKeys
_print_type("__DictKeys", type({}.keys()))

# DictValues
_print_type("__DictValues", type({}.values()))

# DictItems
_print_type("__DictItems", type({}.items()))

# SetIterator
_print_type("__SetIterator", type(iter(set())))

# CallableIterator
_print_type("__CallableIterator", type(iter((lambda: None), None)))

