#pragma optimize("", off)
#include <python.h>

// An extra dummy native frame is needed because otherwise native debugger
// skips global_func when doing the stack walk on Python 2.7.
PyObject* dummy(PyObject* arg) {
    return PyObject_CallObject(arg, NULL);
}

PyObject* global_func(PyObject* self, PyObject* arg) {
    return dummy(arg);
}

PyMethodDef methods[] = {
    { "global_func", global_func, METH_O, NULL },
    NULL
};

PyTypeObject* types[] = { NULL };
