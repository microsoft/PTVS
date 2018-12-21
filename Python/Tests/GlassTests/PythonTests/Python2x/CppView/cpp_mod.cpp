#pragma optimize("", off)
#include <python.h>
#include <structmember.h>

struct CppObj {
    PyObject_HEAD
    int x;
    void* y;
};

int CppObj_init(CppObj* self, PyObject* args, PyObject* kwds) {
    return 0;
}

PyTypeObject* CppObj_type() {
    static PyMemberDef members[] = {
        { "x", T_INT, offsetof(CppObj, x) },
        NULL
    };
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_members = members;
    t.tp_init = (initproc)CppObj_init;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
