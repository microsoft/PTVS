 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

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
    }

member_kind: 'function' | 'method' | 'property' | 'data' | 'type' | 'multiple' | 'typeref' | 'moduleref'
member_value: builtin_function | getset_descriptor | data | type_table | multiple_value | typeref | moduleref

moduleref:
    { 'module_name' : name }

typeref: 
    {
        'module_name' : module name,        # optional
        'type_name' : type name,
        'index_types' : type_list           # optional
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

try:
    import cPickle as pickle
except ImportError:
    import pickle # Py3k

import datetime
import os
import subprocess
import sys
import types

# The version number should match the value of PythonTypeDatabase.CurrentVersion in
#  \Release\Product\Python\Analysis\Interpreter\PythonTypeDatabase.cs
#
# To update the baseline DB:
#  1. Check out all files in Product\Python\PythonTools\CompletionDB
#  2. Run ipy.exe PythonScraper.py ...\CompletionDB
#  3. Undo unnecessary edits (tfpt uu) and delete new files (tfpt treeclean ...\CompletionDB)
#
CURRENT_DATABASE_VERSION = '21'

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
        name = { 'type_name': n1 }
    elif n1 == '__builtin__':
        name = { 'module_name': builtin_name, 'type_name': n2 }
    else:
        name = { 'module_name': n1, 'type_name': n2 }
    return memoize_type_name(name)

def type_to_typeref(type):
    if hasattr(type, '__module__'):
        if type.__module__ == '__builtin__':
            name = { 'module_name': builtin_name, 'type_name': type.__name__ }
        else:
            name = { 'module_name': type.__module__, 'type_name': type.__name__ }
    else:
        name = { 'type_name': type.__name__ }
    # memoize so when we pickle we can share refs
    return memoize_type_name(name)

def memoize_type_name(name):
    key = repr(name)
    if key in TYPE_NAMES:
        return TYPE_NAMES[key]
    TYPE_NAMES[key] = name
    return name

def maybe_to_typelist(type):
    if isinstance(type, list) and len(type) > 0 and isinstance(type[0], dict) and 'type_name' in type[0]:
        return type
    elif isinstance(type, dict) and 'type_name' in type:
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
    # provides extra type info when generating against IronPython which can be used w/ CPython completions
    import IronPythonScraper as BuiltinScraper 
else:
    import BuiltinScraper


def generate_builtin_function(function, is_method = False):
    function_table = {}
    
    try:
        if isinstance(function.__doc__, str):
            function_table['doc'] = function.__doc__
    except:
        # IronPython can throw here if an assembly load fails
        pass

    function_table['overloads'] = BuiltinScraper.get_overloads(function, is_method)
    
    return function_table
    
def generate_getset_descriptor(descriptor):
    descriptor_table = {}
    
    if isinstance(descriptor.__doc__, str):
        descriptor_table['doc'] = descriptor.__doc__
    
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

def generate_member(obj, is_hidden=False, from_type = False):
    member_table = {}
    
    if isinstance(obj, (types.BuiltinFunctionType, class_method_descriptor_type)):
        member_table['kind'] = 'function'
        member_table['value'] = generate_builtin_function(obj)
    elif isinstance(obj, types.FunctionType):
        # PyPy - we see plain old Python functions in addition to built-ins
        if from_type:
            member_table['kind'] = 'method'
        else:
            member_table['kind'] = 'function'

        member_table['value'] = generate_builtin_function(obj, from_type)
    elif isinstance(obj, (type, OldStyleClassType)):
        member_table['kind'] = 'type'
        member_table['value'] = generate_type(obj, is_hidden=is_hidden)
    elif isinstance(obj, (types.BuiltinMethodType, slot_wrapper_type, method_descriptor_type)):
        member_table['kind'] = 'method'
        member_table['value'] = generate_builtin_function(obj, True)
    elif isinstance(obj, (getset_descriptor_type, member_descriptor_type)):
        member_table['kind'] = 'property'
        member_table['value'] = generate_getset_descriptor(obj)
    else:
        member_table['kind'] = 'data'
        member_table['value'] = generate_data(obj)
        
    return member_table
    

if sys.version > '3.':
    str_types = (str, bytes)
else:
    str_types = (str, unicode)


def generate_type_new(type_obj, obj):
    if isinstance(obj, (types.BuiltinFunctionType, class_method_descriptor_type)):
        member_table = {}
        member_table['kind'] = 'function'
        member_table['value'] = function_info = generate_builtin_function(obj)

        new_overloads = BuiltinScraper.get_new_overloads(type_obj, obj)
        if new_overloads is not None:
            # replace overloads with better version if available
            function_info['overloads'] = new_overloads
            return member_table
    if obj.__doc__ == 'T.__new__(S, ...) -> a new object with type S, a subtype of T':
        doc_str = type_obj.__doc__
        if not isinstance(doc_str, str_types):
            doc_str = ''
        return {
                'kind' : 'function',
                'value' : {
                         'doc': doc_str,
                         'overloads' : [
                                 {
                                  'doc': doc_str, 
                                  'args': [{'arg_format': '*', 'name': 'args'}]
                                 }
                        ]                         
                }
        }
    return generate_member(obj)

def oldstyle_mro(type_obj, res):
    for base in type_obj.__bases__:
        if base not in res:
            res.append(type_to_typeref(base))

    for base in type_obj.__bases__:
        oldstyle_mro(base, res)
    return res

def generate_type(type_obj, is_hidden=False):
    type_table = {}
    
    if hasattr(type_obj, '__mro__'):
        type_table['mro'] = types_to_typelist(type_obj.__mro__)
    else:
        type_table['mro'] = oldstyle_mro(type_obj, [])

    type_table['bases'] = types_to_typelist(type_obj.__bases__)
    type_table['members'] = members_table = {}
    
    if isinstance(type_obj.__doc__, str):
         type_table['doc'] = type_obj.__doc__

    if is_hidden:
        type_table['is_hidden'] = True
    
    found_new = False        
    for member in type_obj.__dict__:
        if member == '__new__':
            found_new = True
            if type_obj is object:
                members_table[member] = {
                    'kind' : 'function',
                    'value': { 'overloads': [generate_overload(object, ('cls', type))] }
                }
            else:
                members_table[member] = generate_type_new(type_obj, type_obj.__dict__[member])
        elif member == '__getattribute__' and type(type_obj.__dict__[member]) is slot_wrapper_type and type_obj is not object:
            # skip __getattribute__ on types other than object if it's just a slot wrapper.
            continue
        else:
            members_table[member] = generate_member(type_obj.__dict__[member], from_type = True)

    if not found_new and hasattr(type_obj, '__new__'):
        # you'd expect all types to have __new__, but twisted.internet.iocpreactor.iocpsupport.Event
        # is missing it for some reason, so we'll fallback to object.__new__
        members_table['__new__'] = generate_type_new(type_obj, 
                                                     getattr(type_obj, '__new__', object.__new__))

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
            try:
                module = getattr(module, name)
            except AttributeError:
                module = sys.modules[module_name]
    
    return module

def generate_module(module):
    if not isinstance(module, type(sys)):
        return None
    
    all_members = {}
    module_table = {'members': all_members}
        
    if isinstance(module.__doc__, str):
        module_table['doc'] = module.__doc__

    for attr in module.__dict__:
        attr_value = module.__dict__[attr]
        all_members[attr] = generate_member(attr_value)
            
    return module_table


def get_module_members(module):
    """returns an iterable which gives the names of the module which should be exposed"""
    if hasattr(module, '__all__'):
        return module.__all__

    return module.__dict__

def generate_builtin_module():
    res  = generate_module(lookup_module(builtin_name))

    # add some hidden members we need to support resolving to
    members_table = res['members']
    
    members_table['function'] = generate_member(types.FunctionType, is_hidden=True)
    members_table['builtin_function'] = generate_member(types.BuiltinFunctionType, is_hidden=True)
    members_table['builtin_method_descriptor'] = generate_member(types.BuiltinMethodType, is_hidden=True)
    members_table['generator'] = generate_member(types.GeneratorType, is_hidden=True)
    members_table['NoneType'] = generate_member(NoneType, is_hidden=True)
    members_table['ellipsis'] = generate_member(type(Ellipsis), is_hidden=True)
    members_table['module_type'] = generate_member(types.ModuleType, is_hidden=True)
    if sys.version_info[0] == 2:
        members_table['dict_keys'] = generate_member(type({}.iterkeys()), is_hidden=True)
        members_table['dict_values'] = generate_member(type({}.itervalues()), is_hidden=True)
        members_table['dict_items'] = generate_member(type({}.iteritems()), is_hidden=True)
    else:
        members_table['dict_keys'] = generate_member(type({}.keys()), is_hidden=True)
        members_table['dict_values'] = generate_member(type({}.values()), is_hidden=True)
        members_table['dict_items'] = generate_member(type({}.items()), is_hidden=True)
    
    members_table['object']['value']['doc'] = "The most base type"
    members_table['list_iterator'] = generate_member(type(iter(list())), is_hidden=True)
    members_table['tuple_iterator'] = generate_member(type(iter(tuple())), is_hidden=True)
    members_table['set_iterator'] = generate_member(type(iter(set())), is_hidden=True)
    members_table['str_iterator'] = generate_member(type(iter("")), is_hidden=True)
    if sys.version_info[0] == 2:
        members_table['bytes_iterator'] = generate_member(type(iter("")), is_hidden=True)
    else:
        members_table['bytes_iterator'] = generate_member(type(iter(bytes())), is_hidden=True)
    members_table['callable_iterator'] = generate_member(type(iter(lambda: None, None)), is_hidden=True)

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
    

##############################################################################
# Fixers for multi-targetting of different Python versions w/ the default 
# completion DB.  These add version tags based upon the data generated by 
# VersionDiff.py.  They also add members which are new in 3.x - those are
# all explicitly hard coded values.  The combination of this is that we get
# a default completion DB that can handle different versions of Python.  Then
# the default DB is used as a starting point for scanning the actual installed
# distribution and coming up w/ the real live completion members.

def mark_maximum_version(mod, items, version = '2.7'):
    for two_only in items:
        if two_only in mod['members']:
            # if we're running on a later version for the real scrap we may not have the member
            mod['members'][two_only]['value']['version'] = '<=' + version

def mark_minimum_version(mod, items, version = '2.6'):
    for two_only in items:
        if two_only in mod['members']:
            # if we're running on a later version for the real scrap we may not have the member
            mod['members'][two_only]['value']['version'] = '>=' + version

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

def thread_fixer(mod):
    mark_minimum_version(mod, ['_count'], '2.7')

    # 3.x members
    mod['members']['TIMEOUT_MAX'] = {'kind': 'data', 'value': 
                                     {'version' : '>=3.2', 
                                      'type': type_to_typelist(float)}}
    mod['members']['RLock'] = {'kind': 'type',  'value': 
                               {'version': '>=3.2', 
                                'bases': type_to_typelist(object),
        'members': {'__doc__': {'kind': 'data',
                        'value': {'type': type_to_typelist(NoneType)}},
            '__enter__': {'kind': 'method',
                        'value': {'doc': 'acquire(blocking=True) -> bool\n\n'
                                  'Lock the lock.  `blocking` indicates '
                                  'whether we should wait\nfor the lock '
                                  'to be available or not.  If `blocking`'
                                  ' is False\nand another thread holds the '
                                  'lock, the method will return False\n'
                                  'immediately.  If `blocking` is True and '
                                  'another thread holds\nthe lock, the method '
                                  'will wait for the lock to be released,\n'
                                  'take it and then return True.\n(note: the '
                                  'blocking operation is interruptible.)\n\nIn'
                                  ' all other cases, the method will return '
                                  'True immediately.\nPrecisely, if the '
                                  'current thread already holds the lock, '
                                  'its\ninternal counter is simply incremented.'
                                  ' If nobody holds the lock,\nthe lock is '
                                  'taken and its internal counter initialized '
                                  'to 1.',
                                    'overloads': None}},
            '__exit__': {'kind': 'method',
                        'value': {'doc': 'release()\n\nRelease the lock,'
                                 ' allowing another thread that is blocked'
                                 ' waiting for\nthe lock to acquire the lock.'
                                 '  The lock must be in the locked state,\nand'
                                 ' must be locked by the same thread that'
                                 ' unlocks it; otherwise a\n`RuntimeError` is '
                                 'raised.\n\nDo note that if the lock was '
                                 'acquire()d several times in a row by the\n'
                                 'current thread, release() needs to be called'
                                 ' as many times for the lock\nto be available '
                                 'for other threads.',
                                    'overloads': None}},
            '__new__': NewMethodEntry,
            '__repr__': ReprMethodEntry,
            '_acquire_restore': {'kind': 'method',
                                'value': {'doc': '_acquire_restore(state) -> None\n\n'
                                          'For internal use by `threading.Condition`.',
                                    'overloads': [generate_overload(NoneType, ('self', object), ('state', object))]}},
            '_is_owned': {'kind': 'method',
                        'value': {'doc': '_is_owned() -> bool\n\nFor internal use by'
                                 ' `threading.Condition`.',
                                    'overloads': [generate_overload(bool, ('self', object))]}},
            '_release_save': {'kind': 'method',
                            'value': {'doc': '_release_save() -> tuple\n\nFor internal use'
                                     ' by `threading.Condition`.',
                                        'overloads': [generate_overload(tuple, ('self', object))]}},
            'acquire': {'kind': 'method',
                        'value': {'doc': 'acquire(blocking=True) -> bool\n\n'
                                  'Lock the lock.  `blocking` indicates '
                                  'whether we should wait\nfor the lock '
                                  'to be available or not.  If '
                                  '`blocking` is False\nand another thread'
                                  ' holds the lock, the method will return '
                                  'False\nimmediately.  If `blocking` is True'
                                  ' and another thread holds\nthe lock, the '
                                  'method will wait for the lock to be '
                                  'released,\ntake it and then return True.\n'
                                  '(note: the blocking operation is '
                                  'interruptible.)\n\nIn all other cases, the'
                                  ' method will return True immediately.\n'
                                  'Precisely, if the current thread already holds'
                                  ' the lock, its\ninternal counter is simply '
                                  'incremented. If nobody holds the lock,\nthe'
                                  ' lock is taken and its internal counter '
                                  'initialized to 1.',
                                    'overloads': None}},
            'release': {'kind': 'method',
                        'value': {'doc': 'release()\n\nRelease the lock, '
                                  'allowing another thread that is blocked'
                                  ' waiting for\nthe lock to acquire the lock.'
                                  '  The lock must be in the locked state,\nand'
                                  ' must be locked by the same thread that '
                                  'unlocks it; otherwise a\n`RuntimeError` is'
                                  ' raised.\n\nDo note that if the lock was '
                                  'acquire()d several times in a row by the\n'
                                  'current thread, release() needs to be '
                                  'called as many times for the lock\n'
                                  'to be available for other threads.',
                                    'overloads': [generate_overload(NoneType, ('self', object))]}}},
            'mro': [typename_to_typeref('_thread', 'RLock'), type_to_typeref(object)]}}
    return mod

def builtin_fixer(mod):
    mark_maximum_version(mod, ['StandardError', 'apply', 'basestring', 
                               'buffer', 'cmp', 'coerce', 'execfile', 'file', 
                               'intern', 'long', 'raw_input', 'reduce', 
                               'reload', 'unichr', 'unicode', 'xrange'])
    mark_minimum_version(mod, ['bytearray', 'bin', 'format', 'bytes', 
                               'BytesWarning', 'next', 'BufferError'], '2.6')
    mark_minimum_version(mod, ['memoryview'], '2.7')

    for iter_type in ['generator', 'list_iterator', 'tuple_iterator', 'set_iterator', 'str_iterator', 'bytes_iterator', 'callable_iterator']:
        next2x = mod['members'][iter_type]['value']['members']['next']
        next3x = dict(next2x)
        next2x['version'] = '<=2.7'
        next3x['version'] = '>=3.0'
        mod['members'][iter_type]['value']['members']['__next__'] = next3x
    mod['members']['bytes_iterator']['value']['version'] = '>=3.0'

    # new in 3x: exec, ascii, ResourceWarning, print

    mod['members']['exec'] = {
        'kind': 'function',
        'value': {
        'doc': 'exec(object[, globals[, locals]])\nRead and execute code from'
          ' an object, which can be a string or a code\nobject.\nThe globals'
          ' and locals are dictionaries, defaulting to the current\nglobals'
          ' and locals.  If only globals is given, locals defaults to it.',
        'overloads': [generate_overload(object, ('object', object), ('globals', dict, '', 'None'), ('locals', dict, '', 'None'))],
        'version': '>=3.0'
        }
    }
    
    
    mod['members']['print'] = {
        'kind': 'function',
        'value': {
        'doc': 'print(value, *args, sep=\' \', end=\'\\n\', file=sys.stdout)\n\n'
        'Prints the values to a stream, or to sys.stdout by default.\nOptional'
        ' keyword arguments:\nfile: a file-like object (stream); defaults to '
        'the current sys.stdout.\nsep:  string inserted between values, '
        'default a space.\nend:  string appended after the last value, default '
        'a newline.',
        'overloads': [generate_overload(NoneType,
                                        ('value', object),
                                        ('sep', str, '', "' '"),
                                        ('file', typename_to_typeref('io', 'IOBase'), '', 'sys.stdout')
                                       )],
        'version': '>=3.0'
        }
        
    }

    # ResourceWarning, new in 3.2
    mod['members']['ResourceWarning'] = {
            'kind': 'type',
            'value': {
                'version': '>=3.2',
                'bases': [typename_to_typeref(builtin_name, 'Warning')],
                'doc': 'Base class for warnings about resource usage.',
                'members': {
                    '__doc__': { 'kind': 'data', 'value': { 'type': type_to_typelist(str) } },
                    '__init__': InitMethodEntry,
                    '__new__': NewMethodEntry,
                },
                'mro': [typename_to_typeref(builtin_name, 'ResourceWarning'),
                        typename_to_typeref(builtin_name, 'Warning'),
                        typename_to_typeref(builtin_name, 'Exception'),
                        typename_to_typeref(builtin_name, 'BaseException'),
                        typename_to_typeref(builtin_name, 'object')]
            }
        }

    return mod

def sys_fixer(mod):
    mark_maximum_version(mod, ['exc_clear', 'py3kwarning', 'maxint', 
                               'long_info', 'exc_type'])
    mark_minimum_version(mod, ['dont_write_bytecode', 'float_info', 'gettrace',
                               'getprofile', 'py3kwarning', '__package__', 
                               'maxsize', 'flags','getsizeof', 
                               '_clear_type_cache'], '2.6')
    mark_minimum_version(mod, ['float_repr_style', 'long_info'], '2.7')

    # new in 3x

    mod['members']['int_info'] = {
        'kind': 'data', 
        'value': {
            'type': [typename_to_typeref('sys', 'int_info')], 
            'version': '>=3.1'
        }
    }
    mod['members']['_xoptions'] = {
        'kind': 'data', 
        'value': {
            'type': [typename_to_typeref(builtin_name, 'dict')], 
            'version': '>=3.1'
        }
    }
    mod['members']['intern'] = {
        'kind': 'function',
        'value': {
            'version': '>=3.0', 
            'doc': "intern(string) -> string\n\n"
                   "Intern the given string.  This enters the"
                   " string in the (global)\ntable of "
                   "interned strings whose purpose is to "
                   "speed up dictionary lookups.\nReturn"
                   " the string itself or the previously"
                   " interned string object with the\nsame "
                   "value.",
            'overloads': [generate_overload(str, ('string', str))]
        }
    }
    mod['members']['setswitchinterval'] = {
        'kind': 'function',
        'value': {
            'version': '>=3.2', 
            'doc': 'setswitchinterval(n)\n\n'
                   'Set the ideal thread switching '
                   'delay inside the Python '
                   'interpreter\nThe actual frequency'
                   ' of switching threads can be '
                   'lower if the\ninterpreter '
                   'executes long sequences of'
                   ' uninterruptible code\n(this is'
                   ' implementation-specific and '
                   'workload-dependent).\n\nThe '
                   'parameter must represent the '
                   'desired switching delay in '
                   'seconds\nA typical value '
                   'is 0.005 (5 milliseconds).',
            'overloads': [generate_overload(NoneType, ('n', float))]
        }
    }
    mod['members']['getswitchinterval'] = {
        'kind': 'function',
        'value': {
            'version': '>=3.2',
            'doc': 'getswitchinterval() -> current'
                   'thread switch interval; see setswitchinterval().',
            'overloads': [generate_overload(float)]
        }
    }
    mod['members']['hash_info'] = {
        'kind': 'data',
        'value': {
            'version': '>=3.2', 
            'type': [typename_to_typeref('sys', 'hash_info')]
        }
    }
    return mod

def nt_fixer(mod):
    mark_maximum_version(mod, ['tempnam', 'tmpfile', 'fdopen', 'getcwd', 
                               'popen4', 'popen2', 'popen3', 'popen', 
                               'tmpnam'])
    mark_minimum_version(mod, ['closerange'], '2.6')

    mod['members']['_isdir'] = {
        'kind': 'function',
        'value': {
            'doc': 'Return true if the pathname refers to an existing'
                   ' directory.',
            'overloads': [generate_overload(bool, ('pathname', str))],
            'version': '>=3.2'
        }
    }

    mod['members']['getlogin'] = {
        'kind': 'function',
        'value': {
            'doc': 'getlogin() -> string\n\nReturn the actual login name.',
            'overloads': [generate_overload(typename_to_typeref(builtin_name, 'code'))],
            'version': '>=3.2'
        }
    }
    mod['members']['_getfileinformation'] = {
        'kind': 'function',
        'value': {
            'overloads': None, 
            'version': '>=3.2'
        }
    }
    mod['members']['getppid'] = {
        'kind': 'function',
        'value': {
            'doc': "getppid() -> ppid\n\nReturn the parent's process id.  "
                   "If the parent process has already exited,\nWindows "
                   "machines will still return its id; others systems will"
                   " return the id\nof the 'init' process (1).",
            'overloads': [generate_overload(int)],
            'version': '>=3.2'
        }
    }
    mod['members']['symlink'] = {
        'kind': 'function',
        'value': {
            'doc': 'symlink(src, dst, target_is_directory=False)\n\nCreate'
                   ' a symbolic link pointing to src named dst.\n'
                   'target_is_directory is required if the target is to '
                   'be interpreted as\na directory.\nThis function requires'
                   ' Windows 6.0 or greater, and raises a\n'
                   'NotImplementedError otherwise.',
            'overloads': [generate_overload(NoneType, ('src', object), ('target_is_directory', bool, '', 'False'))],
            'version': '>=3.2'
        }
    }
    mod['members']['link'] = {
        'kind': 'function',
        'value': {
            'doc': 'link(src, dst)\n\nCreate a hard link to a file.',
            'overloads': [generate_overload(NoneType, ('src', object), ('dst', object))],
            'version': '>=3.2'
        }
    }
    mod['members']['device_encoding'] = {
        'kind': 'function',
        'value': {
            'doc': 'device_encoding(fd) -> str\n\nReturn a string '
                   'describing the encoding of the device\nif the'
                   ' output is a terminal; else return None.',
            'overloads': [generate_overload(str, ('fd', object))],
            'version': '>=3.0'
        }
    }
    mod['members']['_getfinalpathname'] = {
        'kind': 'function',
        'value': {
            'overloads': None, 
            'version': '>=3.2'
        }
    }
    mod['members']['readlink'] = {
        'kind': 'function',
        'value': {
            'doc': 'readlink(path) -> path\n\nReturn a string representing'
                   ' the path to which the symbolic link points.',
            'overloads': [generate_overload(str, ('path', object))],
            'version': '>=3.2'
        }
    }
    mod['members']['getcwdb'] = {
        'kind': 'function',
        'value': {
            'doc': 'getcwdb() -> path\n\nReturn a bytes string '
                   'representing the current working directory.',
            'overloads': [generate_overload(str)],
            'version': '>=3.0'
        }
    }
    return mod

def msvcrt_fixer(mod):
    mark_minimum_version(mod, ['LIBRARIES_ASSEMBLY_NAME_PREFIX', 
                               'CRT_ASSEMBLY_VERSION', 'getwche', 'ungetwch', 
                               'getwch', 'putwch', 
                               'VC_ASSEMBLY_PUBLICKEYTOKEN'], '2.6')

    mod['members']['SEM_FAILCRITICALERRORS'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(int), 'version': '>=3.2'}
    }
    mod['members']['SEM_NOALIGNMENTFAULTEXCEPT'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(int), 'version': '>=3.2'}
    }
    mod['members']['SetErrorMode'] = {
        'kind': 'function', 
        'value': {'overloads': None, 'version': '>=3.2'}
    }
    mod['members']['SEM_NOGPFAULTERRORBOX'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(int), 'version': '>=3.2'}
    }
    mod['members']['SEM_NOOPENFILEERRORBOX'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(int), 'version': '>=3.2'}
    }
    return mod

