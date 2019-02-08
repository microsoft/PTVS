#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

PyObject* CppObj_richcompare(PyObject* self, PyObject* other, int op) {
    Py_RETURN_TRUE;
}

template<bool WithCompare>
PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = WithCompare ? "cpp_mod.CppObjWithCompare" : "cpp_mod.CppObjWithoutCompare";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_richcompare = WithCompare ? CppObj_richcompare : NULL;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type<true>(),
    CppObj_type<false>(),
    NULL
};
