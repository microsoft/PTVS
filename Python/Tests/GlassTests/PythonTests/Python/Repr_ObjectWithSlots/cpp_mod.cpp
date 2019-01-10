#pragma optimize("", off)
#include <python.h>
#include <structmember.h>

struct CppObj {
    PyObject_HEAD
    char F_BOOL, F_BYTE, F_CHAR;
    double F_DOUBLE;
    float F_FLOAT;
    int F_INT;
    long F_LONG;
    long long F_LONGLONG;
    PyObject *F_OBJECT, *F_OBJECT_EX;
    Py_ssize_t F_PYSSIZET;
    short F_SHORT;
    char *F_STRING;
    unsigned char F_UBYTE;
    unsigned int F_UINT;
    unsigned long F_ULONG;
    unsigned long long F_ULONGLONG;
    unsigned short F_USHORT;
};

PyObject* CppObj_update(CppObj* self, PyObject* args) {
    self->F_BOOL = 1;
    self->F_STRING = "string";
    self->F_OBJECT = PyBool_FromLong(1);
    self->F_OBJECT_EX = PyBool_FromLong(1);
    Py_RETURN_NONE;
}

#define MEMBER_DEF(t) { "T_"#t, T_##t, offsetof(CppObj, F_##t) }

PyTypeObject* CppObj_type() {
    static PyMemberDef members[] = {
        MEMBER_DEF(BOOL),
        MEMBER_DEF(BYTE),
        MEMBER_DEF(CHAR),
        MEMBER_DEF(DOUBLE),
        MEMBER_DEF(FLOAT),
        MEMBER_DEF(INT),
        MEMBER_DEF(LONG),
        MEMBER_DEF(LONGLONG),
        MEMBER_DEF(OBJECT),
        MEMBER_DEF(OBJECT_EX),
        MEMBER_DEF(PYSSIZET),
        MEMBER_DEF(SHORT),
        MEMBER_DEF(STRING),
        MEMBER_DEF(UBYTE),
        MEMBER_DEF(UINT),
        MEMBER_DEF(ULONG),
        MEMBER_DEF(ULONGLONG),
        MEMBER_DEF(USHORT),
        NULL
    };
    static PyMethodDef methods[] = {
        { "update", (PyCFunction)CppObj_update, METH_NOARGS, NULL },
        NULL
    };
    static PyTypeObject t = { PyVarObject_HEAD_INIT(NULL, 0) };
    t.tp_name = "cpp_mod.CppObj";
    t.tp_basicsize = sizeof CppObj;
    t.tp_flags = Py_TPFLAGS_DEFAULT;
    t.tp_members = members;
    t.tp_methods = methods;
    return &t;
}

PyMethodDef methods[] = { NULL };

PyTypeObject* types[] = {
    CppObj_type(),
    NULL
};
