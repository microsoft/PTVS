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

def get_arg_name_and_type(arg, cfunc):
    arg = arg.strip()
    if arg.startswith('['):
        arg = arg[1:].strip()
    
    if arg.endswith(']'):
        arg = arg[:-1].strip()
        
    if arg.startswith(','):
        arg = arg[1:].strip()

    if arg.startswith('**'):
        arg = arg[2:].strip()
    elif arg.startswith('*'):
        arg = arg[1:].strip()

    return arg

def get_arg_name(arg, cfunc):    
    arg = get_arg_name_and_type(arg, cfunc)

    if cfunc:
        space = arg.find(' ')
        if space != -1:
            return arg[space+1:]

    colon_index = arg.find(':')
    if colon_index != -1:
        return arg[:colon_index]
    return arg

def builtins_keys():
    if isinstance(__builtins__, dict):
        return __builtins__.keys()
    return dir(__builtins__)

def get_builtin(name):
    if isinstance(__builtins__, dict):
        return __builtins__[name]

    return getattr(__builtins__, name)

BUILTIN_TYPES = [type_name for type_name in builtins_keys() if type(get_builtin(type_name)) is type]
if sys.version >= '3.':
    BUILTIN = 'builtins'
else:
    BUILTIN = '__builtin__'

TYPE_OVERRIDES = {'string': PythonScraper.type_to_name(types.CodeType),
                  's': PythonScraper.type_to_name(str),
                  'integer': PythonScraper.type_to_name(int),
                  'boolean': PythonScraper.type_to_name(bool),
                  'number': PythonScraper.type_to_name(int),
                  'pid': PythonScraper.type_to_name(int),
                  'ppid': PythonScraper.type_to_name(int),
                  'fd': PythonScraper.type_to_name(int),
                  'handle': PythonScraper.type_to_name(int),
                  'Exit': PythonScraper.type_to_name(int),
                  'fd2': PythonScraper.type_to_name(int),
                  'Integral': PythonScraper.type_to_name(int),
                  'exit_status':PythonScraper.type_to_name(int),
                  'old_mask': PythonScraper.type_to_name(int),
                  'source': PythonScraper.type_to_name(str),
                  'newpos': PythonScraper.type_to_name(int),
                  'key': PythonScraper.type_to_name(str),
                  'dictionary': PythonScraper.type_to_name(dict),
                  'None': PythonScraper.type_to_name(type(None)),
                  'floating': PythonScraper.type_to_name(float),
                  'filename': PythonScraper.type_to_name(str),
                  'path': PythonScraper.type_to_name(str),
                  'byteswritten': PythonScraper.type_to_name(int),
                  'Unicode': PythonScraper.type_to_name(float),
                  'True':  PythonScraper.type_to_name(bool),
                  'False':  PythonScraper.type_to_name(bool),
                  'lock': PythonScraper.type_to_name(thread.LockType),
                  'code': PythonScraper.type_to_name(types.CodeType),
                  'module': PythonScraper.type_to_name(types.ModuleType),
                  'size': PythonScraper.type_to_name(int),
                }

def type_name_to_type(name, mod):
    arg_type = TYPE_OVERRIDES.get(name, None)
    if arg_type is None:
        if name in BUILTIN_TYPES:
            arg_type = PythonScraper.type_to_name(__builtins__[name])
        elif mod is not None and name in mod.__dict__:
            arg_type = PythonScraper.memoize_type_name((mod.__name__, name))
        elif name.startswith('list'):
            arg_type = PythonScraper.type_to_name(list)
        elif name == 'unicode':
            # Py3k, some doc strings still have unicode in them.
            arg_type = PythonScraper.type_to_name(str)
        else:
            # see if we can find it in any module we've imported...
            for mod_name, mod in sys.modules.items():
                if mod is not None and name in mod.__dict__ and isinstance(mod.__dict__[name], type):
                    arg_type = (mod_name, name)
                    break
            else:
                arg_type = ('', name)
    return arg_type

OBJECT_TYPE = PythonScraper.type_to_name(object)
def get_arg_info(arg, mod, cfunc):    
    name = get_arg_name(arg, cfunc)
    optional = False
    arg_format = None
    if arg.find('[') != -1:
        optional = True
    
    if arg.find('**') != -1:
        arg_format = '**'
    elif arg.find('*') != -1 or name == '...':
        arg_format = '*'
    
    arg = arg.strip()
    default_value = arg.find('=')
    default_value_repr = None
    if default_value != -1:
        if arg.endswith(']'):
            arg = arg[:-1]
        default_value_repr = arg[default_value+1:]
    elif optional:
        default_value_repr = 'None'

    colon = arg.find(':')
    if colon != -1:
        arg_type = type_name_to_type(arg[colon+1:].strip(), mod)
    elif cfunc:
        carg = get_arg_name_and_type(arg, cfunc)
        space = carg.find(' ')
        if space != -1:
            arg_type = type_name_to_type(carg[:space], mod)
        else:
            arg_type = OBJECT_TYPE
    else:
        arg_type = OBJECT_TYPE

    res = {'type': arg_type, 'name': name}
    if default_value_repr is not None:
        res['default_value'] = default_value_repr
    if arg_format is not None:
        res['arg_format'] = arg_format
    return res


 
