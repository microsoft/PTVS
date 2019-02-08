#pragma optimize("", off)
#include <python.h>
#include <structmember.h>

struct CppObj {
    PyObject_HEAD
    PyObject* d;
};

PyTypeObject* CppObj_type() {
    static PyMemberDef members[] = {
        { "d", T_OBJECT, offsetof(CppObj, d) },
        NULL
    };
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_dictoffset = offsetof(CppObj, d);
    t.tp_members = members;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
