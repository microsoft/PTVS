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

import re
import sys
import types
import PythonScraper
try:
    import thread
except:
    import _thread as thread

def builtins_keys():
    if isinstance(__builtins__, dict):
        return __builtins__.keys()
    return dir(__builtins__)

def get_builtin(name):
    if isinstance(__builtins__, dict):
        return __builtins__[name]

    return getattr(__builtins__, name)

safe_getattr = PythonScraper.safe_getattr

BUILTIN_TYPES = [type_name for type_name in builtins_keys() if type(get_builtin(type_name)) is type]
if sys.version >= '3.':
    BUILTIN = 'builtins'
    unicode = str
else:
    BUILTIN = '__builtin__'

TYPE_OVERRIDES = {'string': PythonScraper.type_to_typeref(types.CodeType),
                  's': PythonScraper.type_to_typeref(str),
                  'integer': PythonScraper.type_to_typeref(int),
                  'boolean': PythonScraper.type_to_typeref(bool),
                  'number': PythonScraper.type_to_typeref(int),
                  'pid': PythonScraper.type_to_typeref(int),
                  'ppid': PythonScraper.type_to_typeref(int),
                  'fd': PythonScraper.type_to_typeref(int),
                  'handle': PythonScraper.type_to_typeref(int),
                  'Exit': PythonScraper.type_to_typeref(int),
                  'fd2': PythonScraper.type_to_typeref(int),
                  'Integral': PythonScraper.type_to_typeref(int),
                  'exit_status':PythonScraper.type_to_typeref(int),
                  'old_mask': PythonScraper.type_to_typeref(int),
                  'source': PythonScraper.type_to_typeref(str),
                  'newpos': PythonScraper.type_to_typeref(int),
                  'key': PythonScraper.type_to_typeref(str),
                  'dictionary': PythonScraper.type_to_typeref(dict),
                  'None': PythonScraper.type_to_typeref(type(None)),
                  'floating': PythonScraper.type_to_typeref(float),
                  'filename': PythonScraper.type_to_typeref(str),
                  'path': PythonScraper.type_to_typeref(str),
                  'byteswritten': PythonScraper.type_to_typeref(int),
                  'unicode': PythonScraper.type_to_typeref(unicode),
                  'Unicode': PythonScraper.type_to_typeref(unicode),
                  'True':  PythonScraper.type_to_typeref(bool),
                  'False':  PythonScraper.type_to_typeref(bool),
                  'lock': PythonScraper.type_to_typeref(thread.LockType),
                  'code': PythonScraper.type_to_typeref(types.CodeType),
                  'module': PythonScraper.type_to_typeref(types.ModuleType),
                  'size': PythonScraper.type_to_typeref(int),
                  'INT': PythonScraper.type_to_typeref(int),
                  'STRING': PythonScraper.type_to_typeref(str),
                  'TUPLE': PythonScraper.type_to_typeref(tuple),
                  'OBJECT': PythonScraper.type_to_typeref(object),
                  'LIST': PythonScraper.type_to_typeref(list),
                  'DICT': PythonScraper.type_to_typeref(dict),
                }

RETURN_TYPE_OVERRIDES = dict(TYPE_OVERRIDES)
RETURN_TYPE_OVERRIDES.update({'string': PythonScraper.type_to_typeref(str)})

def type_name_to_typeref(name, mod, type_overrides = TYPE_OVERRIDES):
    arg_type = type_overrides.get(name, None)
    if arg_type is None:
        if name in BUILTIN_TYPES:
            arg_type = PythonScraper.type_to_typeref(get_builtin(name))
        elif mod is not None and name in mod.__dict__:
            arg_type = PythonScraper.typename_to_typeref(mod.__name__, name)
        elif name.startswith('list'):
            arg_type = PythonScraper.type_to_typeref(list)
        else:
            # see if we can find it in any module we've imported...
            for mod_name, mod in sys.modules.items():
                if mod is not None and name in mod.__dict__ and isinstance(mod.__dict__[name], type):
                    arg_type = PythonScraper.typename_to_typeref(mod_name, name)
                    break
            else:
                first_space = name.find(' ')
                if first_space != -1:
                    return type_name_to_typeref(name[:first_space], mod, type_overrides)
                arg_type = PythonScraper.typename_to_typeref(name)
    return arg_type

