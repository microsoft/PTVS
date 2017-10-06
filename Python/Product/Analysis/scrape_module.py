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

import ast
import inspect
import io
import re
import sys
import warnings

try:
    import builtins
except ImportError:
    import __builtin__ as builtins

try:
    bytes
except NameError:
    bytes = str

try:
    unicode
except NameError:
    unicode = str

class InspectWarning(UserWarning): pass

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


SKIP_TYPENAME_FOR_TYPES = bool, str, bytes, int, float
STATICMETHOD_TYPES = ()
CLASSMETHOD_TYPES = type(float.fromhex),
PROPERTY_TYPES = type(int.real), type(property.fget)

if sys.version_info[0] < 3:
    SKIP_TYPENAME_FOR_TYPES += unicode, long

def safe_callable(v):
    try:
        return hasattr(v, '__call__')
    except Exception:
        return False

class Signature(object):
    # These two dictionaries start with Python 3 values.
    # There is an update below for Python 2 differences.
    # They will be used as fallbacks for known protocols

    KNOWN_RESTYPES = {
        "__abs__": "self",
        "__add__": "self",
        "__and__": "self",
        "__annotations__": "{}",
        "__base__": "type",
        "__bases__": "(type,)",
        "__bool__": "False",
        "__call__": "Any",
        "__ceil__": "self",
        "__code__": "object()",
        "__contains__": "False",
        "__del__": "None",
        "__delattr__": "None",
        "__delitem__": "None",
        "__dict__": "{'': Any}",
        "__dir__": "['']",
        "__divmod__": "(0, 0)",
        "__eq__": "False",
        "__format__": "''",
        "__float__": "0.0",
        "__floor__": "self",
        "__floordiv__": "0",
        "__ge__": "False",
        "__get__": "self",
        "__getattribute__": "Any",
        "__getitem__": "Any",
        "__getnewargs__": "()",
        "__getnewargs_ex__": "((), {})",
        "__globals__": "{}",
        "__gt__": "False",
        "__hash__": "0",
        "__iadd__": "None",
        "__iand__": "None",
        "__imul__": "None",
        "__index__": "0",
        "__init__": "self",
        "__init_subclass__": "None",
        "__int__": "0",
        "__invert__": "self",
        "__ior__": "None",
        "__isub__": "None",
        "__iter__": "self",
        "__ixor__": "None",
        "__le__": "False",
        "__len__": "0",
        "__length_hint__": "0",
        "__lshift__": "self",
        "__lt__": "False",
        "__mod__": "self",
        "__mul__": "self",
        "__ne__": "False",
        "__neg__": "self",
        "__new__": "cls()",
        "__next__": "Any",
        "__pos__": "self",
        "__pow__": "self",
        "__or__": "self",
        "__radd__": "self",
        "__rand__": "self",
        "__rdivmod__": "(0, 0)",
        "__rfloordiv__": "self",
        "__rlshift__": "self",
        "__rmod__": "self",
        "__rmul__": "self",
        "__ror__": "self",
        "__round__": "self",
        "__rpow__": "self",
        "__rrshift__": "self",
        "__rshift__": "self",
        "__rsub__": "self",
        "__rtruediv__": "self",
        "__rxor__": "self",
        "__reduce__": ["''", "()"],
        "__reduce_ex__": ["''", "()"],
        "__repr__": "''",
        "__set__": "None",
        "__setattr__": "None",
        "__setitem__": "None",
        "__setstate__": "None",
        "__sizeof__": "0",
        "__str__": "''",
        "__sub__": "self",
        "__truediv__": "0.0",
        "__trunc__": "self",
        "__xor__": "self",
        "__subclasscheck__": "False",
        "__subclasshook__": "False",
    }

    KNOWN_ARGSPECS = {
        "__contains__": "(self, value)",
        "__del__": "(self)",
        "__dir__": "(self)",
        "__floor__": "(self)",
        "__format__": "(self, format_spec)",
        "__getitem__": "(self, index)",
        "__getnewargs__": "(self)",
        "__getnewargs_ex__": "(self)",
        "__init_subclass__": "(cls)",
        "__instancecheck__": "(self, instance)",
        "__length_hint__": "(self)",
        "__new__": "(cls, *args, **kwargs)",
        "__prepare__": "(cls, name, bases, **kwds)",
        "__round__": "(self, ndigits=0)",
        "__reduce__": "(self)",
        "__reduce_ex__": "(self, protocol)",
        "__reversed__": "(self)",
        "__setitem__": "(self, index, value)",
        "__setstate__": "(self, state)",
        "__sizeof__": "(self)",
        "__subclasses__": "(cls)",
        "__subclasscheck__": "(cls, subclass)",
        "__subclasshook__": "(cls, subclass)",
        "__trunc__": "(self)",
    }


    def __init__(self,
        name,
        callable,
        scope=None,
        defaults=None,
        scope_alias=None,
        decorators=None,
        module_doc=None,
    ):
        self.callable = callable
        self.name = name
        self.scope = scope
        self.decorators = decorators or ()
        self._signature = None
        self._defaults = defaults or ()

        if scope and '@staticmethod' not in self.decorators:
            def_arg = 'cls' if '@classmethod' in self.decorators else 'self'
            if len(self._defaults) == 0 or self._defaults[0] != def_arg:
                self._defaults = (def_arg,) + self._defaults
        
        self.fullsig = None
        if self.name in ('__init__', '__new__') and module_doc:
            self.fullsig = self._init_argspec_fromdocstring(self._defaults, module_doc)
        elif not hasattr(self.callable, '__call__') and hasattr(self.callable, '__get__'):
            # We have a property
            self.decorators = '@property',
            self.fullsig = self.name + "(" + ", ".join(self._defaults) + ")"
        
        self.fullsig = (
            self.fullsig or
            # Disable fromsignature() because it doesn't work as well as argspec
            #self._init_argspec_fromsignature(self._defaults) or
            self._init_argspec_fromargspec(self._defaults) or
            self._init_argspec_fromdocstring(self._defaults) or
            self._init_argspec_fromknown(self._defaults, scope_alias) or
            (self.name + "(" + ", ".join(self._defaults) + ")")
        )
        self.restype = (
            self._init_restype_fromsignature() or
            self._init_restype_fromknown(scope_alias) or
            'pass'
        )

    def __str__(self):
        return self.fullsig

    def _init_argspec_fromsignature(self, defaults):
        try:
            sig = inspect.signature(self.callable)
        except Exception:
            return

        new_args = []
        for arg in sig.parameters:
            p = sig.parameters[arg]
            if p.default != inspect.Signature.empty:
                try:
                    ast.literal_eval(repr(p.default))
                except Exception:
                    p = p.replace(default=None)
            if p.kind == inspect.Parameter.POSITIONAL_ONLY:
                p = p.replace(kind=inspect.Parameter.POSITIONAL_OR_KEYWORD)
            new_args.append(p)
        sig = sig.replace(parameters=new_args)

        return self.name + str(sig)

    def _init_restype_fromsignature(self):
        try:
            sig = inspect.signature(self.callable)
        except Exception:
            return

        # If signature has a return annotation, it's in the
        # full signature and we don't need it from here.
        return 'pass'

    def _init_argspec_fromargspec(self, defaults):
        try:
            args = (getattr(inspect, 'getfullargspec', None) or inspect.getargspec)(self.callable)
        except Exception:
            return

        argn = list(args.args)
        if getattr(args, 'varargs', None):
            argn.append('*' + args.varargs)
        if getattr(args, 'varkw', None):
            argn.append('**' + args.varkw)

        self._insert_default_arguments(argn, defaults)
        return self.name + '(' + ', '.join(argn) + ')'

    def _init_argspec_fromknown(self, defaults, scope_alias):
        spec = None
        if scope_alias and not spec:
            spec = self.KNOWN_ARGSPECS.get(scope_alias + '.' + self.name)
        if self.scope and not spec:
            spec = self.KNOWN_ARGSPECS.get(self.scope + '.' + self.name)
        if not spec:
            spec = self.KNOWN_ARGSPECS.get(self.name)
        if not spec:
            return

        return self.name + spec

    def _init_restype_fromknown(self, scope_alias):
        restype = None
        if scope_alias and not restype:
            restype = self.KNOWN_RESTYPES.get(scope_alias + '.' + self.name)
        if self.scope and not restype:
            restype = self.KNOWN_RESTYPES.get(self.scope + '.' + self.name)
        if not restype:
            restype = self.KNOWN_RESTYPES.get(self.name)
        if not restype:
            return

        if isinstance(restype, list):
            return "return " + "; return ".join(restype)
        return "return " + restype

    def _init_argspec_fromdocstring(self, defaults, doc=None):
        allow_name_mismatch = True
        if not doc:
            doc = getattr(self.callable, '__doc__', None)
            allow_name_mismatch = False
        if not isinstance(doc, str):
            return

        doc = self._get_first_function_call(doc)
        if not doc:
            return

        call = self._parse_funcdef(doc, allow_name_mismatch)
        if not call:
            doc = re.sub(r'[\[\]]', '', doc)
            call = self._parse_funcdef(doc, allow_name_mismatch)
        if not call:
            doc = re.sub(r'\=.+?([,\)])', r'\1', doc)
            call = self._parse_funcdef(doc, allow_name_mismatch)
        if not call:
            return

        args = self._ast_args_to_list(call.args)
        self._insert_default_arguments(args, defaults)
        return self.name + '(' + ', '.join(args) + ')'

    def _insert_default_arguments(self, args, defaults):
        if len(args) < len(defaults):
            args[:0] = defaults
        else:
            for i, (x, y) in enumerate(zip(defaults, args)):
                if x == 'cls' and y == 'type':
                    continue
                if x != y:
                    args[:i] = defaults
                    break

    def _parse_funcdef(self, expr, allow_name_mismatch):
        '''Takes a call expression that was part of a docstring
        and parses the AST as if it were a definition. If the parsed
        AST matches the callable we are wrapping, returns the node.
        '''
        try:
            node = ast.parse("def " + expr + ": pass").body[0]
            if isinstance(node, ast.FunctionDef):
                if allow_name_mismatch or node.name == self.name:
                    return node
                warnings.warn('function ' + self.name + ' had call to ' + node.name + ' in docstring', InspectWarning)
        except SyntaxError:
            pass

    def _get_first_function_call(self, expr):
        '''Scans the string for the first closing parenthesis,
        handling nesting, which is the best heuristic we have for
        an example call at the start of the docstring.'''
        if not expr or ')' not in expr:
            return
        expr = expr.lstrip('\r\n\t ')
        n = 0
        for i, c in enumerate(expr):
            if c == ')':
                n -= 1
                if n <= 0:
                    return expr[:i + 1]
            elif c == '(':
                n += 1

    def _ast_args_to_list(self, node):
        args = node.args
        defaults = list(getattr(node, 'defaults', ()))
        defaults[:0] = [None for _ in range(len(args) - len(defaults))]

        seen_names = set()
        return [self._ast_arg_to_str(a, d, seen_names) for a, d in zip(args, defaults)]

    _AST_ARG_TYPES = tuple(getattr(ast, n) for n in (
        'arg', 'keyword', 'Name'
    ) if hasattr(ast, n))

    class DefaultValueWriter(object):
        def walk(self, node):
            try:
                op = getattr(self, 'walk_' + type(node).__name__)
            except AttributeError:
                print('walk_' + type(node).__name__, vars(node), file=sys.stderr)
                return None
            else:
                return op(node)

        def walk_BitOr(self, node):         return '|'
        def walk_Call(self, node):          pass    # Do not generate defaults from calls
        def walk_Name(self, node):          return node.id
        def walk_NameConstant(self, node):  return repr(node.value)
        def walk_Num(self, node):           return str(node.n)
        def walk_Str(self, node):           return repr(node.s)
        def walk_USub(self, node):          return '-'

        def walk_Attribute(self, node):
            v = self.walk(node.value)
            if v:
                return v + '.' + node.attr

        def walk_List(self, node):
            elts = [self.walk(n) for n in node.elts]
            if any(n is None for n in elts):
                return '[]'
            return '[' + ', '.join(elts) + ']'

        def walk_Tuple(self, node):
            elts = [self.walk(n) for n in node.elts]
            if any(n is None for n in elts):
                return '()'
            return '(' + ', '.join(elts) + ')'

        def walk_BinOp(self, node):
            v1 = self.walk(node.left)
            op = self.walk(node.op)
            v2 = self.walk(node.right)
            if v1 and op and v2:
                return v1 + op + v2

        def walk_UnaryOp(self, node):
            op = self.walk(node.op)
            value = self.walk(node.operand)
            if op and value:
                return op + value

    def _ast_arg_to_str(self, arg, default, seen_names):
        '''Converts an AST argument object into a string.'''
        arg_id = None
        default_value = ''
        if isinstance(arg, ast.List):
            default_value = '=None'
            arg = arg.elts[0]

        if isinstance(arg, ast.keyword):
            try:
                default_value = '=' + arg.value.id
            except AttributeError:
                pass
            arg_id = arg.arg

        if default:
            v = self.DefaultValueWriter().walk(default)
            if v is not None:
                default_value = '=' + v

        if isinstance(arg, ast.Tuple):
            arg_id = '(' + ', '.join(a.id for a in arg.elts) + ')'

        if not arg_id and not isinstance(arg, self._AST_ARG_TYPES):
            warnings.warn('failed to get argument name for ' + repr(arg) + repr(vars(arg)), InspectWarning)

        if not arg_id:
            arg_id = getattr(arg, 'arg', None)
            if arg_id is None:
                arg_id = arg.id

        if arg_id in seen_names:
            i = 2
            new_arg_id = arg_id + str(i)
            while new_arg_id in seen_names:
                i += 1
                new_arg_id = arg_id + str(i)
            arg_id = new_arg_id
        seen_names.add(arg_id)

        if default_value:
            final_arg = arg_id + default_value
            try:
                ast.parse(final_arg)
            except SyntaxError:
                pass
            else:
                seen_names.add(' default_value')
                return final_arg
        
        if ' default_value' in seen_names:
            return arg_id + '=None'

        return arg_id

