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

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import clr
import PythonScraper

from System import ParamArrayAttribute, DBNull, Void
from System.Reflection import Missing

clr.AddReference('IronPython')
from IronPython.Runtime.Operations import PythonOps
from IronPython.Runtime import SiteLocalStorage
from IronPython.Runtime.Operations import InstanceOps

clr.AddReference('Microsoft.Dynamic')
clr.AddReference('Microsoft.Scripting')
from Microsoft.Scripting import ParamDictionaryAttribute
from Microsoft.Scripting.Generation import CompilerHelpers

class NonPythonTypeException(Exception):
    pass

def safe_dir(obj):
    try:
        return frozenset(obj.__dict__) | frozenset(clr.Dir(obj))
    except:
        # Some types crash when we access __dict__ and/or dir()
        pass
    try:
        return frozenset(clr.Dir(obj))
    except:
        pass
    try:
        return frozenset(obj.__dict__)
    except:
        pass
    return frozenset()

def type_to_typelist(type_obj):
    if type_obj.IsArray:
        return PythonScraper.type_to_typelist(tuple)
    elif type_obj == Void:
        return PythonScraper.type_to_typelist(type(None))
    elif not PythonOps.IsPythonType(clr.GetPythonType(type_obj)):
        raise NonPythonTypeException

    return PythonScraper.type_to_typelist(clr.GetPythonType(type_obj))


def get_default_value(param):
    if param.DefaultValue is not DBNull.Value and param.DefaultValue is not Missing.Value:
        return repr(param.DefaultValue)
    elif param.IsOptional:
        missing = CompilerHelpers.GetMissingValue(param.ParameterType)
        if missing != Missing.Value:
            return repr(missing)
        return ""


def get_arg_format(param):
    if param.IsDefined(ParamArrayAttribute, False):
        return "*"
    elif param.IsDefined(ParamDictionaryAttribute, False):
        return "**"


def sanitize_name(param):
    for v in param.Name:
        if not ((v >= '0' and v <= '9') or (v >= 'A' and v <= 'Z') or (v >= 'a' and v <= 'z') or v == '_'):
            break
    else:
        return param.Name

    letters = []
    for v in param.Name:
        if ((v >= '0' and v <= '9') or (v >= 'A' and v <= 'Z') or (v >= 'a' and v <= 'z') or v == '_'):
            letters.append(v)
    return ''.join(letters)

def get_parameter_info(param):
    parameter_table = {
        'type': type_to_typelist(param.ParameterType),
        'name': sanitize_name(param),
    }

    default_value = get_default_value(param)
    if default_value is not None:
        parameter_table['default_value'] = default_value

    arg_format = get_arg_format(param)
    if arg_format is not None:
        parameter_table['arg_format'] = arg_format

    return parameter_table

def get_return_type(target):
    if hasattr(target, 'ReturnType'):
        return target.ReturnType
    # constructor
    return target.DeclaringType

def get_function_overloads(targets):
    res = []
    for target in targets:
        try:
            args = list(target.GetParameters())
        except:
            # likely a failure to load an assembly...
            continue
        if args and args[0].ParameterType.FullName == 'IronPython.Runtime.CodeContext':
            del args[0]
        if args and args[0].ParameterType.IsSubclassOf(SiteLocalStorage):
            del args[0]

        try:
            arg_info = [get_parameter_info(arg) for arg in args]
            if not target.IsStatic and not target.IsConstructor:
                arg_info.insert(0, {'type' : type_to_typelist(target.DeclaringType), 'name': 'self'})

            res.append({
                'args' : tuple(arg_info),
                'ret_type' : type_to_typelist(get_return_type(target))
            })
        except NonPythonTypeException:
            pass


    return res

def get_overloads(func, is_method = False):
    if type(func) == type(list.append):
        func = PythonOps.GetBuiltinMethodDescriptorTemplate(func)

    targets = func.Targets

    res = get_function_overloads(targets)

    return res


def get_descriptor_type(descriptor):
    if hasattr(descriptor, 'PropertyType'):
        return clr.GetPythonType(descriptor.PropertyType)
    elif hasattr(descriptor, 'FieldType'):
        return clr.GetPythonType(descriptor.FieldType)
    return object


def get_new_overloads(type_obj, func):
    if func.Targets and func.Targets[0].DeclaringType == clr.GetClrType(InstanceOps):
        print('has instance ops ' + str(type_obj))
        clrType = clr.GetClrType(type_obj)

        return get_function_overloads(clrType.GetConstructors())

    return None

SPECIAL_MODULES = ('wpf', 'clr')

def should_include_module(name):
    return name not in SPECIAL_MODULES
