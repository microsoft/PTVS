#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

int CppObj_compare(PyObject* self, PyObject* other) {
    return 0;
}

PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_compare = CppObj_compare;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
