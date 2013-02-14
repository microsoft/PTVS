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

def type_to_name(type_obj):
    if type_obj.IsArray:
        return PythonScraper.type_to_name(tuple)
    elif type_obj == Void:
        return PythonScraper.type_to_name(type(None))
    elif not PythonOps.IsPythonType(clr.GetPythonType(type_obj)):
        raise NonPythonTypeException

    return PythonScraper.type_to_name(clr.GetPythonType(type_obj))


def get_default_value(param):
    if param.DefaultValue != DBNull.Value and not isinstance(param.DefaultValue, Missing):
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
        'type':type_to_name(param.ParameterType),
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
                arg_info = [{'type' : type_to_name(target.DeclaringType), 'name': 'self'}] + arg_info

            res.append(
             {'args' :  arg_info,
              'ret_type' : type_to_name(get_return_type(target)), }
            )
        except NonPythonTypeException:
            pass


    return tuple(res)

def get_overloads(func, is_method = False):
    if type(func) == type(list.append):
        func = PythonOps.GetBuiltinMethodDescriptorTemplate(func)

    targets = func.Targets

    res = get_function_overloads(targets)

    return tuple(res)


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