OBJECT_TYPE = PythonScraper.type_to_typeref(object)

TOKENS_REGEX = (
    '('
    '(?:[a-zA-Z_][0-9a-zA-Z_-]*)|'  # identifier
    '(?:[0-9]+[lL]?)|'              # integer value
    '(?:[0-9]*\.[0-9]+)|'           # floating point value
    '(?:\.\.\.)|'                   # ellipsis
    '(?:\.)|'                      # dot
    '(?:\()|'                      # open paren
    '(?:\))|'                      # close paren
    '(?:\:)|'                      # colon
    '(?:-->)|'                      # return value
    '(?:->)|'                      # return value
    '(?:=>)|'                      # return value
    '(?:[,])|'                      # comma
    '(?:=)|'                      # assignment (default value)
    '(?:\.\.\.)|'                      # ellipsis
    '(?:\[)|'
    '(?:\])|'
    '(?:\*\*)|'
    '(?:\*)|'
     ')'
    )

def get_ret_type(ret_type, obj_class, mod):
    if ret_type is not None:
        if ret_type == 'copy' and obj_class is not None:
            # returns a copy of self
            return PythonScraper.type_to_typelist(obj_class)
        else:
            return [type_name_to_typeref(ret_type, mod, RETURN_TYPE_OVERRIDES)]


RETURNS_REGEX = [r'^\s*returns?[\s\-]*[a-z_]\w*\s*:\s*([a-z_]\w*)']

def update_overload_from_doc_str(overload, doc_str, obj_class, mod):
    # see if we can get additional information from the doc string
    if 'ret_type' not in overload:
        for ret_regex in RETURNS_REGEX:
            match = re.search(ret_regex, doc_str, re.MULTILINE | re.IGNORECASE)
            if match:
                ret_type = match.groups(0)[0]
                overload['ret_type'] = get_ret_type(ret_type, obj_class, mod)
                break


def parse_doc_str(input_str, module_name, mod, func_name, extra_args = [], obj_class = None):    
    # we split, so as long as we have all tokens every other item is a token, and the
    # rest are empty space.  If we have unrecognized tokens (for example during the description
    # of the function) those will show up in the even locations.  We do join's and bring the
    # entire range together in that case.
    tokens = re.split(TOKENS_REGEX, input_str) 
    start_token = 0
    last_identifier = None
    cur_token = 1
    overloads = []
    while cur_token < len(tokens):
        token = tokens[cur_token]
        # see if we have modname.funcname(
        if (cur_token + 10 < len(tokens) and
            token == module_name and 
            tokens[cur_token + 2] == '.' and
            tokens[cur_token + 4] == func_name and
            tokens[cur_token + 6] == '('):
            sig_start = cur_token
            args, ret_type, cur_token = parse_args(tokens, cur_token + 8, mod)

            doc_str = ''.join(tokens[start_token:sig_start])
            if doc_str.find(' ') == -1:
                doc_str = ''
            if (not args or doc_str) and overloads:
                # if we already parsed an overload, and are now getting an argless
                # overload we're likely just seeing a reference to the function in
                # a doc string, let's ignore that.  This is betting on the idea that
                # people list overloads first, then doc strings, and that people tend
                # to list overloads from simplest to more complex. an example of this
                # is the future_builtins.ascii doc string
                # We also skip it if we have a doc string, this comes up in overloads
                # like isinstance which has example calls embedded in the doc string
                continue

            start_token = cur_token
            overload = {'args': tuple(extra_args + args), 'doc': doc_str}
            ret_types = get_ret_type(ret_type, obj_class, mod)
            if ret_types is not None:
                overload['ret_type'] = ret_types
            update_overload_from_doc_str(overload, doc_str, obj_class, mod)
            overloads.append(overload)
        # see if we have funcname(
        elif (cur_token + 4 < len(tokens) and
              token == func_name and
              tokens[cur_token + 2] == '('):
            sig_start = cur_token
            args, ret_type, cur_token = parse_args(tokens, cur_token + 4, mod)

            doc_str = ''.join(tokens[start_token:sig_start])
            if doc_str.find(' ') == -1:
                doc_str = ''
            if (not args or doc_str) and overloads:
                # if we already parsed an overload, and are now getting an argless
                # overload we're likely just seeing a reference to the function in
                # a doc string, let's ignore that.  This is betting on the idea that
                # people list overloads first, then doc strings, and that people tend
                # to list overloads from simplest to more complex. an example of this
                # is the future_builtins.ascii doc string
                # We also skip it if we have a doc string, this comes up in overloads
                # like isinstance which has example calls embedded in the doc string
                continue
            
            start_token = cur_token
            overload = {'args': tuple(extra_args + args), 'doc': doc_str}
            ret_types = get_ret_type(ret_type, obj_class, mod)
            if ret_types is not None:
                overload['ret_type'] = ret_types
            update_overload_from_doc_str(overload, doc_str, obj_class, mod)
            overloads.append(overload)

        else:
            # append to doc string
            cur_token += 2

    finish_doc = ''.join(tokens[start_token:cur_token])
    if finish_doc:
        for overload in overloads:
            overload['doc'] += finish_doc
            update_overload_from_doc_str(overload, overload['doc'], obj_class, mod)

    return overloads