# We could still improve the parsing of doc strings, but we really need a parser, not just a regex
# which can't handle things like balanced parens such as x [, y [, z]].  But this is better than
# nothing.

DOC_REGEX = ('([a-zA-Z_][\\w]*\\.)?'                                    # X. (instance/class name)
            '([a-zA-Z_][\\w]*)\s*\\'                                    # foo   (method name)
            '(((?:(?:\\*)|(?:\\*\\*))?\s*(?:(\.\.\.)|(?:[a-zA-Z_][\\w]*(?::\s*[a-zA-Z_][\\w]*)?)))?'               # first argument
            '(\s*[[]?\s*,\s*(?:(?:(?:\\*)|(?:\\*\\*))?\s*(?:(\.\.\.)|(?:[a-zA-Z_][\\w]*(?::\s*[a-zA-Z_][\\w]*)?)))\s*(?:\s*=\s*[0-9a-zA-Z_\.]+\s*)?[\]]?)*\\)'    # additional args, accepts ", foo", [, foo]"
            '(\s*->\s*[a-zA-Z_][\\w]*)?')                               #f return type

# (?:[a-zA-Z_]+\s+)?
# regex to match a func like (int x, int y)
DOC_REGEX_CFUNC = ('([a-zA-Z_][\\w]*\\.)?'                                    # X. (instance/class name)
            '([a-zA-Z_][\\w]*)\s*\\'                                    # foo   (method name)
            '(((?:(?:\\*)|(?:\\*\\*))?\s*(?:[a-zA-Z_]+\s+)[a-zA-Z_][\\w]*)?'               # first argument
            '(\s*[[]?\s*,\s*(?:[a-zA-Z_]+\s+)?(?:(?:(?:\\*)|(?:\\*\\*))?\s*[a-zA-Z_][\\w]*)\s*(?:\s*=\s*[0-9a-zA-Z_\.]+\s*)?[\]]?)*\\)'    # additional args, accepts ", foo", [, foo]"
            '(\s*->\s*[a-zA-Z_][\\w]*)?')                               # return type

def get_overloads_from_doc_string(doc_str, mod, obj_class, func_name, is_method = False):
    decl_mod = None
    if mod is not None:
        decl_mod = sys.modules.get(mod, None)

    res = []
    if isinstance(doc_str, str):
        doc_matches = re.findall(DOC_REGEX, doc_str)
        cfunc = False
        if not doc_matches:
            doc_matches = re.findall(DOC_REGEX, doc_str)
            cfunc = True
    
        for arg_info in doc_matches:
            method = arg_info[1]
            if func_name is not None and method != func_name:
                # wrong function name, ignore the match
                continue
                
            args = [get_arg_info(arg, decl_mod, cfunc) for arg in arg_info[2:-1] if get_arg_name(arg, cfunc)]
            
            ret_type = arg_info[-1]
            if ret_type:
                ret_type = ret_type.strip()
                ret_type = type_name_to_type(ret_type[2:].strip(), decl_mod)  # remove ->
                if not ret_type[0]:
                    if ret_type[1] == 'copy' and obj_class is not None:
                        # returns a copy of self
                        ret_type = PythonScraper.type_to_name(obj_class)
            else:
                ret_type = PythonScraper.type_to_name(type(None))
    
            if is_method:
                args = [{'type': PythonScraper.type_to_name(object), 'name': 'self'}] + args

            overload = {
                'args': args,
                'ret_type' : ret_type
            }
            
            res.append(overload)
    
    if not res:
        return None
    
    return tuple(res)

def get_overloads(func, is_method = False):
    return get_overloads_from_doc_string(func.__doc__, 
                                         getattr(func, '__module__', None), 
                                         getattr(func, '__objclass__', None),
                                         getattr(func, '__name__', None),
                                         is_method)

def get_descriptor_type(descriptor):
	return object

def get_new_overloads(type_obj, obj):
    res = get_overloads_from_doc_string(type_obj.__doc__, 
                                        getattr(type_obj, '__module__', None), 
                                        type(type_obj), 
                                        getattr(type_obj, '__name__', None))
    if not res:
        res = get_overloads_from_doc_string(obj.__doc__, 
                                            getattr(type_obj, '__module__', None), 
                                            type(type_obj), 
                                            getattr(type_obj, '__name__', None))
    return res