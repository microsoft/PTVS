#include <Windows.h>
#include <Python.h>

PyObject *success(PyObject *, PyObject *) {
    return PyLong_FromLong(1234567L);
}

static PyMethodDef native_module_methods[] = {
    {
        "success",
        (PyCFunction)success,
        METH_O,
        PyDoc_STR("Returns a number... if it works")
    },
    { nullptr, nullptr, 0, nullptr }
};

static PyModuleDef native_module_module = {
    PyModuleDef_HEAD_INIT,
    "native_module",
    "Provides some functions, but faster",
    0,
    native_module_methods
};

PyMODINIT_FUNC PyInit_native_module() {
    return PyModule_Create(&native_module_module);
}

int main() { }