def gc_fixer(mod):
    mark_maximum_version(mod, ['DEBUG_OBJECTS', 'DEBUG_INSTANCES'])
    mark_minimum_version(mod, ['is_tracked'], '2.7')
    return mod

def cmath_fixer(mod):
    mark_minimum_version(mod, ['polar', 'isnan', 'isinf', 'phase', 'rect'], 
                         '2.6')
    mark_minimum_version(mod, ['lgamma', 'expm1', 'erfc', 'erf', 'gamma'], 
                         '2.7')

    mod['members']['isfinite'] = {
        'kind': 'function',
        'value': {
            'doc': 'isfinite(z) -> bool\nReturn True if both the real and '
                   'imaginary parts of z are finite, else False.',
            'overloads': [generate_overload(bool, ('z', object))],
            'version': '>=3.2'
        }
    }
    return mod

def _symtable_fixer(mod):
    mark_maximum_version(mod, ['OPT_BARE_EXEC', 'OPT_EXEC'])
    mark_minimum_version(mod, ['SCOPE_OFF', 'SCOPE_MASK'], '2.6')

    mod['members']['OPT_TOPLEVEL'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(int), 'version': '>=3.2'}
    }

    return mod

def _warnings_fixer(mod):
    mark_maximum_version(mod, ['default_action', 'once_registry'])

    mod['members']['_defaultaction'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(str), 'version': '>=3.0'}
    }
    mod['members']['_onceregistry'] = {
        'kind': 'data',
        'value': {'type': type_to_typelist(dict), 'version': '>=3.0'}
    }

    return mod

