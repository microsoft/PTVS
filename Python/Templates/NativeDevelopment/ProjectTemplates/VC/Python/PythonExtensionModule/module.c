#include <Python.h>

/*
 * Implements an example function.
 */
PyDoc_STRVAR($safeprojectname$_example_doc, "example(obj, number)\
\
Example function");

PyObject *$safeprojectname$_example(PyObject *self, PyObject *args, PyObject *kwargs) {
    /* Shared references that do not need Py_DECREF before returning. */
    PyObject *obj = NULL;
    int number = 0;

    /* Parse positional and keyword arguments */
    static char* keywords[] = { "obj", "number", NULL };
    if (!PyArg_ParseTupleAndKeywords(args, kwargs, "Oi", keywords, &obj, &number)) {
        return NULL;
    }

    /* Function implementation starts here */

    if (number < 0) {
        PyErr_SetObject(PyExc_ValueError, obj);
        return NULL;    /* return NULL indicates error */
    }

    Py_RETURN_NONE;
}

/*
 * List of functions to add to $safeprojectname$ in exec_$safeprojectname$().
 */
static PyMethodDef $safeprojectname$_functions[] = {
    { "example", (PyCFunction)$safeprojectname$_example, METH_VARARGS | METH_KEYWORDS, $safeprojectname$_example_doc },
    { NULL, NULL, 0, NULL } /* marks end of array */
};

/*
 * Initialize $safeprojectname$. May be called multiple times, so avoid
 * using static state.
 */
int exec_$safeprojectname$(PyObject *module) {
    PyModule_AddFunctions(module, $safeprojectname$_functions);

    PyModule_AddStringConstant(module, "__author__", "$username$");
    PyModule_AddStringConstant(module, "__version__", "1.0.0");
    PyModule_AddIntConstant(module, "year", $year$);

    return 0; /* success */
}

/*
 * Documentation for $safeprojectname$.
 */
PyDoc_STRVAR($safeprojectname$_doc, "The $safeprojectname$ module");


static PyModuleDef_Slot $safeprojectname$_slots[] = {
    { Py_mod_exec, exec_$safeprojectname$ },
    { 0, NULL }
};

static PyModuleDef $safeprojectname$_def = {
    PyModuleDef_HEAD_INIT,
    "$safeprojectname$",
    $safeprojectname$_doc,
    0,              /* m_size */
    NULL,           /* m_methods */
    $safeprojectname$_slots,
    NULL,           /* m_traverse */
    NULL,           /* m_clear */
    NULL,           /* m_free */
};

PyMODINIT_FUNC PyInit_$safeprojectname$() {
    return PyModuleDef_Init(&$safeprojectname$_def);
}
