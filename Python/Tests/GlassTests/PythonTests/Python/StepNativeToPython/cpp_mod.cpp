#pragma optimize("", off)
#include <python.h>

// We need a dummy func here to provide an extra stack frame, because otherwise the stack is messed up in Python 2.7
// because it is compiled with FPO, and skips our native frame - and there isn't really anything we can do about it.
PyObject* dummy(PyObject* arg) {
    PyObject* volatile result = PyObject_CallObject(arg, NULL);
    return result;
}

PyObject* global_func(PyObject* self, PyObject* arg) {
    return dummy(arg);
}

PyMethodDef methods [] = {
    { "global_func", global_func, METH_O, NULL },
    NULL
};

PyTypeObject* types [] = { NULL };