def _codecs_fixer(mod):
    mark_maximum_version(mod, ['charbuffer_encode'])
    mark_minimum_version(mod, ['utf_32_le_encode', 'utf_32_le_decode', 
                               'utf_32_be_decode', 'utf_32_be_encode', 
                               'utf_32_encode', 'utf_32_ex_decode', 
                               'utf_32_decode'], '2.6')
    return mod

def _md5_fixer(mod):
    mark_maximum_version(mod, ['new', 'MD5Type', 'digest_size'])
    mod['members']['md5'] = {
        'kind': 'function',
        'value': {
            'doc': 'Return a new MD5 hash object; optionally initialized '
                   'with a string.',
            'overloads': None,
            'version': '>=3.0'
        }
    }

    return mod

def math_fixer(mod):
    mark_minimum_version(mod, ['isnan', 'atanh', 'factorial', 'fsum', 
                               'copysign', 'asinh', 'isinf', 'acosh', 
                               'log1p', 'trunc'], '2.6')

    mod['members']['isfinite'] = {
        'kind': 'function',
        'value': {
            'doc': 'isfinite(x) -> bool\n\nReturn True if x is neither'
                   ' an infinity nor a NaN, and False otherwise.',
            'overloads': [generate_overload(bool, ('x', object))],
            'version': '>=3.2'
        }
    }

    return mod

