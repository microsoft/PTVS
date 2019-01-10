#pragma optimize("", off)
#include <python.h>

PyObject* global_func(PyObject* self, PyObject* args) {
    Py_RETURN_NONE;
}

PyMethodDef methods[] = {
    { "global_func", global_func, METH_VARARGS, NULL },
    NULL
};

PyTypeObject* types[] = { NULL };
