#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

PyObject* CppObj_property_get(PyObject* self, void*) {
    Py_RETURN_NONE;
}

int CppObj_property_set(PyObject* self, PyObject* value, void*) {
    return 0;
}

PyGetSetDef CppObj_getset[] = {
    { "property", CppObj_property_get, CppObj_property_set, NULL, NULL },
    {}
};

PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_getset = CppObj_getset;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