IDENTIFIER_REGEX = re.compile('^[a-zA-Z_][a-zA-Z_0-9-]*$')

def is_identifier(token):
    if IDENTIFIER_REGEX.match(token):
        return True
    return False

RETURN_TOKENS = set(['-->', '->', '=>', 'return'])

def parse_args(tokens, cur_token, module):
    args = []
    star_args = None
    is_optional = False
    default_value = None
    annotation = None
    ret_type = None
    is_tuple_param = False
    last_tuple_param = False
    while cur_token < len(tokens):
        token = tokens[cur_token]
        if token == '[':
            # optional arg
            is_optional = True
        elif token == '*':
            star_args = '*'
        elif token == '**':
            star_args = '**'
        elif token == ')':
            if is_tuple_param:
                is_tuple_param = False
                last_tuple_param = False
            else:
                cur_token += 2
                break
        elif token == ',':
            cur_token += 2
            if cur_token < len(tokens) and tokens[cur_token] == ']':
                # [foo,], ignore the trailing ]
                cur_token += 2
            continue
        elif token == '(':
            is_tuple_param = True
        else:
            arg_name = token
            if cur_token + 2 < len(tokens) and is_identifier(tokens[cur_token + 2]):
                # C cstyle sig, 'int foo'
                arg_name = tokens[cur_token + 2]
                annotation = token
                cur_token += 2

            if cur_token + 4 < len(tokens) and tokens[cur_token + 2] == '=':
                default_value = tokens[cur_token + 4]
                cur_token += 4
            
            if cur_token + 4 < len(tokens) and tokens[cur_token + 2] == ':':
                annotation = tokens[cur_token + 4]
                cur_token += 4

            arg = {'name': arg_name}
            if default_value is not None:
                arg['default_value'] = default_value
            elif is_optional:
                arg['default_value'] = 'None'

            if annotation is not None:
                arg_type = type_name_to_typeref(annotation, module)
                if arg_type is not None:
                    arg['type'] = [arg_type]
            if star_args is not None:
                arg['arg_format'] = star_args
            elif token == '...':
                arg['arg_format'] = '*'
                arg['name'] = 'args'
            
            while cur_token + 2 < len(tokens) and tokens[cur_token + 2] == ']':
                cur_token += 2
    
            if is_tuple_param:
                if last_tuple_param:
                    args[-1]['name'] = args[-1]['name'] + ', ' + arg_name
                else:
                    last_tuple_param = True
                    args.append(arg)
            else:
                args.append(arg)
            
            is_optional = False
            star_args = None
            default_value = None

        cur_token += 2

    # end of params, check for ret value
    if cur_token + 2 < len(tokens) and tokens[cur_token] in RETURN_TOKENS:
        ret_type_start = cur_token + 2
        # we might have a descriptive return value, 'list of foo'
        while ret_type_start < len(tokens) and is_identifier(tokens[ret_type_start]):
            if tokens[ret_type_start - 1].find('\n') != -1:
                break
            ret_type_start += 2

        if ret_type_start < len(tokens) and ',' in tokens[ret_type_start]:
            # foo(bar, baz) -> some info about the return, and more info, and more info.
            # "some info" is unlikely to be a return type
            ret_type = ''
            cur_token += 2
        else:
            ret_type = ''.join(tokens[cur_token + 2:ret_type_start]).strip()
            cur_token = ret_type_start
    elif (cur_token + 4 < len(tokens) and 
        tokens[cur_token] == ':' and tokens[cur_token + 2] in RETURN_TOKENS):
        ret_type_start = cur_token + 4
        # we might have a descriptive return value, 'list of foo'
        while ret_type_start < len(tokens) and is_identifier(tokens[ret_type_start]):
            if tokens[ret_type_start - 1].find('\n') != -1:
                break
            ret_type_start += 2

        if ret_type_start < len(tokens) and ',' in tokens[ret_type_start]:
            # foo(bar, baz) -> some info about the return, and more info, and more info.
            # "some info" is unlikely to be a return type
            ret_type = ''
            cur_token += 4
        else:
            ret_type = ''.join(tokens[cur_token + 4:ret_type_start]).strip()
            cur_token = ret_type_start

    return args, ret_type, cur_token


