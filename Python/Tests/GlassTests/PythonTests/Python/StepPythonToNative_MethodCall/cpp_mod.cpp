#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

PyObject* CppObj_method(PyObject* self, PyObject* args) {
    Py_RETURN_NONE;
}

PyTypeObject* CppObj_type() {
    static PyMethodDef methods[] = {
        { "method", CppObj_method, METH_NOARGS, NULL },
        NULL
    };
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_methods = methods;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
