import clr
import PythonScraper

from System import ParamArrayAttribute, DBNull, Void
from System.Reflection import Missing

clr.AddReference('IronPython')
from IronPython.Runtime.Operations import PythonOps
from IronPython.Runtime import SiteLocalStorage

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


def get_parameter_info(param):
	parameter_table = {
		'type':type_to_name(param.ParameterType),
		'name':param.Name,		
	}
	
	default_value = get_default_value(param)
	if default_value is not None:
		parameter_table['default_value'] = default_value

	arg_format = get_arg_format(param)
	if arg_format is not None:
		parameter_table['arg_format'] = arg_format
	
	return parameter_table

def get_overloads(func):
    res = []
	
    if type(func) == type(list.append):
        func = PythonOps.GetBuiltinMethodDescriptorTemplate(func)

    for target in func.Targets:
		args = list(target.GetParameters())
		if args and args[0].ParameterType.FullName == 'IronPython.Runtime.CodeContext':
			del args[0]
		if args and args[0].ParameterType.IsSubclassOf(SiteLocalStorage):
			del args[0]

		try:
			arg_info = [get_parameter_info(arg) for arg in args]
			if not target.IsStatic:
				arg_info = [{'type' : type_to_name(target.DeclaringType), 'name': 'self'}] + arg_info

			res.append(
				{'args' :  arg_info,
				 'ret_type' : type_to_name(target.ReturnType), }
			)
		except NonPythonTypeException:
			pass

    return tuple(res)

def get_descriptor_type(descriptor):
	if hasattr(descriptor, 'PropertyType'):
		return clr.GetPythonType(descriptor.PropertyType)
	elif hasattr(descriptor, 'FieldType'):
		return clr.GetPythonType(descriptor.FieldType)
	return object