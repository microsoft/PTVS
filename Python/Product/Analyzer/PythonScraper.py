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
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

"""
Generates information for supporting completion and analysis of Python code.

Outputs a pickled set of dictionaries.  The dictionaries are in the format:

top-level: module_table
        
module_table:
    {
        'members': member_table,
        'doc': doc_string,
    }

type_table:
    {
        'mro' :  type_list,
        'bases' : type_list,
        'members' : member_table,
        'doc' : doc_string,
        'is_hidden': bool,
        'builtin': bool
    }

member_table:
    { member_name : member_entry }

member_name: str

member_entry:
    {
        'kind': member_kind
        'value': member_value
        'version': version_spec     # optional
    }

member_kind: 'function' | 'funcref' | 'method' | 'property' | 'data' | 'type' | 'multiple' | 'typeref' | 'moduleref'
member_value: builtin_function | getset_descriptor | data | type_table | multiple_value | typeref | moduleref

moduleref:
    { 'module_name' : name }

typeref: 
    (
        module name,                # use '' to omit
        type name,
        type_list                   # index types; optional
    )

funcref:
    {
        'func_name' : fully-qualified function name
    }

multiple_value:
    { 'members' : (member_entry, ... ) }

builtin_function:
    {
        'doc': doc string,
        'overloads': overload_table,
        'builtin' : bool,
        'static': bool,
    }

overload_table:
    [overload, ...]

overload:
    {
        'args': args_info,
        'ret_type': type_list
    }

args_info:
    (arg_info, ...)

arg_info:
    {
        'type': type_list,
        'name': argument name,
        'default_value': repr of default value,
        'arg_format' : ('*' | '**')
    }

getset_descriptor:
    {
        'doc': doc string,
        'type': type_list
    }

data:
    { 'type': type_list }

type_list:
    [type_ref, ...]
"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

try:
    import cPickle as pickle
except ImportError:
    import pickle # Py3k
import datetime
import os
import subprocess
import sys
import traceback
import types

# The values in KNOWN_METHOD_TYPES and KNOWN_FUNCTION_TYPES are used when
# detecting the types of members.  The names are ('module.name', 'type.name')
# pairs, matching the contents of a typeref.
#
# If the type's name matches an item in KNOWN_METHOD_TYPES, the object is
# treated as a method descriptor.
#
# If the type's name matches an item in KNOWN_FUNCTION_TYPES, the object is
# treated as a method if defined on a type or a function otherwise.
KNOWN_METHOD_TYPES = frozenset(
    ('sip', 'methoddescriptor'),
)

KNOWN_FUNCTION_TYPES = frozenset(
    ('numpy', 'ufunc'),
)


# Safe member access methods because PyQt4 contains compiled types that crash
# on operations that should be completely safe, such as getattr() and dir().
# These should be used throughout when accessing members on type objects.
def safe_getattr(obj, attr, default):
    try:
        return getattr(obj, attr, default)
    except:
        return default

def safe_hasattr(obj, attr):
    try:
        return hasattr(obj, attr)
    except:
        return False

def safe_isinstance(obj, types):
    try:
        return isinstance(obj, types)
    except:
        return False

# safe_dir is imported from BuiltinScraper/IronPythonScraper
def safe_repr(obj):
    try:
        return repr(obj)
    except:
        return 'invalid object'

if sys.version_info[0] == 2:
    builtin_name = '__builtin__'
else:
    builtin_name = 'builtins'


TYPE_NAMES = {}

def types_to_typelist(iterable):
    return [type_to_typeref(type) for type in iterable]

def type_to_typelist(type):
    return [type_to_typeref(type)]

def typename_to_typeref(n1, n2=None):
    '''If both n1 and n2 are specified, n1 is the module name and n2 is the type name.
    If only n1 is specified, it is the type name.
    '''
    if n2 is None:
        name = ('', n1)
    elif n1 == '__builtin__':
        name = (builtin_name, n2)
    else:
        name = (n1, n2)
    return memoize_type_name(name)

def type_to_typeref(type):
    type_name = safe_getattr(type, '__name__', None)
    if not type_name:
        print('Cannot get type name of ' + safe_repr(type))
        type = object
        type_name = 'object'
    if safe_hasattr(type, '__module__'):
        if safe_getattr(type, '__module__', '') == '__builtin__':
            name = (builtin_name, type_name)
        else:
            name = (type.__module__, type_name)
    elif safe_isinstance(type, types.ModuleType):
        name = (type_name, '')
    else:
        name = ('', type_name)
    # memoize so when we pickle we can share refs
    return memoize_type_name(name)

def memoize_type_name(name):
    key = repr(name)
    if key in TYPE_NAMES:
        return TYPE_NAMES[key]
    TYPE_NAMES[key] = name
    return name

def maybe_to_typelist(type):
    if isinstance(type, list) and len(type) > 0 and isinstance(type[0], tuple) and len(type[0]) > 1 and type[0][1]:
        return type
    elif isinstance(type, tuple) and len(type) > 1 and type[1]:
        return [type]
    else:
        return type_to_typelist(type)

def generate_overload(ret_type, *args):
    '''ret_type is either a type suitable for type_to_typelist, or it is the result of
    one of the *_to_typelist() or *_to_typeref() functions.

    Each arg is a tuple of ('name', type or type_ref or type_list, '' or '*' or '**', 'default value string')
    The last two elements are optional, but the format is required if the default value
    is specified.
    '''
    res = { 'ret_type': maybe_to_typelist(ret_type) }
    arglist = []
    for arg in args:
        arglist.append({ 'name': arg[0], 'type': maybe_to_typelist(arg[1]) })
        if len(arg) >= 3 and arg[2]:
            arglist[-1]['arg_format'] = arg[2]
        if len(arg) >= 4:
            arglist[-1]['default_value'] = arg[3]
    res['args'] = tuple(arglist)
    return res

if sys.platform == "cli":
    # provides extra type info when generating against IronPython which can be
    # used w/ CPython completions
    import IronPythonScraper as BuiltinScraper 
else:
    import BuiltinScraper

def generate_builtin_function(function, is_method=False):
    function_table = {}
    
    func_doc = safe_getattr(function, '__doc__', None)
    if safe_isinstance(func_doc, str):
        function_table['doc'] = func_doc

    function_table['overloads'] = BuiltinScraper.get_overloads(function, is_method)
    
    return function_table
    
def generate_getset_descriptor(descriptor):
    descriptor_table = {}
    
    desc_doc = safe_getattr(descriptor, '__doc__', None)
    if safe_isinstance(desc_doc, str):
        descriptor_table['doc'] = desc_doc
    
    desc_type = BuiltinScraper.get_descriptor_type(descriptor)
    descriptor_table['type'] = type_to_typelist(desc_type)
    
    return descriptor_table

NoneType = type(None)
slot_wrapper_type = type(int.__add__)
method_descriptor_type = type(str.center)
member_descriptor_type = type(property.fdel)
try:
    getset_descriptor_type = type(file.closed)
except NameError:
    getset_descriptor_type = type(Exception.args) # Py3k, no file
class_method_descriptor_type = type(datetime.date.__dict__['today'])
class OldStyleClass: pass
OldStyleClassType = type(OldStyleClass)

def generate_member_table(obj, is_hidden=False, from_type=False, extra_types=None):
    '''Generates a table of members of `obj`.

    `is_hidden` determines whether all the members are hidden from IntelliSense.

    `from_type` determines whether method descriptors are retained (True) or
    ignored (False).

    `extra_types` is a sequence of ``(type_name, object)`` pairs to add as types
    to this table. These types are always hidden.
    '''

    sentinel = object()
    members = []
    for name in BuiltinScraper.safe_dir(obj):
        member = safe_getattr(obj, name, sentinel)
        if member is not sentinel:
            members.append((name, member))

    dependencies = {}
    table = {}
    if extra_types:
        for name, member in extra_types:
            member_kind, member_value = generate_member(member, is_hidden = True, from_type = from_type)
            if member_kind == 'typeref':
                actual_name = type_to_typeref(member)
                if actual_name not in dependencies:
                    dependencies[actual_name] = member
            table[name] = { 'kind': member_kind, 'value': member_value }

    for name, member in members:
        member_kind, member_value = generate_member(member, is_hidden, from_type)
        if member_kind == 'typeref':
            actual_name = type_to_typeref(member)
            if actual_name not in dependencies:
                dependencies[actual_name] = member
        table[name] = { 'kind': member_kind, 'value': member_value }

    if dependencies:
        obj_mod, obj_name = type_to_typeref(obj)
        def needs_type_info(other_mod, other_name):
            if obj_mod != other_mod:
                if other_mod == builtin_name:
                    # Never embed builtins (unless obj_mod is builtins, in
                    # which case the first comparison failed)
                    return False
                
                # Always embed external types
                return True

            # We know obj_mod == other_mod at this point

            if not obj_name:
                # Writing ourselves in the expected place
                return True
            elif obj_name.startswith(other_name + '.'):
                # Always write references to outer types
                return False
            elif other_name and other_name.startswith(obj_name + '.'):
                # Always write type info for inner types
                return True

            # Otherwise, use a typeref
            return False

        for (dep_mod, dep_name), dep_obj in dependencies.items():
            if needs_type_info(dep_mod, dep_name):
                table[dep_name] = {
                    'kind': 'type',
                    'value': generate_type(dep_obj, is_hidden = dep_name not in table),
                }

    return table

def generate_member(obj, is_hidden=False, from_type=False):
    try:
        # Already handling all exceptions here, so don't worry about using the
        # 'safe_*' functions.

        if isinstance(obj, (types.BuiltinFunctionType, class_method_descriptor_type)):
            return 'function', generate_builtin_function(obj)
        elif isinstance(obj, types.FunctionType):
            # PyPy - we see plain old Python functions in addition to built-ins
            return 'method' if from_type else 'function', generate_builtin_function(obj, from_type)
        elif isinstance(obj, (type, OldStyleClassType)):
            return 'typeref', type_to_typelist(obj)
        elif isinstance(obj, (types.BuiltinMethodType, slot_wrapper_type, method_descriptor_type)):
            return 'method', generate_builtin_function(obj, True)
        elif isinstance(obj, (getset_descriptor_type, member_descriptor_type)):
            return 'property', generate_getset_descriptor(obj)

        # Check whether we recognize the type name as one that does not respond
        # correctly to isinstance checks.
        type_name = type_to_typeref(type(obj))

        if type_name in KNOWN_METHOD_TYPES:
            return 'method', generate_builtin_function(obj, True)

        if type_name in KNOWN_FUNCTION_TYPES:
            return 'method' if from_type else 'function', generate_builtin_function(obj, from_type)

        # Callable objects with a docstring that provides us with at least one
        # overload will be treated as functions rather than data.
        if safe_hasattr(obj, '__call__'):
            try:
                info = generate_builtin_function(obj, from_type)
                if info and info['overloads']:
                    return 'method' if from_type else 'function', info
            except:
                pass
    except:
        # Some compiled types fail here, so catch everything and treat the
        # object as data.
        traceback.print_exc()
        print('Treating type as data')

    # We don't have any special handling for this object type, so treat it as
    # a constant.
    return 'data', generate_data(obj)


if sys.version > '3.':
    str_types = (str, bytes)
else:
    str_types = (str, unicode)


def generate_type_new(type_obj, obj):
    if safe_isinstance(obj, (types.BuiltinFunctionType, class_method_descriptor_type)):
        function_info = generate_builtin_function(obj)

        new_overloads = BuiltinScraper.get_new_overloads(type_obj, obj)
        if new_overloads is not None:
            # replace overloads with better version if available
            function_info['overloads'] = new_overloads
            return 'function', function_info

    if safe_getattr(obj, '__doc__', '') == 'T.__new__(S, ...) -> a new object with type S, a subtype of T':
        doc_str = safe_getattr(type_obj, '__doc__', None)
        if not safe_isinstance(doc_str, str_types):
            doc_str = ''
        return (
            'function',
            {
                'doc': doc_str,
                'overloads' : [{'doc': doc_str, 'args': [{'arg_format': '*', 'name': 'args'}] }]
            }
        )

    return generate_member(obj)

def oldstyle_mro(type_obj, res):
    type_bases = safe_getattr(type_obj, '__bases__', None)

    if not type_bases:
        return res

    for base in type_bases:
        if base not in res:
            res.append(type_to_typeref(base))

    for base in type_bases:
        oldstyle_mro(base, res)
    return res

def generate_type(type_obj, is_hidden=False):
    type_table = {}
    
    type_mro = safe_getattr(type_obj, '__mro__', None)
    if type_mro:
        type_table['mro'] = types_to_typelist(type_mro)
    else:
        type_table['mro'] = oldstyle_mro(type_obj, [])
    
    type_bases = safe_getattr(type_obj, '__bases__', None)
    if type_bases:
        type_table['bases'] = types_to_typelist(type_bases)
    
    type_doc = safe_getattr(type_obj, '__doc__', None)
    if safe_isinstance(type_doc, str):
        type_table['doc'] = type_doc
    
    if is_hidden:
        type_table['is_hidden'] = True
    
    
    type_table['members'] = member_table = generate_member_table(type_obj)

    if type_obj is object:
        member_table['__new__'] = {
            'kind' : 'function',
            'value': { 'overloads': [generate_overload(object, ('cls', type))] }
        }
    elif '__new__' not in member_table:
        member_table['__new__'] = generate_type_new(type_obj, 
            safe_getattr(type_obj, '__new__', object.__new__),)
    
    if ('__getattribute__' in member_table and 
        type_obj is not object and 
        safe_isinstance(safe_getattr(type_obj, '__getattribute__', None), slot_wrapper_type)):
        # skip __getattribute__ on types other than object if it's just a slot
        # wrapper.
        del member_table['__getattribute__']

    return type_table

def generate_data(data_value):
    data_table = {}
    
    data_type = type(data_value)
    data_table['type'] = type_to_typelist(data_type)
    
    return data_table

def lookup_module(module_name):
    try:
        module = __import__(module_name)
    except:
        return None
    if '.' in module_name:
        for name in module_name.split('.')[1:]:
            module = safe_getattr(module, name, None)
            if not module:
                module = sys.modules[module_name]
    
    return module

def generate_module(module, extra_types=None):
    module_table = {}
    
    module_doc = safe_getattr(module, '__doc__', None)
    if safe_isinstance(module_doc, str):
        module_table['doc'] = module_doc
    
    module_table['members'] = generate_member_table(module, extra_types = extra_types)
    
    return module_table


def get_module_members(module):
    """returns an iterable which gives the names of the module which should be exposed"""
    module_all = safe_getattr(module, '__all__', None)
    if module_all:
        return frozenset(module_all)

    return BuiltinScraper.safe_dir(module)

def generate_builtin_module():
    extra_types = {}
    extra_types['object'] = type(object)
    extra_types['function'] = types.FunctionType
    extra_types['builtin_function'] = types.BuiltinFunctionType
    extra_types['builtin_method_descriptor'] = types.BuiltinMethodType
    extra_types['generator'] = types.GeneratorType
    extra_types['NoneType'] = NoneType
    extra_types['ellipsis'] = type(Ellipsis)
    extra_types['module_type'] = types.ModuleType
    if sys.version_info[0] == 2:
        extra_types['dict_keys'] = type({}.iterkeys())
        extra_types['dict_values'] = type({}.itervalues())
        extra_types['dict_items'] = type({}.iteritems())
    else:
        extra_types['dict_keys'] = type({}.keys())
        extra_types['dict_values'] = type({}.values())
        extra_types['dict_items'] = type({}.items())
    
    extra_types['list_iterator'] = type(iter(list()))
    extra_types['tuple_iterator'] = type(iter(tuple()))
    extra_types['set_iterator'] = type(iter(set()))
    extra_types['str_iterator'] = type(iter(""))
    if sys.version_info[0] == 2:
        extra_types['bytes_iterator'] = type(iter(""))
        extra_types['unicode_iterator'] = type(iter(unicode()))
    else:
        extra_types['bytes_iterator'] = type(iter(bytes()))
        extra_types['unicode_iterator'] = type(iter(""))
    extra_types['callable_iterator'] = type(iter(lambda: None, None))

    res = generate_module(lookup_module(builtin_name), extra_types = extra_types.items())

    if res and 'members' in res and 'object' in res['members']:
        assert res['members']['object']['kind'] == 'type', "Unexpected: " + repr(res['members']['object'])
        res['members']['object']['value']['doc'] = "The most base type"

    return res


def merge_type(baseline_type, new_type):
    if 'doc' not in new_type and 'doc' in baseline_type:
        new_type['doc'] = baseline_type['doc']

    merge_member_table(baseline_type['members'], new_type['members'])
    
    return new_type

def merge_function(baseline_func, new_func):
    new_func['overloads'].extend(baseline_func['overloads'])
    return new_func

def merge_property(baseline_prop, new_prop):
    new_prop['type'].extend(baseline_prop['type'])
    return new_prop

def merge_data(baseline_data, new_data):
    new_data['type'].extend(baseline_data['type'])
    return new_data

def merge_method(baseline_method, new_method):
    if baseline_method.get('overloads') is not None:
        if new_method.get('overloads') is None:
            new_method['overloads'] = baseline_method['overloads']
        else:
            new_method['overloads'].extend(baseline_method['overloads'])
    
    if 'doc' in baseline_method and 'doc' not in new_method:
        new_method['doc'] = baseline_method['doc']
        #print 'new doc string'

    return new_method

_MERGES = {'type' : merge_type,
          'function': merge_method,
          'property': merge_property,
          'data': merge_data,
          'method': merge_method}

def merge_member_table(baseline_table, new_table):
    for name, member_table in new_table.items():
        base_member_table = baseline_table.get(name, None)
        kind = member_table['kind']
        
        if base_member_table is not None and base_member_table['kind'] == kind:
            merger = _MERGES.get(kind, None)
            if merger is not None:
                member_table['value'] = merger(base_member_table['value'], member_table['value'])
            #else:
            #    print('unknown kind')
        #elif base_member_table is not None:
        #    print('kinds differ', kind, base_member_table['kind'], name)
    
InitMethodEntry = {
    'kind': 'method',
    'value': {
        'doc': 'x.__init__(...) initializes x; see help(type(x)) for signature',
        'overloads': [generate_overload(NoneType, ('self', object), ('args', object, '*'), ('kwargs', object, '**'))]
    }
}

NewMethodEntry = {
    'kind': 'function',
    'value': {
        'doc': 'T.__new__(S, ...) -> a new object with type S, a subtype of T',
        'overloads': [generate_overload(object, ('self', object), ('args', object, '*'), ('kwargs', object, '**'))]
    }
}

ReprMethodEntry = {
    'kind': 'method',
    'value': {
        'doc': 'x.__repr__() <==> repr(x)',
        'overloads': [generate_overload(str, ('self', object))]
    }
}



def _sre_post_fixer(mod):
    if sys.platform == 'cli':
        # IronPython doesn't have a real _sre module
        return mod
        
    mod['members']['compile'] = {
        'kind': 'function',
        'value': {
            'overloads': [generate_overload(typename_to_typeref('_sre', 'SRE_Pattern'),
                ('pattern', object), ('flags', object), ('code', object), ('groups', object),
                ('groupindex', object), ('indexgroup', object))],
            'builtin' : True,
            'static': True,
        }
    }
    mod['members']['SRE_Match'] = {
        'kind': 'type',
        'value': {
            'bases': [(builtin_name, 'object')],
            'doc': 'SRE_Match(m: Match, pattern: SRE_Pattern, text: str)\r\nRE_Match(m: Match, pattern: SRE_Pattern, text: str, pos: int, endpos: int)\r\n',
            'members': {
                '__new__': {
                    'kind': 'function',
                    'value': {
                        'doc': '__new__(cls: type, m: Match, pattern: SRE_Pattern, text: str)\r\n__new__(cls: type, m: Match, pattern: SRE_Pattern, text: str, pos: int, endpos: int)\r\n',
                        'overloads': None
                    }
                },
                'end': {
                    'kind': 'method',
                    'value': {
                        'doc': 'end(self: SRE_Match, group: object) -> int\r\nend(self: SRE_Match) -> int\r\n',
                        'overloads': [
                            generate_overload(int, ('self', typename_to_typeref('re', 'SRE_Match'))),
                            generate_overload(int, ('self', typename_to_typeref('re', 'SRE_Match')), ('group', object))
                        ],
                    }
                },
                'endpos': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: endpos(self: SRE_Match) -> int\r\n\r\n',
                        'type': type_to_typelist(int)
                    }
                },
                'expand': {
                    'kind': 'method',
                    'value': {
                        'doc': 'expand(self: SRE_Match, template: object) -> str\r\n',
                        'overloads': [generate_overload(str, ('self', typename_to_typeref('re', 'SRE_Match')), ('template', object))],
                    }
                },
                'group': {
                    'kind': 'method',
                    'value': {
                        'doc': 'group(self: SRE_Match) -> str\r\ngroup(self: SRE_Match, index: object) -> str\r\ngroup(self: SRE_Match, index: object, *additional: Array[object]) -> object\r\n',
                        'overloads': [
                            generate_overload(str, ('self', typename_to_typeref('re', 'SRE_Match'))),
                            generate_overload(str, ('self', typename_to_typeref('re', 'SRE_Match')), ('index', object)),
                            generate_overload(object, ('self', typename_to_typeref('re', 'SRE_Match')), ('index', object), ('additional', tuple, '*'))
                        ],
                    },
                },
                'groupdict': {
                    'kind': 'method',
                    'value': {
                        'doc': 'groupdict(self: SRE_Match, value: object) -> dict (of str to object)\r\ngroupdict(self: SRE_Match, value: str) -> dict (of str to str)\r\ngroupdict(self: SRE_Match) -> dict (of str to str)\r\n',
                        'overloads': [
                            generate_overload(dict, ('self', typename_to_typeref('re', 'SRE_Match')), ('value', types_to_typelist([object, str]))),
                            generate_overload(dict, ('self', typename_to_typeref('re', 'SRE_Match')))
                        ],
                    }
                },
                'groups': {
                    'kind': 'method',
                    'value': {
                        'doc': 'groups(self: SRE_Match, default: object) -> tuple\r\ngroups(self: SRE_Match) -> tuple (of str)\r\n',
                        'overloads': [
                            generate_overload(tuple, ('self', typename_to_typeref('re', 'SRE_Match')), ('default', object)),
                            generate_overload(tuple, ('self', typename_to_typeref('re', 'SRE_Match')))
                        ],
                    }
                },
                'lastgroup': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: lastgroup(self: SRE_Match) -> str\r\n\r\n',
                        'type': type_to_typelist(str)
                    }
                },
                'lastindex': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: lastindex(self: SRE_Match) -> object\r\n\r\n',
                        'type': type_to_typelist(object)
                    }
                },
                'pos': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: pos(self: SRE_Match) -> int\r\n\r\n',
                        'type': type_to_typelist(int)
                    }
                },
                're': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: re(self: SRE_Match) -> SRE_Pattern\r\n\r\n',
                        'type': [typename_to_typeref('re', 'SRE_Pattern')]
                    }
                },
                'regs': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: regs(self: SRE_Match) -> tuple\r\n\r\n',
                        'type': type_to_typelist(tuple)
                    }
                },
                'span': {
                    'kind': 'method',
                    'value': {
                        'doc': 'span(self: SRE_Match, group: object) -> tuple (of int)\r\nspan(self: SRE_Match) -> tuple (of int)\r\n',
                        'overloads': [
                            generate_overload(tuple, ('self', typename_to_typeref('re', 'SRE_Match'))),
                            generate_overload(tuple, ('self', typename_to_typeref('re', 'SRE_Match')), ('group', object))
                        ]
                    }
                },
                'start': {
                    'kind': 'method',
                    'value': {
                        'doc': 'start(self: SRE_Match, group: object) -> int\r\nstart(self: SRE_Match) -> int\r\n',
                        'overloads': [
                            generate_overload(int, ('self', typename_to_typeref('re', 'SRE_Match'))),
                            generate_overload(int, ('self', typename_to_typeref('re', 'SRE_Match')), ('group', object))
                        ]
                    }
                },
                'string': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: string(self: SRE_Match) -> str\r\n\r\n',
                        'type': type_to_typelist(str)
                    }
                }
            },
            'mro': [typename_to_typeref('re', 'SRE_Match'), type_to_typeref(object)]
        }
    }
    mod['members']['SRE_Pattern'] = {
            'kind': 'type',
            'value': {'bases': [type_to_typeref(object)],
            'doc': '',
            'members': {
                '__eq__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__eq__(y) <==> x==y',
                        'overloads': [generate_overload(bool, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('obj', object))],
                    }
                },
                '__ne__': {
                    'kind': 'method',
                    'value': {
                        'doc': '__ne__(x: object, y: object) -> bool\r\n',
                        'overloads': [generate_overload(bool, ('x', object), ('y', object))]
                    }
                },
                '__new__': NewMethodEntry,
                'findall': {
                    'kind': 'method',
                    'value': {
                        'doc': 'findall(self: SRE_Pattern, string: object, pos: int, endpos: object) -> object\r\nfindall(self: SRE_Pattern, string: str, pos: int) -> object\r\nfindall(self: SRE_Pattern, string: str) -> object\r\n',
                        'overloads': [generate_overload(bool, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('pos', int, '', '0'), ('endpos', object, '', 'None'))]
                    }
                },
                'finditer': {
                    'kind': 'method',
                    'value': {
                        'doc': 'finditer(self: SRE_Pattern, string: object, pos: int, endpos: int) -> object\r\nfinditer(self: SRE_Pattern, string: object, pos: int) -> object\r\nfinditer(self: SRE_Pattern, string: object) -> object\r\n',
                        'overloads': [generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('pos', int, '', '0'), ('endpos', int, '', 'None'))]
                    }
                },
                'flags': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: flags(self: SRE_Pattern) -> int\r\n\r\n',
                        'type': type_to_typelist(int)
                    }
                },
                'groupindex': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: groupindex(self: SRE_Pattern) -> dict\r\n\r\n',
                        'type': type_to_typelist(dict)
                    }
                },
                'groups': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: groups(self: SRE_Pattern) -> int\r\n\r\n',
                        'type': type_to_typelist(int)
                    }
                },
                'match': {
                    'kind': 'method',
                    'value': {
                        'doc': 'match(self: SRE_Pattern, text: object, pos: int, endpos: int) -> RE_Match\r\nmatch(self: SRE_Pattern, text: object, pos: int) -> RE_Match\r\nmatch(self: SRE_Pattern, text: object) -> RE_Match\r\n',
                        'overloads': [generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('text', str), ('pos', int, '', '0'), ('endpos', int, '', 'None'))],
                    }
                },
                'pattern': {
                    'kind': 'property',
                    'value': {
                        'doc': 'Get: pattern(self: SRE_Pattern) -> str\r\n\r\n',
                        'type': type_to_typelist(str)
                    }
                },
                'search': {
                    'kind': 'method',
                    'value': {
                        'doc': 'search(self: SRE_Pattern, text: object, pos: int, endpos: int) -> RE_Match\r\nsearch(self: SRE_Pattern,text: object, pos: int) -> RE_Match\r\nsearch(self: SRE_Pattern, text: object) -> RE_Match\r\n',
                        'overloads': [generate_overload(typename_to_typeref('_sre', 'RE_Match'), ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('text', str), ('pos', int, '', '0'), ('endpos', int, '', 'None'))]
                    }
                },
                'split': {
                    'kind': 'method',
                    'value': {
                        'doc': 'split(self: SRE_Pattern, string: object, maxsplit: int) -> list (of str)\r\nsplit(self: SRE_Pattern, string: str) -> list (of str)\r\n',
                        'overloads': [generate_overload(list, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('maxsplit', int, '', 'None'))]
                    }
                },
                'sub': {
                    'kind': 'method',
                    'value': {
                        'doc': 'sub(self: SRE_Pattern, repl: object, string: object, count: int) -> str\r\nsub(self: SRE_Pattern, repl: object, string: object) -> str\r\n',
                        'overloads': [generate_overload(str, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('repl', object), ('string', str), ('count', int, '', 'None'))]
                    }
                },
                'subn': {
                    'kind': 'method',
                    'value': {'doc': 'subn(self: SRE_Pattern, repl: object, string: object, count: int) -> object\r\nsubn(self: SRE_Pattern, repl: object, string: str) -> object\r\n',
                        'overloads': [generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('repl', object), ('string', str), ('count', int, '', 'None'))]
                    }
                },
            },
            'mro': [typename_to_typeref('_sre', 'SRE_Pattern'),
                    type_to_typeref(object)]
        }
    }

    return mod


# fixers which run on the newly generated file, not on the baseline file.
post_module_fixers = {
    '_sre' : _sre_post_fixer,
}

def merge_with_baseline(mod_name, baselinepath, final):
    baseline_file = os.path.join(baselinepath, mod_name + '.idb')
    if os.path.exists(baseline_file):
        print(baseline_file)
        f = open(baseline_file, 'rb')
        baseline = pickle.load(f)
        f.close()

        #import pprint
        #pp = pprint.PrettyPrinter()
        #pp.pprint(baseline['members'])
        fixer = post_module_fixers.get(mod_name, None)
        if fixer is not None:
            final = fixer(final)

        merge_member_table(baseline['members'], final['members'])

    return final

def write_analysis(out_filename, analysis):
    out_file = open(out_filename + '.idb', 'wb')
    saved_analysis = pickle.dumps(analysis, 2)
    if sys.platform == 'cli':
        # work around strings always being unicode on IronPython, we fail to
        # write back out here because IronPython doesn't like the non-ascii
        # characters in the unicode string
        import System
        data = System.Array.CreateInstance(System.Byte, len(saved_analysis))
        for i, v in enumerate(saved_analysis):
            try:
                data[i] = ord(v)
            except:
                pass
            
        saved_analysis = data
    out_file.write(saved_analysis)
    out_file.close()

    # write a list of members which we can load to check for member existance
    out_file = open(out_filename + '.idb.$memlist', 'wb')
    for member in sorted(analysis['members']):
        if sys.version_info >= (3, 0):
            out_file.write((member + '\n').encode('utf8'))
        else:
            out_file.write(member + '\n')

    out_file.close()

def write_module(mod_name, outpath, analysis):
    write_analysis(os.path.join(outpath, mod_name), analysis)



if __name__ == "__main__":
    outpath = sys.argv[1]
    if len(sys.argv) > 2:
        baselinepath = sys.argv[2]
    else:
        baselinepath = None
    
    res = generate_builtin_module()
    if not res:
        raise RuntimeError("Unable to scrape builtins")
    res = merge_with_baseline(builtin_name, baselinepath, res)
    
    write_module(builtin_name, outpath, res)
    
    for mod_name in sys.builtin_module_names:
        if (mod_name == builtin_name or
            mod_name == '__main__' or
            not BuiltinScraper.should_include_module(mod_name)):
            continue
        
        res = generate_module(lookup_module(mod_name))
        if res is not None:
            try:
                res = merge_with_baseline(mod_name, baselinepath, res)

                write_module(mod_name, outpath, res)
            except ValueError:
                pass