if sys.version > '3.':
    str_types = (str, bytes)
else:
    str_types = (str, unicode)


def get_overloads_from_doc_string(doc_str, mod, obj_class, func_name, extra_args = []):
    if isinstance(doc_str, str_types):
        decl_mod = None
        if isinstance(mod, types.ModuleType):
            decl_mod = mod
            mod = decl_mod.__name__
        elif mod is not None:
            decl_mod = sys.modules.get(mod, None)

        res = parse_doc_str(doc_str, mod, decl_mod, func_name, extra_args, obj_class)
        if res:
            for i, v in enumerate(res):
                if 'ret_type' not in v or (not v['ret_type'] or v['ret_type'] == ('', '')):
                    alt_ret_type = v['doc'].find('returned as a ')
                    if alt_ret_type != -1:
                        last_space = v['doc'].find(' ', alt_ret_type + 14)
                        last_new_line = v['doc'].find('\n', alt_ret_type + 14)
                        if last_space == -1:
                            if last_new_line == -1:
                                last_space = None
                            else:
                                last_space = last_new_line
                        elif last_new_line == -1:
                            last_space = None
                        else:
                            last_space = last_new_line
                        
                        ret_type_str = v['doc'][alt_ret_type+14:last_space]
                        if ret_type_str.endswith('.') or ret_type_str.endswith(','):
                            ret_type_str = ret_type_str[:-1]
                        new_ret_type = get_ret_type(ret_type_str, obj_class, decl_mod)
                        res[i]['ret_type'] = new_ret_type

            return res
    return None


def get_overloads(func, is_method = False):
    if is_method:
        extra_args = [{'type': PythonScraper.type_to_typelist(object), 'name': 'self'}]
    else:
        extra_args = []

    func_doc = safe_getattr(func, '__doc__', None)
    if not func_doc:
        return None
    
    return get_overloads_from_doc_string(
        func_doc, 
        safe_getattr(func, '__module__', None), 
        safe_getattr(func, '__objclass__', None),
        safe_getattr(func, '__name__', None),
        extra_args,
    )

def get_descriptor_type(descriptor):
    return object

def get_new_overloads(type_obj, obj):
    try:
        type_doc = safe_getattr(type_obj, '__doc__', None)
        type_type = type(type_obj)
    except:
        return None
    
    res = get_overloads_from_doc_string(
        type_doc, 
        safe_getattr(type_obj, '__module__', None), 
        type_type, 
        safe_getattr(type_obj, '__name__', None),
        [{'type': PythonScraper.type_to_typelist(type), 'name': 'cls'}],
    )

    if not res:
        obj_doc = safe_getattr(obj, '__doc__', None)
        if not obj_doc:
            return None
        res = get_overloads_from_doc_string(
            obj_doc, 
            safe_getattr(type_obj, '__module__', None), 
            type_type, 
            safe_getattr(type_obj, '__name__', None),
        )

    return res

def should_include_module(name):
    return True