def imp_fixer(mod):
    mark_minimum_version(mod, ['reload'], '2.6')

    mod['members']['source_from_cache'] = {
        'kind': 'function',
        'value': {
            'doc': 'Given the path to a .pyc./.pyo file, return the path '
                   'to its .py file.\n\nThe .pyc/.pyo file does not need'
                   ' to exist; this simply returns the path to\nthe .py '
                   'file calculated to correspond to the .pyc/.pyo file.'
                   '  If path\ndoes not conform to PEP 3147 format, '
                   'ValueError will be raised.',
            'overloads': None,
            'version': '>=3.2'
        }
    }
    mod['members']['get_tag'] = {
        'kind': 'function',
        'value': {
            'doc': 'get_tag() -> string\nReturn the magic tag for .pyc or'
                   ' .pyo files.',
            'overloads': [generate_overload(typename_to_typeref(builtin_name, 'code'))],
            'version': '>=3.2'
        }
    }
    mod['members']['cache_from_source'] = {
        'kind': 'function',
        'value': {
            'doc': 'Given the path to a .py file, return the path to its'
                   ' .pyc/.pyo file.\n\nThe .py file does not need to '
                   'exist; this simply returns the path to the\n.pyc/'
                   '.pyo file calculated as if the .py file were '
                   'imported.  The extension\nwill be .pyc unless '
                   '__debug__ is not defined, then it will be .pyo.'
                   '\n\nIf debug_override is not None, then it must'
                   ' be a boolean and is taken as\nthe value of '
                   '__debug__ instead.',
            'overloads': None,
            'version': '>=3.2'
        }
    }
    mod['members']['is_frozen_package'] = {
        'kind': 'function', 
        'value': {
            'overloads': None, 
            'version': '>=3.1'
        }
    }
    return mod

