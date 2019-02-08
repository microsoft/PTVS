#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

#if PY_MAJOR_VERSION < 3
typedef long Py_hash_t;
#endif

Py_hash_t CppObj_hash(PyObject* self) {
    return 0;
}

PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_hash = CppObj_hash;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