if __name__ == '__main__':
    import pprint
    r = parse_doc_str("""arange([start,] stop[, step,], dtype=None)

    Returns
    -------
    out : ndarray""",
        'numpy',
        None,
        'arange')

    assert r == [{
        'doc': 'Returns\n    -------\n    out : ndarray',
        'ret_type': [('', 'ndarray')],
        'args': ({'name': 'start', 'default_value':'None'}, 
                 {'name': 'stop'}, 
                 {'name': 'step', 'default_value': 'None'},
                 {'name': 'dtype', 'default_value':'None'}, 
                )
    }], pprint.pformat(r)

    r = parse_doc_str("""arange([start,] stop[, step,], dtype=None)

    Return - out : ndarray""",
        'numpy',
        None,
        'arange')

    assert r == [{
        'doc': 'Return - out : ndarray',
        'ret_type': [('', 'ndarray')],
        'args': ({'name': 'start', 'default_value':'None'}, 
                 {'name': 'stop'}, 
                 {'name': 'step', 'default_value': 'None'},
                 {'name': 'dtype', 'default_value':'None'}, 
                )
    }], pprint.pformat(r)

    r = parse_doc_str('reduce(function, sequence[, initial]) -> value', BUILTIN, __builtins__, 'reduce')
    assert r == [
           {'args': (
                {'name': 'function'},
                {'name': 'sequence'},
                {'default_value': 'None', 'name': 'initial'}), 
            'doc': '', 
            'ret_type': [('', 'value')]
           }
        ], repr(r)

    r = parse_doc_str('pygame.draw.arc(Surface, color, Rect, start_angle, stop_angle, width=1): return Rect', 
                         'draw',
                         None,
                         'arc')

    assert r == [
           {'args': (
               {'name': 'Surface'},
               {'name': 'color'},
               {'name': 'Rect'},
               {'name': 'start_angle'},
               {'name': 'stop_angle'},
               {'default_value': '1', 'name': 'width'}),
            'doc': '',
            'ret_type': [('', 'Rect')]
           }
    ]

    r = parse_doc_str('''B.isdigit() -> bool

Return True if all characters in B are digits
and there is at least one character in B, False otherwise.''',
                    'bytes',
                    None,
                    'isdigit')

    assert r == [
        {'args': (),
         'doc': 'Return True if all characters in B are digits\nand there is at least one character in B, False otherwise.',
         'ret_type': [(BUILTIN, 'bool')]}
    ], repr(r)
    r = parse_doc_str('x.__init__(...) initializes x; see help(type(x)) for signature',
                      'str',
                      None,
                      '__init__')

    assert r == [{'args': ({'arg_format': '*', 'name': 'args'},),
                  'doc': 'initializes x; see help(type(x)) for signature'}], repr(r)

    r = parse_doc_str('S.find(sub [,start [,end]]) -> int',
                         'str',
                         None,
                         'find')

    assert r == [{
        'args': (
            {'name': 'sub'},
            {'default_value': 'None', 'name': 'start'},
            {'default_value': 'None', 'name': 'end'}
        ),
        'doc': '',
        'ret_type': [(BUILTIN, 'int')]
    }], repr(r)

    r = parse_doc_str('S.format(*args, **kwargs) -> unicode',
                      'str',
                      None,
                      'format')
    assert r == [
                 {'args': (
                           {'arg_format': '*', 'name': 'args'},
                           {'arg_format': '**', 'name': 'kwargs'}
                          ),
                 'doc': '',
                 'ret_type': [(BUILTIN, unicode.__name__)]}
    ], repr(r)
    
    r = parse_doc_str("'ascii(object) -> string\n\nReturn the same as repr().  In Python 3.x, the repr() result will\\ncontain printable characters unescaped, while the ascii() result\\nwill have such characters backslash-escaped.'",
            'future_builtins',
            None,
            'ascii')
    assert r == [{'args': ({'name': 'object'},),
                 'doc': "Return the same as repr().  In Python 3.x, the repr() result will\\ncontain printable characters unescaped, while the ascii() result\\nwill have such characters backslash-escaped.'",
                 'ret_type': [(BUILTIN, 'str')]}
    ], repr(r)

    r = parse_doc_str('f(INT class_code) => SpaceID',
                'foo',
                None,
                'f')    
    assert r == [{'args': ({'name': 'class_code', 'type': [(BUILTIN, 'int')]},),
        'doc': '',
        'ret_type': [('', 'SpaceID')]}], repr(r)

    r = parse_doc_str('compress(data, selectors) --> iterator over selected data\n\nReturn data elements',
                      'itertools',
                      None,
                      'compress')
    assert r == [{'args': ({'name': 'data'}, {'name': 'selectors'}),
                  'doc': 'Return data elements',
                  'ret_type': [('', 'iterator')]}], repr(r)
                  
    r = parse_doc_str('isinstance(object, class-or-type-or-tuple) -> bool\n\nReturn whether an object is an '
                      'instance of a class or of a subclass thereof.\nWith a type as second argument, '
                      'return whether that is the object\'s type.\nThe form using a tuple, isinstance(x, (A, B, ...)),'
                      ' is a shortcut for\nisinstance(x, A) or isinstance(x, B) or ... (etc.).',
                      BUILTIN,
                      None,
                      'isinstance')
    
    assert r == [{'args': ({'name': 'object'}, {'name': 'class-or-type-or-tuple'}),
                   'doc': "Return whether an object is an instance of a class or of a subclass thereof.\n"
                          "With a type as second argument, return whether that is the object's type.\n"
                          "The form using a tuple, isinstance(x, (A, B, ...)), is a shortcut for\n"
                          "isinstance(x, A) or isinstance(x, B) or ... (etc.).",
                   'ret_type': [(BUILTIN, 'bool')]}], repr(r)

    r = parse_doc_str('pygame.Rect(left, top, width, height): return Rect\n'
                      'pygame.Rect((left, top), (width, height)): return Rect\n'
                      'pygame.Rect(object): return Rect\n'
                      'pygame object for storing rectangular coordinates',
                      'pygame',
                      None,
                      'Rect'
     )
    assert r == [
        {'args': ({'name': 'left'}, {'name': 'top'}, {'name': 'width'}, {'name': 'height'}),
         'doc': 'pygame object for storing rectangular coordinates',
         'ret_type': [('', 'Rect')]},
         {'args': ({'name': 'left, top'}, {'name': 'width, height'}),
          'doc': 'pygame object for storing rectangular coordinates',
         'ret_type': [('', 'Rect')]},
         {'args': ({'name': 'object'},),
          'doc': 'pygame object for storing rectangular coordinates',
         'ret_type': [('', 'Rect')]}
    ], repr(r)

    r = parse_doc_str('read([size]) -> read at most size bytes, returned as a string.\n\n'
                      'If the size argument is negative or omitted, read until EOF is reached.\n'
                      'Notice that when in non-blocking mode, less data than what was requested\n'
                      'may be returned, even if no size parameter was given.',
                      BUILTIN,
                      __builtins__,
                      'read'
     )

    assert r == [{
        'args': ({'default_value': 'None', 'name': 'size'},),
        'doc': 'read at most size bytes, returned as a string.\n\nIf the size argument is negative or omitted, read until EOF is reached.\nNotice that when in non-blocking mode, less data than what was requested\nmay be returned, even if no size parameter was given.',
        'ret_type': [('', '')]
    }], repr(r)


    r = get_overloads_from_doc_string('read([size]) -> read at most size bytes, returned as a string.\n\n'
                      'If the size argument is negative or omitted, read until EOF is reached.\n'
                      'Notice that when in non-blocking mode, less data than what was requested\n'
                      'may be returned, even if no size parameter was given.',
                      __builtins__,
                      None,
                      'read'
     )

    assert r == [{'args': ({'default_value': 'None', 'name': 'size'},),
                 'doc': 'read at most size bytes, returned as a string.\n\nIf the size argument is negative or omitted, read until EOF is reached.\nNotice that when in non-blocking mode, less data than what was requested\nmay be returned, even if no size parameter was given.',
                 'ret_type': [('', '')]}], repr(r)

    r = parse_doc_str('T.__new__(S, ...) -> a new object with type S, a subtype of T',
                      'struct',
                      None,
                      '__new__'
     )

    assert r == [{
        'ret_type': [('', '')],
        'doc': 'a new object with type S, a subtype of T',
        'args': ({'name': 'S'}, {'arg_format': '*', 'name': 'args'})
    }], repr(r)
