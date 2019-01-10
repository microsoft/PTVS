// Scaffolding for declaring a Python module.

#include <python.h>

extern PyMethodDef methods[];
extern PyTypeObject* types[];

static bool init_types(PyObject* module) {
    for (PyTypeObject** p = types; *p; ++p) {
        PyTypeObject* type = *p;

        if (!type->tp_new) {
            type->tp_new = PyType_GenericNew;
        }

        if (PyType_Ready(type) < 0) {
            return false;
        }       

        const char* shortName = strrchr(type->tp_name, '.');
        if (shortName) {
            ++shortName;
        } else {
            shortName = type->tp_name;
        }

        Py_INCREF(type);
        PyModule_AddObject(module, shortName, (PyObject*)type);
    }

    return true;
}

#if PY_MAJOR_VERSION == 3

PyMODINIT_FUNC PyInit_cpp_mod() {
    static PyModuleDef module_def = {
        PyModuleDef_HEAD_INIT,
        "cpp_mod",
        NULL,
        -1,
        methods
    };
    PyObject* module = PyModule_Create(&module_def);
    if (!module || !init_types(module)) {
        return NULL;
    }
    return module;
}

#else

PyMODINIT_FUNC initcpp_mod() {
    PyObject* module = Py_InitModule("cpp_mod", methods);
    if (!module || !init_types(module)) {
        return;
    }
}

#endif
