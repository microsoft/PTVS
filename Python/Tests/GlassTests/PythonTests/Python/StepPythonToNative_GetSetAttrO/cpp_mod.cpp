#pragma optimize("", off)
#include <python.h>

struct CppObj {
    PyObject_HEAD
};

PyObject* CppObj_getattro(PyObject* self, PyObject* attr_name) {
    Py_RETURN_NONE;
}

int CppObj_setattro(PyObject* self, PyObject* attr_name, PyObject* value) {
    return 0;
}

PyTypeObject* CppObj_type() {
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_getattro = CppObj_getattro;
    t.tp_setattro = CppObj_setattro;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