class MemberInfo(object):
    NO_VALUE = object()

    def __init__(self, name, value, literal=None, scope=None, module=None, alias=None, module_doc=None):
        self.name = name
        self.value = value
        self.literal = literal
        self.members = []
        self.values = []
        self.need_imports = ()
        self.type_name = None
        self.scope_name = None
        self.bases = ()
        self.signature = None
        self.documentation = getattr(value, '__doc__', None)
        if not isinstance(self.documentation, str):
            self.documentation = None

        if self.name:
            self.name = self.name.replace('-', '_')

        value_type = type(value)
        if issubclass(value_type, type):
            self.need_imports, type_name = self._get_typename(value, module)
            if '.' in type_name:
                self.literal = type_name
            else:
                self.scope_name = self.type_name = type_name
                try:
                    bases = getattr(value, '__bases__', ())
                except Exception:
                    pass
                else:
                    self.bases = []
                    self.need_imports = list(self.need_imports)
                    for ni, t in (self._get_typename(b, module) for b in bases):
                        if not t:
                            continue
                        if t == type_name and module in ni:
                            continue
                        self.bases.append(t)
                        self.need_imports.extend(ni)

        elif safe_callable(value):
            dec = ()
            if scope:
                if value_type in STATICMETHOD_TYPES:
                    dec += '@staticmethod',
                elif value_type in CLASSMETHOD_TYPES:
                    dec += '@classmethod',
            self.signature = Signature(name, value, scope, scope_alias=alias, decorators=dec, module_doc=module_doc)
        elif value is not None:
            if value_type in PROPERTY_TYPES:
                self.signature = Signature(name, value, scope, scope_alias=alias)
            if value_type not in SKIP_TYPENAME_FOR_TYPES:
                self.need_import, self.type_name = self._get_typename(value_type, module)
        elif not self.literal:
            self.literal = 'None'

    @classmethod
    def _get_typename(cls, value_type, in_module):
        try:
            type_name = value_type.__name__.replace('-', '_')
            module = getattr(value_type, '__module__', None)
            if module and module != '<unknown>':
                if module != in_module:
                    type_name = module + '.' + type_name
                return (module,), type_name
            return (), type_name
        except Exception:
            warnings.warn('could not get type of ' + repr(value), InspectWarning)
            return (), None
        
        return self.type_name

    def _str_from_literal(self, lit):
        return self.name + ' = ' + lit

    def _str_from_typename(self, type_name):
        return self.name + ' = ' + type_name + '()'

    def _str_from_value(self, v):
        return self.name + ' = ' + repr(v)

    def _lines_with_members(self):
        if self.bases:
            yield 'class ' + self.name + '(' + ','.join(self.bases) + '):'
        else:
            yield 'class ' + self.name + ':'
        if self.documentation:
            yield '    ' + repr(self.documentation)
        if self.members:
            for mi in self.members:
                if mi is not MemberInfo.NO_VALUE:
                    yield mi.as_str('    ')
        else:
            yield '    pass'
        yield ''

    def _lines_with_signature(self):
        seen_decorators = set()
        for d in self.signature.decorators:
            d = str(d)
            if d not in seen_decorators:
                seen_decorators.add(d)
                yield d
        yield 'def ' + str(self.signature) + ':'
        if self.documentation:
            yield '    ' + repr(self.documentation)
        yield '    pass'
        yield ''

    def as_str(self, indent=''):
        if self.literal:
            return indent + self._str_from_literal(self.literal)

        if self.members:
            return '\n'.join(indent + s for s in self._lines_with_members())

        if self.signature:
            return '\n'.join(indent + s for s in self._lines_with_signature())

        if self.type_name is not None:
            return indent + self._str_from_typename(self.type_name)

        if self.value is not None:
            return indent + self._str_from_value(self.value)

        return indent + self.name