def operator_fixer(mod):
    mark_maximum_version(mod, ['__idiv__', 'delslice', 'repeat', 
                               '__getslice__', '__setslice__', 'getslice', 
                               '__repeat__', '__delslice__', 'idiv', 
                               'isMappingType', 'isSequenceType', '__div__', 
                               '__irepeat__', 'setslice', 'irepeat', 
                               'isNumberType', 'isCallable', 
                               'sequenceIncludes', 'div'])

    mark_minimum_version(mod, ['methodcaller'], '2.6')
    return mod

def itertools_fixer(mod):
    mark_maximum_version(mod, ['ifilter', 'izip', 'ifilterfalse', 
                               'imap', 'izip_longest'])
    mark_minimum_version(mod, ['combinations', 'product', 'permutations', 
                               'izip_longest'], '2.6')
    mark_minimum_version(mod, ['combinations_with_replacement', 'compress'], 
                                '2.7')

    mod['members']['accumulate'] = {
        'kind': 'type',
        'value': {
            'bases': [type_to_typeref(object)],
            'doc': 'accumulate(iterable) --> accumulate object\n\nReturn series of accumulated sums.',
            'members': {
                '__doc__': {'kind': 'data', 'value': { 'type': type_to_typelist(str) } },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
                '__new__': {
                    'kind': 'function',
                    'value': {
                        'doc': 'T.__new__(S, ...) -> a new object '
                               'with type S, a subtype of T',
                        'overloads': [generate_overload(NoneType, ('iterable', object))],
                    }
                },
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
            },
            'mro': [typename_to_typeref('itertools', 'accumulate'), type_to_typeref(object)],
            'version': '>=3.2'
        },
    }
    mod['members']['zip_longest'] = {
        'kind': 'type',
        'value': {
            'bases': [type_to_typeref(object)],
            'doc': 'zip_longest(iter1 [,iter2 [...]], [fillvalue=None]) '
                   '--> zip_longest object\n\nReturn an zip_longest object whose'
                   ' .__next__() method returns a tuple where\nthe i-th element'
                   ' comes from the i-th iterable argument.  The .__next__()'
                   '\nmethod continues until the longest iterable in the argument'
                   ' sequence\nis exhausted and then it raises StopIteration.  '
                   'When the shorter iterables\nare exhausted, the fillvalue is '
                   'substituted in their place.  The fillvalue\ndefaults to None'
                   ' or can be specified by a keyword argument.\n',
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': { 'type': type_to_typelist(str) },
                },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                },
                '__new__': NewMethodEntry,
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                }
            },
            'mro': [typename_to_typeref('itertools', 'zip_longest'), type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    mod['members']['filterfalse'] = {
        'kind': 'type',
        'value': {
            'bases': [type_to_typeref(object)],
            'doc': 'filterfalse(function or None, sequence) --> '
                   'filterfalse object\n\nReturn those items of sequence'
                   ' for which function(item) is false.\nIf function is None,'
                   ' return the items that are false.',
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__getattribute__': {
                    'kind': 'method',
                    'value': {
                        'doc': "x.__getattribute__('name') <==> x.name",
                        'overloads': None
                    }
                },
                '__iter__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__iter__() <==> iter(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    },
                },
                '__new__': NewMethodEntry,
                '__next__': {
                    'kind': 'method',
                    'value': {
                        'doc': 'x.__next__() <==> next(x)',
                        'overloads': [generate_overload(NoneType, ('self', object))],
                    }
                }
            },
            'mro': [typename_to_typeref('itertools', 'filterfalse'), type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    return mod

def cPickle_fixer(mod):
    mark_maximum_version(mod, ['HIGHEST_PROTOCOL', 'format_version', 
                               'UnpickleableError', '__builtin__', 
                               'BadPickleGet', '__version__', 
                               'compatible_formats'])
    return mod

def _struct_fixer(mod):
    mark_maximum_version(mod, ['_PY_STRUCT_FLOAT_COERCE', 
                               '_PY_STRUCT_RANGE_CHECKING', '__version__'])
    mark_minimum_version(mod, ['_clearcache', 'pack_into', 'calcsize', 
                               'unpack', 'unpack_from', 'pack'], '2.6')
    return mod

def parser_fixer(mod):
    mark_maximum_version(mod, ['compileast', 'ast2list', 'ASTType', 
                               'ast2tuple', 'sequence2ast', 'tuple2ast'])
    return mod

def array_fixer(mod):
    mod['members']['_array_reconstructor'] = {
        'kind': 'function',
        'value': {
            'doc': 'Internal. Used for pickling support.',
            'overloads': None,
            'version': '>=3.2'
        }
    }
    mod['members']['typecodes'] = {
        'kind': 'data', 
        'value': {
            'type': type_to_typelist(str), 
            'version': '>=3.0'
        }
    }

    return mod

def _ast_fixer(mod):
    mark_maximum_version(mod, ['Print', 'Repr', 'Exec'])
    mark_minimum_version(mod, ['ExceptHandler'], '2.6')
    mark_minimum_version(mod, ['SetComp', 'Set', 'DictComp'], '2.7')

    mod['members']['Starred'] = {
        'kind': 'type',
        'value': {
            'bases': [typename_to_typeref('_ast', 'expr')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(NoneType)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                },
            },
            'mro': [typename_to_typeref('_ast', 'Starred'),
                    typename_to_typeref('_ast', 'expr'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    mod['members']['Bytes'] = {
        'kind': 'type',
        'value': {
            'bases': [typename_to_typeref('_ast', 'expr')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(object)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'Bytes'),
                    typename_to_typeref('_ast', 'expr'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    mod['members']['Nonlocal'] = {
        'kind': 'type',
        'value': {
            'bases': [typename_to_typeref('_ast', 'stmt')],
            'members': {
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(NoneType)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'Nonlocal'),
                    typename_to_typeref('_ast', 'stmt'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    mod['members']['arg'] = {
        'kind': 'type',
        'value': {
            'bases': [typename_to_typeref('_ast', 'AST')],
            'members': {
                '__dict__': {
                    'kind': 'property',
                    'value': {
                        'doc': 'dictionary for instance variables (if defined)',
                        'type': type_to_typelist(object)
                    }
                },
                '__doc__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(object)}
                },
                '__module__': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(str)}
                },
                '__new__': NewMethodEntry,
                '__weakref__': {
                    'kind': 'property',
                    'value': {
                        'doc': 'list of weak references to the object (if defined)',
                        'type': type_to_typelist(object)
                    }
                },
                '_fields': {
                    'kind': 'data',
                    'value': {'type': type_to_typelist(tuple)}
                }
            },
            'mro': [typename_to_typeref('_ast', 'arg'),
                    typename_to_typeref('_ast', 'AST'),
                    type_to_typeref(object)],
            'version': '>=3.0'
        }
    }
    return mod

def mmap_fixer(mod):
    mark_minimum_version(mod, ['ALLOCATIONGRANULARITY'], '2.6')
    return mod

def _functools_fixer(mod):
    mark_minimum_version(mod, ['reduce'], '2.6')
    return mod

def winreg_fixer(mod):
    mark_minimum_version(mod, ['QueryReflectionKey', 'DisableReflectionKey', 
                               '__package__', 'KEY_WOW64_32KEY', 
                               'KEY_WOW64_64KEY', 'EnableReflectionKey', 
                               'ExpandEnvironmentStrings'], '2.6')
    mark_minimum_version(mod, ['CreateKeyEx', 'DeleteKeyEx'], '2.7')
    return mod

def _heapq_fixer(mod):
    mark_minimum_version(mod, ['heapqpushpop'], '2.6')
    return mod

def exceptions_fixer(mod):
    mark_minimum_version(mod, ['BytesWarning', 'BufferError'], '2.6')
    return mod

def signal_fixer(mod):
    mark_minimum_version(mod, ['set_wakeup_fd'], '2.6')
    mark_minimum_version(mod, ['CTRL_C_EVENT', 'CTRL_BREAK_EVENT'], '2.7')
    return mod

def _subprocess_fixer(mod):
    mark_minimum_version(mod, ['CREATE_NEW_PROCESS_GROUP'], '2.7')
    return mod

def _json_fixer(mod):
    mark_minimum_version(mod, ['make_scanner', 'make_encoder'], '2.7')
    return mod

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
                        'overloads': [
                            generate_overload(bool, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('pos', int, '', '0'), ('endpos', object, '', 'None')),
                        ]
                    }
                },
                'finditer': {
                    'kind': 'method',
                    'value': {
                        'doc': 'finditer(self: SRE_Pattern, string: object, pos: int, endpos: int) -> object\r\nfinditer(self: SRE_Pattern, string: object, pos: int) -> object\r\nfinditer(self: SRE_Pattern, string: object) -> object\r\n',
                        'overloads': [
                            generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('pos', int, '', '0'), ('endpos', int, '', 'None')),
                        ]
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
                        'overloads': [
                            generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('text', str), ('pos', int, '', '0'), ('endpos', int, '', 'None')),
                        ],
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
                        'overloads': [
                            generate_overload(typename_to_typeref('_sre', 'RE_Match'), ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('text', str), ('pos', int, '', '0'), ('endpos', int, '', 'None')),
                        ]
                    }
                },
                'split': {
                    'kind': 'method',
                    'value': {
                        'doc': 'split(self: SRE_Pattern, string: object, maxsplit: int) -> list (of str)\r\nsplit(self: SRE_Pattern, string: str) -> list (of str)\r\n',
                        'overloads': [
                            generate_overload(list, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('string', str), ('maxsplit', int, '', 'None'))
                        ]
                    }
                },
                'sub': {
                    'kind': 'method',
                    'value': {
                        'doc': 'sub(self: SRE_Pattern, repl: object, string: object, count: int) -> str\r\nsub(self: SRE_Pattern, repl: object, string: object) -> str\r\n',
                        'overloads': [
                            generate_overload(str, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('repl', object), ('string', str), ('count', int, '', 'None'))
                        ]
                    }
                },
                'subn': {
                    'kind': 'method',
                    'value': {'doc': 'subn(self: SRE_Pattern, repl: object, string: object, count: int) -> object\r\nsubn(self: SRE_Pattern, repl: object, string: str) -> object\r\n',
                        'overloads': [
                            generate_overload(object, ('self', typename_to_typeref('_sre', 'SRE_Pattern')), ('repl', object), ('string', str), ('count', int, '', 'None'))
                        ]
                    }
                },
            },
            'mro': [typename_to_typeref('_sre', 'SRE_Pattern'),
                    type_to_typeref(object)]
        }
    }

    return mod

