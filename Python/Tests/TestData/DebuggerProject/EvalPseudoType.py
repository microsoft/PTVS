from ctypes import *

class PyObject(Structure):
    _fields_ = [('ob_refcnt', c_size_t),
                ('ob_type', py_object)]

class PseudoTypeType(object):
    def __getattribute__(self, name):
        if name == '__repr__':
            raise Exception()
        elif name == '__name__':
            return 'PseudoType'

class PseudoType(object):
    def __repr__(self):
        return 'pseudo'

PseudoType_ptr = cast(id(PseudoType), POINTER(PyObject))
obj = PseudoType()
PseudoType_ptr.contents.ob_type = py_object(PseudoTypeType)

print()