MODULE_MEMBER_SUBSTITUTE = {
    '__builtins__': MemberInfo('__builtins__', {}),
    '__spec__': None,
    '__loader__': None,
}

CLASS_MEMBER_SUBSTITUTE = {
    '__bases__': MemberInfo('__bases__', ()),
    '__mro__': MemberInfo('__mro__', ()),
    '__dict__': MemberInfo('__dict__', {}),
    '__doc__': None,
}

class ScrapeState(object):
    def __init__(self, module_name, module=None):
        self.root_module = None
        self.module = module
        self.module_name = module_name

        self.imports = set()
        self.members = []

    def initial_import(self, search_path=None):
        if self.module:
            return

        if search_path:
            sys.path.insert(0, search_path)

        try:
            try:
                mod = __import__(self.module_name)
            except Exception:
                ex_msg = str(sys.exc_info()[1])
                warnings.warn("Working around " + ex_msg, InspectWarning)
                if ex_msg == "This must be an MFC application - try 'import win32ui' first":
                    import win32ui
                elif ex_msg == "Could not find TCL routines" or self.module_name == 'matplotlib.backends._tkagg':
                    if sys.version_info[0] == 2:
                        import Tkinter
                    else:
                        import tkinter
                else:
                    raise
                mod = None
            if not mod:
                # Try the import again, either fixed or without chaining the
                # previous exception.
                mod = __import__(self.module_name)
        finally:
            if search_path:
                del sys.path[0]
        self.root_module = mod

        # __import__ gives us the topmost module. We should generally use
        # getattr() from here to find the child module. However, sometimes
        # this is unsuccessful. So we work backwards through the full name
        # to see what is in sys.modules, then getattr() to go forwards.
        mod_name = self.module_name
        bits = []
        while mod_name and mod_name not in sys.modules:
            mod_name, _, bit = self.module_name.rpartition('.')[0]
            bits.insert(0, bit)

        if mod_name:
            self.root_module = mod = sys.modules[mod_name]
        else:
            bits = self.module_name.split('.')[1:]

        for bit in bits:
            mod = getattr(mod, bit)

        self.module = mod

    def collect_top_level_members(self):
        self._collect_members(self.module, self.members, MODULE_MEMBER_SUBSTITUTE, None)

        m_names = set(m.name for m in self.members)
        undeclared = []
        for m in self.members:
            if m.value is not None and m.type_name and '.' not in m.type_name and m.type_name not in m_names:
                undeclared.append(MemberInfo(m.type_name, type(m.value), module=self.module_name))

        self.members[:0] = undeclared

    def _should_collect_members(self, member):
        if self.module_name in member.need_imports and member.name == member.type_name:
            return True
        # Support cffi libs
        if member.type_name == builtins.__name__ + '.CompiledLib':
            return True

    def collect_second_level_members(self):
        for mi in self.members:
            if self._should_collect_members(mi):
                substitutes = dict(CLASS_MEMBER_SUBSTITUTE)
                substitutes['__class__'] = MemberInfo('__class__', None, literal=mi.type_name)
                self._collect_members(mi.value, mi.members, substitutes, mi.scope_name)

                if mi.scope_name != mi.type_name:
                    # When the scope and type names are different, we have a static
                    # class. To emulate this, we add '@staticmethod' decorators to
                    # all members.
                    for mi2 in mi.members:
                        if mi2.signature:
                            mi2.signature.decorators += '@staticmethod',

    def _collect_members(self, mod, members, substitutes, scope):
        '''Fills the members attribute with a dictionary containing
        all members from the module.'''
        if not mod:
            raise RuntimeError("failed to import module")
        if mod is MemberInfo.NO_VALUE:
            return
        
        mod_scope = (self.module_name + '.' + scope) if scope else self.module_name
        mod_doc = getattr(mod, '__doc__', None)
        mro = (getattr(mod, '__mro__', None) or ())[1:]
        for name in dir(mod):
            try:
                m = substitutes[name]
                if m:
                    members.append(m)
                continue
            except LookupError:
                pass
            try:
                m = substitutes[mod_scope + '.' + name]
                if m:
                    members.append(m)
                continue
            except LookupError:
                pass

            try:
                value = getattr(mod, name)
            except AttributeError:
                warnings.warn("attribute " + name + " on " + repr(mod) + " was in dir() but not getattr()", InspectWarning)
            except Exception:
                warnings.warn("error getting " + name + " for " + repr(mod), InspectWarning)
            else:
                if not self._should_add_value(value):
                    continue
                if self._mro_contains(mro, name, value):
                    continue
                members.append(MemberInfo(name, value, scope=scope, module=self.module_name, module_doc=mod_doc))

    def _should_add_value(self, value):
        try:
            value_type = type(value)
            mod = getattr(value_type, '__module__', None)
            name = value_type.__name__
        except Exception:
            warnings.warn("error getting typename", InspectWarning)
            return

        if (mod, name) == (builtins.__name__, 'CompiledLib'):
            # Always allow CFFI lib
            return True

        if issubclass(value_type, (type(sys), type(inspect))):
            # Disallow nested modules
            return

        # By default, include all values
        return True

    def _mro_contains(self, mro, name, value):
        for m in mro:
            try:
                mro_value = getattr(m, name)
            except Exception:
                pass
            else:
                if mro_value is value:
                    return True

    def translate_members(self):
        pass

    def dump(self, out):
        imports = set()
        for value in self.members:
            for mod in value.need_imports:
                imports.add(mod)
        imports.discard(self.module_name)

        if imports:
            for mod in sorted(imports):
                print("import " + mod, file=out)
            print("", file=out)

        for value in self.members:
            s = value.as_str('')
            try:
                print(s, file=out)
            except TypeError:
                print(repr(s), file=sys.stderr)
                raise