module_fixers = {
    'thread' : thread_fixer,
    '__builtin__' : builtin_fixer,
    'sys': sys_fixer,
    'nt': nt_fixer,
    'msvcrt': msvcrt_fixer,
    'gc' : gc_fixer,
    '_symtable': _symtable_fixer,
    '_warnings': _warnings_fixer,
    '_codecs': _codecs_fixer,
    '_md5' : _md5_fixer,
    'cmath' : cmath_fixer,
    'math': math_fixer,
    'imp': imp_fixer,
    'operator': operator_fixer,
    'itertools': itertools_fixer,
    'cPickle': cPickle_fixer,
    '_struct': _struct_fixer,
    'parser': parser_fixer,
    'array': array_fixer,
    '_ast': _ast_fixer,
    'mmap': mmap_fixer,
    '_functools': _functools_fixer,
    '_winreg': winreg_fixer,
    'exceptions': exceptions_fixer,
    'signal' : signal_fixer,
    '_subprocess' : _subprocess_fixer,
    '_json' : _json_fixer,
}

# fixers which run on the newly generated file, not on the baseline file.
post_module_fixers = {
    '_sre' : _sre_post_fixer,
}

def merge_with_baseline(mod_name, baselinepath, final):
    if baselinepath is not None:
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
    else:
        # we are generating the baseline, which we do against IronPython where
        # we have all sorts of additional metadata.  We should fixup certain
        # members so that they are versioned appropriately.
        fixer = module_fixers.get(mod_name, None)
        if fixer is not None:
            final = fixer(final)

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
    for member in analysis['members']:
        if sys.version >= '3.':
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
    
    #import pprint
    #pp = pprint.PrettyPrinter()
    #pp.pprint(res['members']['NoneType'])

    res = merge_with_baseline(builtin_name, baselinepath, res)

    write_module(builtin_name, outpath, res)
    
    for mod_name in sys.builtin_module_names:
        if mod_name == builtin_name or mod_name == '__main__': continue
        
        res = generate_module(lookup_module(mod_name))
        if res is not None:
            try:
                res = merge_with_baseline(mod_name, baselinepath, res)

                write_module(mod_name, outpath, res)
            except ValueError:
                pass

    f = open(os.path.join(outpath, 'database.ver'), 'w')
    f.write(CURRENT_DATABASE_VERSION)
    f.close()

    # inspect extension modules installed into site-packages
    def package_inspector(site_packages, dirname, fnames):
        for filename in fnames:
            if filename.lower().endswith('.pyd'):
                # Spawn scraping out to a subprocess incase the module causes a crash.
                pkg_name = filename[:-4]

                cur_dirname = dirname
                while os.path.exists(os.path.join(cur_dirname, '__init__.py')):
                    head, tail = os.path.split(cur_dirname)
                    pkg_name = tail + '.' + pkg_name
                    cur_dirname = head

                # The win32com package automatically loads extension modules from
                # "site-packages\win32comext" and exposes them as children of itself.
                # For example, "win32comext\axcontrol\axcontrol.pyd" should be imported
                # as "win32com.axcontrol.axcontrol"
                # win32comext\ does not include "__init__.py", so it will be the last part
                # of the remaining path name.
                if os.path.split(cur_dirname)[1] == 'win32comext':
                    pkg_name = 'win32com.' + pkg_name

                subprocess.call([sys.executable,
                                 os.path.join(os.path.dirname(__file__), 'ExtensionScraper.py'),
                                 'scrape',
                                 pkg_name,      # name to pass to __import__()
                                 '-',           # not providing a path
                                 os.path.join(outpath, pkg_name)])

    site_packages = os.path.join(os.path.join(sys.prefix, 'Lib'), 'site-packages')
    for root, dirs, files in os.walk(site_packages):
        package_inspector(site_packages, root, files)
