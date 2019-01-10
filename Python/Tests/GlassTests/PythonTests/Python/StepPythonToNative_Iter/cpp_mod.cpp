#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

PyObject* CppObj_iter(PyObject* self) {
    Py_RETURN_NONE;
}

PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_iter = CppObj_iter;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
