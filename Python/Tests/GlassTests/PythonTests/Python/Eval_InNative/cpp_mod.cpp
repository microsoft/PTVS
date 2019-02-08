#pragma optimize("", off)
#include <python.h>

PyObject* global_func(PyObject* self, PyObject* arg) {
    Py_RETURN_NONE;
}

PyMethodDef methods[] = {
    { "global_func", global_func, METH_NOARGS, NULL },
    NULL
};

PyTypeObject* types[] = { NULL };