def add_builtin_objects(state):
    T = "__Type(self)()"
    Signature.KNOWN_RESTYPES.update({
        "__Type.__call__": "cls()",
        "__Property.__delete__": "None",
        "__Float.__getformat__": "''",
        "__Type.__instancecheck__": "__Bool()",
        "__Tuple.__iter__": "__TupleIterator()",
        "__List.__iter__": "__ListIterator()",
        "__Dict.__iter__": "__DictKeys()",
        "__Set.__iter__": "__SetIterator()",
        "__FrozenSet.__iter__": "__SetIterator()",
        "__Bytes.__iter__": "__BytesIterator()",
        "__Unicode.__iter__": "__UnicodeIterator()",
        "__BytesIterator.__next__": "0",
        "__UnicodeIterator.__next__": "__Unicode()",
        "__Type.__prepare__": "None",
        "__List.__reversed__": "__ListIterator()",
        "__Float.__setformat__": "None",
        "__Type.__subclasses__": "(cls,)",
        "__truediv__": "Float()",
        "__Type.__subclasscheck__": "__Bool()",
        "__subclasshook__": "__Bool()",
        "__Set.add": "None",
        "__List.append": "None",
        "__Float.as_integer_ratio": "(0, 0)",
        "__Int.bit_length": "0",
        "capitalize": T,
        "casefold": T,
        "center": T,
        "clear": "None",
        "__Generator.close": "None",
        "conjugate": "__Complex()",
        "copy": T,
        "count": "0",
        "__Bytes.decode": "''",
        "__Property.deleter": "func",
        "__Set.difference": T,
        "__FrozenSet.difference": T,
        "__Set.difference_update": "None",
        "__Set.discard": "None",
        "__Bytes.encode": "b''",
        "__Unicode.encode": "b''",
        "endswith": "__Bool()",
        "expandtabs": T,
        "__List.extend": "None",
        "find": "0",
        "__Unicode.format": T,
        "__Unicode.format_map": T,
        "__Bool.from_bytes": "__Bool()",
        "__Int.from_bytes": "0",
        "__Long.from_bytes": "__Long()",
        "__Float.fromhex": "0.0",
        "__Bytes.fromhex": "b''",
        "__Dict.fromkeys": "{}",
        "__Dict.get": "self[0]",
        "__Property.getter": "func",
        "hex": "''",
        "index": "0",
        "__List.insert": "None",
        "__Set.intersection": T,
        "__FrozenSet.intersection": T,
        "__Set.intersection_update": "None",
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
        "__Set.pop": "Any",
        "__Dict.popitem": "self.items()[0]",
        "remove": "None",
        "replace": T,
        "rfind": "0",
        "__List.reverse": "None",
        "rindex": "0",
        "rjust": T,
        "rpartition": "(__Type(self)(), __Type(self)(), __Type(self)())",
        "rsplit": "[__Type(self)()]",
        "rstrip": T,
        "__Generator.send": "self.__next__()",
        "__Dict.setdefault": "self[0]",
        "__Property.setter": "func",
        "__List.sort": "None",
        "split": "[__Type(self)()]",
        "splitlines": "[self()]",
        "startswith": "__Bool()",
        "strip": T,
        "swapcase": T,
        "__Set.symmetric_difference": T,
        "__FrozenSet.symmetric_difference": T,
        "__Set.symmetric_difference_update": "None",
        "__Bytes.translate": T,
        "__Unicode.translate": T,
        "__Generator.throw": "None",
        "title": T,
        "to_bytes": "b''",
        "__Set.union": T,
        "__FrozenSet.union": T,
        "__Dict.update": "None",
        "__Set.update": "None",
        "upper": T,
        "__Dict.values": "__DictValues()",
        "zfill": T,
    })

    Signature.KNOWN_ARGSPECS.update({
        "__Type.__call__": "(cls, *args, **kwargs)",
        "__Int.__ceil__": "(self)",
        "__Int.__floor__": "(self)",
        "__Float.__getformat__": "(typestr)",
        "__Dict.__getitem__": "(self, key)",
        "__Type.__instancecheck__": "(self, instance)",
        "__Bool.__init__": "(self, x)",
        "__Int.__init__": "(self, x=0)",
        "__Type.__prepare__": "(cls, name, bases, **kwds)",
        "__Int.__round__": "(self, ndigits=0)",
        "__Float.__round__": "(self, ndigits=0)",
        "__List.__reversed__": "(self)",
        "__Float.__setformat__": "(typestr, fmt)",
        "__Dict.__setitem__": "(self, key, value)",
        "__Set.add": "(self, value)",
        "__List.append": "(self, value)",
        "__Float.as_integer_ratio": "(self)",
        "__Int.bit_length": "(self)",
        "capitalize": "(self)",
        "casefold": "(self)",
        "__Bytes.center": "(self, width, fillbyte=b' ')",
        "__Unicode.center": "(self, width, fillchar=' ')",
        "clear": "(self)",
        "__Generator.close": "(self)",
        "conjugate": "(self)",
        "copy": "(self)",
        "count": "(self, x)",
        "__Bytes.count": "(self, sub, start=0, end=-1)",
        "__Unicode.count": "(self, sub, start=0, end=-1)",
        "__Bytes.decode": "(self, encoding='utf-8', errors='strict')",
        "__Property.deleter": "(self, func)",
        "__Set.difference": "(self, other)",
        "__FrozenSet.difference": "(self, other)",
        "__Set.difference_update": "(self, *others)",
        "__Set.discard": "(self, elem)",
        "__Unicode.encode": "(self, encoding='utf-8', errors='strict')",
        "endswith": "(self, suffix, start=0, end=-1)",
        "expandtabs": "(self, tabsize=8)",
        "__List.extend": "(self, iterable)",
        "find": "(self, sub, start=0, end=-1)",
        "__Unicode.format": "(self, *args, **kwargs)",
        "__Unicode.format_map": "(self, mapping)",
        "__Bool.from_bytes": "(bytes, byteorder, *, signed=False)",
        "__Int.from_bytes": "(bytes, byteorder, *, signed=False)",
        "__Float.fromhex": "(string)",
        "__Dict.get": "(self, key, d=Unknown())",
        "__Property.getter": "(self, func)",
        "hex": "(self)",
        "__List.insert": "(self, index, value)",
        "index": "(self, v)",
        "__Bytes.index": "(self, sub, start=0, end=-1)",
        "__Unicode.index": "(self, sub, start=0, end=-1)",
        "__Set.intersection": "(self, other)",
        "__FrozenSet.intersection": "(self, other)",
        "__Set.intersection_update": "(self, *others)",
        "isalnum": "(self)",
        "isalpha": "(self)",
        "isdecimal": "(self)",
        "isdigit": "(self)",
        "isidentifier": "(self)",
        "islower": "(self)",
        "isnumeric": "(self)",
        "isprintable": "(self)",
        "isspace": "(self)",
        "istitle": "(self)",
        "isupper": "(self)",
        "__Float.is_integer": "(self)",
        "__Set.isdisjoint": "(self, other)",
        "__FrozenSet.isdisjoint": "(self, other)",
        "__DictKeys.isdisjoint": "(self, other)",
        "__DictItems.isdisjoint": "(self, other)",
        "__Set.issubset": "(self, other)",
        "__FrozenSet.issubset": "(self, other)",
        "__Set.issuperset": "(self, other)",
        "__FrozenSet.issuperset": "(self, other)",
        "__Dict.items": "(self)",
        "__Bytes.join": "(self, iterable)",
        "__Unicode.join": "(self, iterable)",
        "__Dict.keys": "(self)",
        "lower": "(self)",
        "__Bytes.ljust": "(self, width, fillbyte=b' ')",
        "__Unicode.ljust": "(self, width, fillchar=' ')",
        "lstrip": "(self, chars)",
        "__Bytes.maketrans": "(from_, to)",
        "__Unicode.maketrans": "(x, y, z)",
        "__Type.mro": "(cls)",
        "__Bytes.partition": "(self, sep)",
        "__Unicode.partition": "(self, sep)",
        "__List.pop": "(self, index=-1)",
        "__Dict.pop": "(self, k, d=Unknown())",
        "__Set.pop": "(self)",
        "__Dict.popitem": "(self, k, d=Unknown())",
        "__List.remove": "(self, value)",
        "__Set.remove": "(self, elem)",
        "replace": "(self, old, new, count=-1)",
        "__List.reverse": "(self)",
        "rfind": "(self, sub, start=0, end=-1)",
        "rindex": "(self, sub, start=0, end=-1)",
        "__Bytes.rjust": "(self, width, fillbyte=b' ')",
        "__Unicode.rjust": "(self, width, fillchar=' ')",
        "__Bytes.rpartition": "(self, sep)",
        "__Unicode.rpartition": "(self, sep)",
        "rsplit": "(self, sep=None, maxsplit=-1)",
        "rstrip": "(self, chars=None)",
        "__Generator.send": "(self, value)",
        "__Dict.setdefault": "(self, k, d)",
        "__Property.setter": "(self, func)",
        "__List.sort": "(self)",
        "split": "(self, sep=None, maxsplit=-1)",
        "splitlines": "(self, keepends=False)",
        "strip": "(self, chars=None)",
        "startswith": "(self, prefix, start=0, end=-1)",
        "swapcase": "(self)",
        "__Set.symmetric_difference": "(self, other)",
        "__FrozenSet.symmetric_difference": "(self, other)",
        "__Set.symmetric_difference_update": "(self, *others)",
        "__Generator.throw": "(self, type, value=None, traceback=None)",
        "title": "(self)",
        "__Int.to_bytes": "(bytes, byteorder, *, signed=False)",
        "__Bytes.translate": "(self, table, delete=b'')",
        "__Unicode.translate": "(self, table)",
        "__Set.union": "(self, *others)",
        "__FrozenSet.union": "(self, *others)",
        "__Dict.update": "(self, d)",
        "__Set.update": "(self, *others)",
        "upper": "(self)",
        "__Dict.values": "(self)",
        "zfill": "(self, width)",
    })

    if sys.version[0] == '2':
        Signature.KNOWN_RESTYPES.update({
            "__BytesIterator.__next__": None,
            "__BytesIterator.next": "b''",
            "__UnicodeIterator.__next__": None,
            "__UnicodeIterator.next": "u''",
            "__Generator.send": "self.next()",
        })

        Signature.KNOWN_ARGSPECS.update({
            "__BytesIterator.next": "self",
            "__UnicodeIterator.next": "self",
        })



    def add_simple(name, doc, *members):
        mi = MemberInfo(name, MemberInfo.NO_VALUE)
        mi.documentation = doc
        mi.need_imports = (state.module_name,)
        mi.members.extend(members)
        state.members.append(mi)

    def add_literal(name, literal):
        state.members.append(MemberInfo(name, None, literal=literal))

    def add_type(alias, type_obj):
        mi = MemberInfo(type_obj.__name__, type_obj, module=builtins.__name__, alias=alias)
        state.members.append(mi)
        state.members.append(MemberInfo(alias, None, literal=mi.name))

    add_simple('__Unknown', '<unknown type>', MemberInfo("__name__", None, literal='"<unknown type>"'))
    add_simple('__NoneType', 'the type of the None object', MemberInfo.NO_VALUE)

    # NoneType and None are explicitly defined to avoid parser errors
    # because of None being a keyword.
    #add_literal('NoneType', '__NoneType')
    #add_literal('None', '__NoneType()')

    add_type('__Object', object)
    add_type('__Type', type)
    
    add_type('__Int', int)
    if type(bool()) is int:
        add_literal('__Bool', '__Int')
    else:
        add_type('__Bool', bool)

    try:
        long
    except NameError:
        add_literal('__Long', '__Int')
    else:
        add_type('__Long', long)

    add_type("__Float", float)
    add_type("__Complex", complex)

    add_type("__Tuple", tuple)
    add_type("__List", list)
    add_type("__Dict", dict)
    add_type("__Set", set)
    add_type("__FrozenSet", frozenset)

    if bytes is not str:
        add_type("__Bytes", bytes)
        add_type("__BytesIterator", type(iter(bytes())))
        add_type("__Unicode", str)
        add_type("__UnicodeIterator", type(iter(str())))
        add_literal("__Str", "__Unicode")
        add_literal("__StrIterator", "__UnicodeIterator")

    else:
        add_type("__Bytes", str)
        add_type("__BytesIterator", type(iter(str())))
        add_type("__Unicode", unicode)
        add_type("__UnicodeIterator", type(iter(unicode())))
        add_literal("__Str", "__Bytes")
        add_literal("__StrIterator", "__BytesIterator")

    add_type("__Module", type(inspect))
    add_type("__Function", type(add_simple))

    add_type("__BuiltinMethodDescriptor", type(object.__hash__))
    add_type("__BuiltinFunction", type(abs))
    add_type("__Generator", type((_ for _ in [])))
    add_type("__Property", property)
    add_type("__ClassMethod", classmethod)
    add_type("__StaticMethod", staticmethod)
    add_type("__Ellipsis", type(Ellipsis))
    add_type("__TupleIterator", type(iter(())))
    add_type("__ListIterator", type(iter([])))
    add_type("__DictKeys", type({}.keys()))
    add_type("__DictValues", type({}.values()))
    add_type("__DictItems", type({}.items()))
    add_type("__SetIterator", type(iter(set())))
    add_type("__CallableIterator", type(iter((lambda: None), None)))

    # Also write out the builtin module names here so that we cache them
    try:
        builtin_module_names = sys.builtin_module_names
    except AttributeError:
        pass
    else:
        add_literal('__builtin_module_names', '"' + ','.join(builtin_module_names) + '"')


if __name__ == '__main__':
    EXCLUDED_MEMBERS = ()

    outfile = sys.stdout
    if '-u8' in sys.argv:
        sys.argv.remove('-u8')
        try:
            b_outfile = outfile.buffer
        except AttributeError:
            warnings.warn("cannot enable UTF-8 output", InspectWarning)
            pass    # on Python 2, so hopefully everything is valid ASCII...
        else:
            import io
            outfile = io.TextIOWrapper(b_outfile, encoding='utf-8', errors='replace')

    if len(sys.argv) == 1:
        state = ScrapeState(builtins.__name__, builtins)
        add_builtin_objects(state)

        EXCLUDED_MEMBERS += ('None', 'False', 'True', '__debug__')
        if sys.version_info[0] == 2:
            EXCLUDED_MEMBERS += ('print',)

    elif len(sys.argv) >= 2:
        state = ScrapeState(sys.argv[1])

        if len(sys.argv) >= 3:
            state.initial_import(sys.argv[2])
        else:
            state.initial_import()

    state.collect_top_level_members()

    state.members[:] = [m for m in state.members if m.name not in EXCLUDED_MEMBERS]

    state.collect_second_level_members()

    state.dump(outfile)
    #import io
    #state.dump(io.BytesIO())
