// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

#ifndef __PYTHON_H__
#define __PYTHON_H__

// must be kept in sync with PythonLanguageVersion.cs
enum PythonVersion {
    PythonVersion_Unknown,
    PythonVersion_25 = 0x0205,
    PythonVersion_26 = 0x0206,
    PythonVersion_27 = 0x0207,
    PythonVersion_30 = 0x0300,
    PythonVersion_31 = 0x0301,
    PythonVersion_32 = 0x0302,
    PythonVersion_33 = 0x0303,
    PythonVersion_34 = 0x0304,
    PythonVersion_35 = 0x0305,
    PythonVersion_36 = 0x0306,
    PythonVersion_37 = 0x0307,
    PythonVersion_38 = 0x0308,
    PythonVersion_39 = 0x0309,
    PythonVersion_310 = 0x0310
};

struct PythonVersionMajMin {
    int major;
    int minor;
};

typedef const char* (GetVersionFunc)();

// Get the python version in major and minor parts from a version string
static PythonVersionMajMin GetPythonVersionFromVersionString(const char* version) {

    int major = atoi(version);
    int minor = 0;

    while (*version && *version >= '0' && *version <= '9') {
        version++;
    }
    if (*version == '.') {
        version++;
        minor = atoi(version);
    }

    PythonVersionMajMin ver = {};
    ver.major = major;
    ver.minor = minor;

    return ver;
}

static PythonVersion GetPythonVersion(HMODULE hMod) {
    auto versionFunc = (GetVersionFunc*)GetProcAddress(hMod, "Py_GetVersion");

    // if we can't find the version function, we're done
    if (versionFunc == nullptr) {
        return PythonVersion_Unknown;
    }
    auto version = versionFunc();

    // if the results of the version function are bad, we're done
    if (version == nullptr || strlen(version) < 3 || version[1] != '.') {
        return PythonVersion_Unknown;
    }

    // get the python major and minor versions, parse them, and return the correct enum
    PythonVersionMajMin pythonVer = GetPythonVersionFromVersionString(version);

    if (pythonVer.major == 2) {
        switch (pythonVer.minor) {
        case 5: return PythonVersion_25;
        case 6: return PythonVersion_26;
        case 7: return PythonVersion_27;
        }
    }
    else if (pythonVer.major == 3) {
        switch (pythonVer.minor) {
        case 1: return PythonVersion_31;
        case 2: return PythonVersion_32;
        case 3: return PythonVersion_33;
        case 4: return PythonVersion_34;
        case 5: return PythonVersion_35;
        case 6: return PythonVersion_36;
        case 7: return PythonVersion_37;
        case 8: return PythonVersion_38;
        case 9: return PythonVersion_39;
        case 10: return PythonVersion_310;
        }
    }

    // if we get here, the version doesn't match an expected value
    return PythonVersion_Unknown;
}

// defines limited header of Python API for compatible access across a number of Pythons.

class PyTypeObject;
class PyThreadState;

#define PyObject_HEAD           \
    size_t ob_refcnt;           \
    PyTypeObject *ob_type;

#define PyObject_VAR_HEAD       \
    PyObject_HEAD               \
    size_t ob_size; /* Number of items in variable part */

class PyObject {
public:
    PyObject_HEAD
};

class PyVarObject : public PyObject {
public:
    size_t ob_size; /* Number of items in variable part */
};

// 2.4 - 2.7 compatible
class PyCodeObject25_27 : public PyObject {
public:
    int co_argcount;        /* #arguments, except *args */
    int co_nlocals;         /* #local variables */
    int co_stacksize;       /* #entries needed for evaluation stack */
    int co_flags;           /* CO_..., see below */
    PyObject *co_code;      /* instruction opcodes */
    PyObject *co_consts;    /* list (constants used) */
    PyObject *co_names;     /* list of strings (names used) */
    PyObject *co_varnames;  /* tuple of strings (local variable names) */
    PyObject *co_freevars;  /* tuple of strings (free variable names) */
    PyObject *co_cellvars;  /* tuple of strings (cell variable names) */
    /* The rest doesn't count for hash/cmp */
    PyObject *co_filename;  /* string (where it was loaded from) */
    PyObject *co_name;      /* string (name, for reference) */
    int co_firstlineno;     /* first source line number */
    PyObject *co_lnotab;    /* string (encoding addr<->lineno mapping) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 2 && (minorVersion >= 5 && minorVersion <= 7);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_25 && version <= PythonVersion_27;
    }
};

// 3.0-3.2
class PyCodeObject30_32 : public PyObject {
public:
    int co_argcount;        /* #arguments, except *args */
    int co_kwonlyargcount;  /* #keyword only arguments */
    int co_nlocals;         /* #local variables */
    int co_stacksize;       /* #entries needed for evaluation stack */
    int co_flags;           /* CO_..., see below */
    PyObject *co_code;      /* instruction opcodes */
    PyObject *co_consts;    /* list (constants used) */
    PyObject *co_names;     /* list of strings (names used) */
    PyObject *co_varnames;  /* tuple of strings (local variable names) */
    PyObject *co_freevars;  /* tuple of strings (free variable names) */
    PyObject *co_cellvars;  /* tuple of strings (cell variable names) */
    /* The rest doesn't count for hash or comparisons */
    PyObject *co_filename;  /* unicode (where it was loaded from) */
    PyObject *co_name;      /* unicode (name, for reference) */
    int co_firstlineno;     /* first source line number */
    PyObject *co_lnotab;    /* string (encoding addr<->lineno mapping) */
    void *co_zombieframe;   /* for optimization only (see frameobject.c) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && (minorVersion >= 0 && minorVersion <= 2);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_30 && version <= PythonVersion_32;
    }
};

// 3.3-3.5
class PyCodeObject33_35 : public PyObject {
public:
    int co_argcount;            /* #arguments, except *args */
    int co_kwonlyargcount;      /* #keyword only arguments */
    int co_nlocals;             /* #local variables */
    int co_stacksize;           /* #entries needed for evaluation stack */
    int co_flags;               /* CO_..., see below */
    PyObject *co_code;          /* instruction opcodes */
    PyObject *co_consts;        /* list (constants used) */
    PyObject *co_names;         /* list of strings (names used) */
    PyObject *co_varnames;      /* tuple of strings (local variable names) */
    PyObject *co_freevars;      /* tuple of strings (free variable names) */
    PyObject *co_cellvars;      /* tuple of strings (cell variable names) */
    /* The rest doesn't count for hash or comparisons */
    unsigned char *co_cell2arg; /* Maps cell vars which are arguments. */
    PyObject *co_filename;      /* unicode (where it was loaded from) */
    PyObject *co_name;          /* unicode (name, for reference) */
    int co_firstlineno;         /* first source line number */
    PyObject *co_lnotab;        /* string (encoding addr<->lineno mapping) */
    void *co_zombieframe;       /* for optimization only (see frameobject.c) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && (minorVersion >= 3 && minorVersion <= 5);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_33 && version <= PythonVersion_35;
    }
};

// 3.6
class PyCodeObject36 : public PyObject {
public:
    int co_argcount;            /* #arguments, except *args */
    int co_kwonlyargcount;      /* #keyword only arguments */
    int co_nlocals;             /* #local variables */
    int co_stacksize;           /* #entries needed for evaluation stack */
    int co_flags;               /* CO_..., see below */
    int co_firstlineno;         /* first source line number */
    PyObject *co_code;          /* instruction opcodes */
    PyObject *co_consts;        /* list (constants used) */
    PyObject *co_names;         /* list of strings (names used) */
    PyObject *co_varnames;      /* tuple of strings (local variable names) */
    PyObject *co_freevars;      /* tuple of strings (free variable names) */
    PyObject *co_cellvars;      /* tuple of strings (cell variable names) */
    /* The rest doesn't count for hash or comparisons */
    unsigned char *co_cell2arg; /* Maps cell vars which are arguments. */
    PyObject *co_filename;      /* unicode (where it was loaded from) */
    PyObject *co_name;          /* unicode (name, for reference) */
    PyObject *co_lnotab;        /* string (encoding addr<->lineno mapping) */
    void *co_zombieframe;       /* for optimization only (see frameobject.c) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion == 6;
    }

    static bool IsFor(PythonVersion version) {
        return version == PythonVersion_36;
    }
};

// 3.7
class PyCodeObject37 : public PyObject {
public:
    int co_argcount;            /* #arguments, except *args */
    int co_kwonlyargcount;      /* #keyword only arguments */
    int co_nlocals;             /* #local variables */
    int co_stacksize;           /* #entries needed for evaluation stack */
    int co_flags;               /* CO_..., see below */
    int co_firstlineno;         /* first source line number */
    PyObject *co_code;          /* instruction opcodes */
    PyObject *co_consts;        /* list (constants used) */
    PyObject *co_names;         /* list of strings (names used) */
    PyObject *co_varnames;      /* tuple of strings (local variable names) */
    PyObject *co_freevars;      /* tuple of strings (free variable names) */
    PyObject *co_cellvars;      /* tuple of strings (cell variable names) */
                                /* The rest doesn't count for hash or comparisons */
    SSIZE_T *co_cell2arg;       /* Maps cell vars which are arguments. */
    PyObject *co_filename;      /* unicode (where it was loaded from) */
    PyObject *co_name;          /* unicode (name, for reference) */
    PyObject *co_lnotab;        /* string (encoding addr<->lineno mapping) */
    void *co_zombieframe;       /* for optimization only (see frameobject.c) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion == 7;
    }

    static bool IsFor(PythonVersion version) {
        return version == PythonVersion_37;
    }
};

// 3.8 - 3.10
class PyCodeObject38 : public PyObject {
public:
    int co_argcount;            /* #arguments, except *args */
    int co_posonlyargcount;     /* #positional only arguments */
    int co_kwonlyargcount;      /* #keyword only arguments */
    int co_nlocals;             /* #local variables */
    int co_stacksize;           /* #entries needed for evaluation stack */
    int co_flags;               /* CO_..., see below */
    int co_firstlineno;         /* first source line number */
    PyObject* co_code;          /* instruction opcodes */
    PyObject* co_consts;        /* list (constants used) */
    PyObject* co_names;         /* list of strings (names used) */
    PyObject* co_varnames;      /* tuple of strings (local variable names) */
    PyObject* co_freevars;      /* tuple of strings (free variable names) */
    PyObject* co_cellvars;      /* tuple of strings (cell variable names) */
                                /* The rest doesn't count for hash or comparisons */
    SSIZE_T* co_cell2arg;       /* Maps cell vars which are arguments. */
    PyObject* co_filename;      /* unicode (where it was loaded from) */
    PyObject* co_name;          /* unicode (name, for reference) */
    union {
        PyObject* co_lnotab;    /* <= 3.9 string (encoding addr<->lineno mapping) */
        PyObject* co_linetable; /* >= 3.10 string (encoding addr<->lineno mapping) */
    };
    void* co_zombieframe;       /* for optimization only (see frameobject.c) */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion >= 8;
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_38;
    }
};

// 2.5 - 3.10
// corresponds to the PyFunctionObject struct in include\funcobject.h in the CPython install
class PyFunctionObject : public PyObject {
public:
    PyObject *func_code;    /* A code object */
};

// 2.5 - 2.7 compatible
class PyStringObject : public PyVarObject {
public:
    long ob_shash;
    int ob_sstate;
    char ob_sval[1];

    /* Invariants:
     *     ob_sval contains space for 'ob_size+1' elements.
     *     ob_sval[ob_size] == 0.
     *     ob_shash is the hash of the string or -1 if not computed yet.
     *     ob_sstate != 0 iff the string object is in stringobject.c's
     *       'interned' dictionary; in this case the two references
     *       from 'interned' to this object are *not counted* in ob_refcnt.
     */
};

// 2.4 - 3.10 compatible
// corresponds to the PyASCIIObject struct in include\cpython\unicodeobject.h in the CPython install
typedef struct {
    PyObject_HEAD
    size_t length;      /* Length of raw Unicode data in buffer */
    wchar_t *str;       /* Raw Unicode buffer */
    long hash;          /* Hash value; -1 if not set */
} PyUnicodeObject;

// 2.4 - 3.10 compatible
// corresponds to the _frame struct in include\cpython\frameobject.h in the CPython install.
class PyFrameObject : public PyVarObject {
public:
    PyFrameObject *f_back;      /* previous frame, or NULL */
    PyObject *f_code;           /* code segment */
    PyObject *f_builtins;       /* builtin symbol table (PyDictObject) */
    PyObject *f_globals;        /* global symbol table (PyDictObject) */
    PyObject *f_locals;         /* local symbol table (any mapping) */
    PyObject **f_valuestack;    /* points after the last local */
};

#define CO_MAXBLOCKS 20
typedef struct {
    int b_type;         /* what kind of block this is */
    int b_handler;      /* where to jump to find handler */
    int b_level;        /* value stack level to pop to */
} PyTryBlock;

class PyFrameObject25_33 : public PyFrameObject {
public:
        /* Next free slot in f_valuestack.  Frame creation sets to f_valuestack.
       Frame evaluation usually NULLs it, but a frame that yields sets it
       to the current stack top. */
    PyObject **f_stacktop;
    PyObject *f_trace;          /* Trace function */
    PyObject * f_exc_type, *f_exc_value, *f_exc_traceback;
    PyThreadState* f_tstate;
    int f_lasti;                /* Last instruction if called */
    /* As of 2.3 f_lineno is only valid when tracing is active (i.e. when
       f_trace is set) -- at other times use PyCode_Addr2Line instead. */
    int f_lineno;               /* Current line number */
    int f_iblock;       /* index in f_blockstack */
    PyTryBlock f_blockstack[CO_MAXBLOCKS]; /* for try and loop blocks */
    PyObject *f_localsplus[1];    /* locals+stack, dynamically sized */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 2 && (minorVersion >= 5 && minorVersion <= 7) ||
            majorVersion == 3 && (minorVersion >= 0 && minorVersion <= 3);
    }

    static bool IsFor(PythonVersion version) {
        return ((version >= PythonVersion_25 && version <= PythonVersion_27) ||
            (version >= PythonVersion_30 && version <= PythonVersion_33));
    }
};

class PyFrameObject34_36 : public PyFrameObject {
public:
        /* Next free slot in f_valuestack.  Frame creation sets to f_valuestack.
       Frame evaluation usually NULLs it, but a frame that yields sets it
       to the current stack top. */
    PyObject **f_stacktop;
    PyObject *f_trace;          /* Trace function */
    PyObject * f_exc_type, *f_exc_value, *f_exc_traceback;
    /* Borrowed reference to a generator, or NULL */
    PyObject *f_gen;

    int f_lasti;                /* Last instruction if called */
    /* As of 2.3 f_lineno is only valid when tracing is active (i.e. when
       f_trace is set) -- at other times use PyCode_Addr2Line instead. */
    int f_lineno;               /* Current line number */
    int f_iblock;       /* index in f_blockstack */
    char f_executing;           /* whether the frame is still executing */
    PyTryBlock f_blockstack[CO_MAXBLOCKS]; /* for try and loop blocks */
    PyObject *f_localsplus[1];    /* locals+stack, dynamically sized */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion >= 4 && minorVersion <= 6;
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_34 && version <= PythonVersion_36;
    }
};

// 3.7 - 3.9
class PyFrameObject37_39 : public PyFrameObject {
public:
        /* Next free slot in f_valuestack.  Frame creation sets to f_valuestack.
       Frame evaluation usually NULLs it, but a frame that yields sets it
       to the current stack top. */
    PyObject **f_stacktop;
    PyObject *f_trace;          /* Trace function */
    char f_trace_lines;         /* Emit per-line trace events? */
    char f_trace_opcodes;       /* Emit per-opcode trace events? */
    /* Borrowed reference to a generator, or NULL */
    PyObject *f_gen;

    int f_lasti;                /* Last instruction if called */
    /* As of 2.3 f_lineno is only valid when tracing is active (i.e. when
       f_trace is set) -- at other times use PyCode_Addr2Line instead. */
    int f_lineno;               /* Current line number */
    int f_iblock;       /* index in f_blockstack */
    char f_executing;           /* whether the frame is still executing */
    PyTryBlock f_blockstack[CO_MAXBLOCKS]; /* for try and loop blocks */
    PyObject *f_localsplus[1];    /* locals+stack, dynamically sized */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && (minorVersion >= 7 && minorVersion <= 9);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_37 && version <= PythonVersion_39;
    }
};

enum _framestate {
    FRAME_CREATED = -2,
    FRAME_SUSPENDED = -1,
    FRAME_EXECUTING = 0,
    FRAME_RETURNED = 1,
    FRAME_UNWINDING = 2,
    FRAME_RAISED = 3,
    FRAME_CLEARED = 4
};

typedef signed char PyFrameState;

// 3.10
// corresponds to the _frame struct in include\cpython\frameobject.h in the CPython install.
class PyFrameObject310 : public PyFrameObject {
public:
    PyObject *f_trace;          /* Trace function */
    int f_stackdepth;           /* Depth of value stack */
    char f_trace_lines;         /* Emit per-line trace events? */
    char f_trace_opcodes;       /* Emit per-opcode trace events? */

    /* Borrowed reference to a generator, or NULL */
    PyObject *f_gen;

    int f_lasti;                /* Last instruction if called */
    /* As of 2.3 f_lineno is only valid when tracing is active (i.e. when
       f_trace is set) -- at other times use PyCode_Addr2Line instead. */
    int f_lineno;               /* Current line number */
    int f_iblock;               /* index in f_blockstack */
    PyFrameState f_state;       /* What state the frame is in */
    PyTryBlock f_blockstack[CO_MAXBLOCKS]; /* for try and loop blocks */
    PyObject *f_localsplus[1];    /* locals+stack, dynamically sized */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion >= 10;
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_310;
    }
};


typedef void (*destructor)(PyObject *);

// 2.4 - 3.10
// corresponds to the PyMethodDef struct in include\methodobject.h in the CPython install
class PyMethodDef {
public:
    char    *ml_name;    /* The name of the built-in function/method */
    PyObject *m_self;
    PyObject *m_module;
    PyObject *m_reflist;
};


// 
// 2.5 - 3.10
// corresponds to the _typeobject struct in include\cpython\object.h in the CPython install
// While these are compatible there are fields only available on later versions.
class PyTypeObject : public PyVarObject {
public:
    const char *tp_name; /* For printing, in format "<module>.<name>" */
    size_t tp_basicsize, tp_itemsize; /* For allocation */

    /* Methods to implement standard operations */

    destructor tp_dealloc;
    union {
        void* tp_print; // < 3.8
        void* tp_vectorcall_offset; // >= 3.8
    };
    void *tp_getattr;
    void *tp_setattr;
    union {
        void *tp_compare; /* 2.4 - 3.4 */
        void *tp_as_async; /* > 3.5 */
    };
    void *tp_repr;

    /* Method suites for standard classes */

    void *tp_as_number;
    void *tp_as_sequence;
    void *tp_as_mapping;

    /* More standard operations (here for binary compatibility) */

    void *tp_hash;
    void *tp_call;
    void *tp_str;
    void *tp_getattro;
    void *tp_setattro;

    /* Functions to access object as input/output buffer */
    void *tp_as_buffer;

    /* Flags to define presence of optional/expanded features */
    long tp_flags;

    const char *tp_doc; /* Documentation string */

    /* Assigned meaning in release 2.0 */
    /* call function for all accessible objects */
    void *tp_traverse;

    /* delete references to contained objects */
    void *tp_clear;

    /* Assigned meaning in release 2.1 */
    /* rich comparisons */
    void *tp_richcompare;

    /* weak reference enabler */
    size_t tp_weaklistoffset;

    /* Added in release 2.2 */
    /* Iterators */
    void *tp_iter;
    void *tp_iternext;

    /* Attribute descriptor and subclassing stuff */
    PyMethodDef *tp_methods;
    struct PyMemberDef *tp_members;
    struct PyGetSetDef *tp_getset;
    struct _typeobject *tp_base;
    PyObject *tp_dict;
    void *tp_descr_get;
    void *tp_descr_set;
    size_t tp_dictoffset;
    void *tp_init;
    void *tp_alloc;
    void *tp_new;
    void *tp_free; /* Low-level free-memory routine */
    void *tp_is_gc; /* For PyObject_IS_GC */
    PyObject *tp_bases;
    PyObject *tp_mro; /* method resolution order */
    PyObject *tp_cache;
    PyObject *tp_subclasses;
    PyObject *tp_weaklist;
    void *tp_del;

    /* Type attribute cache version tag. Added in version 2.6 */
    unsigned int tp_version_tag;
};

// 2.4 - 3.10
// corresponds to the PyTupleObject struct in include\cpython\tupleobject.h in the CPython install
class PyTupleObject : public PyVarObject {
public:
    PyObject *ob_item[1];

    /* ob_item contains space for 'ob_size' elements.
     * Items must normally not be NULL, except during construction when
     * the tuple is not yet visible outside the function that builds it.
     */
};

// 2.4 - 3.10
// corresponds to the PyCFunctionObject struct in include\cpython\methodobject.h in the CPython install
class PyCFunctionObject : public PyObject {
public:
    PyMethodDef *m_ml;      /* Description of the C function to call */
    PyObject    *m_self;    /* Passed as 'self' arg to the C func, can be NULL */
    PyObject    *m_module;  /* The __module__ attribute, can be anything */
};

typedef int (*Py_tracefunc)(PyObject *, PyFrameObject *, int, PyObject *);

#define PyTrace_CALL 0
#define PyTrace_EXCEPTION 1
#define PyTrace_LINE 2
#define PyTrace_RETURN 3
#define PyTrace_C_CALL 4
#define PyTrace_C_EXCEPTION 5
#define PyTrace_C_RETURN 6

class PyInterpreterState {
};

class PyThreadState { };

class PyThreadState_25_27 : public PyThreadState {
public:
    /* See Python/ceval.c for comments explaining most fields */

    PyThreadState *next;
    PyInterpreterState *interp;

    PyFrameObject *frame;
    int recursion_depth;
    /* 'tracing' keeps track of the execution depth when tracing/profiling.
       This is to prevent the actual trace/profile code from being recorded in
       the trace/profile. */
    int tracing;
    int use_tracing;

    Py_tracefunc c_profilefunc;
    Py_tracefunc c_tracefunc;
    PyObject *c_profileobj;
    PyObject *c_traceobj;

    PyObject *curexc_type;
    PyObject *curexc_value;
    PyObject *curexc_traceback;

    PyObject *exc_type;
    PyObject *exc_value;
    PyObject *exc_traceback;

    PyObject *dict;  /* Stores per-thread state */

    /* tick_counter is incremented whenever the check_interval ticker
     * reaches zero. The purpose is to give a useful measure of the number
     * of interpreted bytecode instructions in a given thread.  This
     * extremely lightweight statistic collector may be of interest to
     * profilers (like psyco.jit()), although nothing in the core uses it.
     */
    int tick_counter;

    int gilstate_counter;

    PyObject *async_exc; /* Asynchronous exception to raise */
    long thread_id; /* Thread id where this tstate was created */

    /* XXX signal handlers should also be here */
    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 2 && (minorVersion >= 5 && minorVersion <= 7);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_25 && version <= PythonVersion_27;
    }
};

class PyThreadState_30_33 : public PyThreadState {
public:
    PyThreadState *next;
    PyInterpreterState *interp;

    PyFrameObject *frame;
    int recursion_depth;
    char overflowed; /* The stack has overflowed. Allow 50 more calls
                        to handle the runtime error. */
    char recursion_critical; /* The current calls must not cause 
                                a stack overflow. */
    /* 'tracing' keeps track of the execution depth when tracing/profiling.
       This is to prevent the actual trace/profile code from being recorded in
       the trace/profile. */
    int tracing;
    int use_tracing;

    Py_tracefunc c_profilefunc;
    Py_tracefunc c_tracefunc;
    PyObject *c_profileobj;
    PyObject *c_traceobj;

    PyObject *curexc_type;
    PyObject *curexc_value;
    PyObject *curexc_traceback;

    PyObject *exc_type;
    PyObject *exc_value;
    PyObject *exc_traceback;

    PyObject *dict;  /* Stores per-thread state */

    /* tick_counter is incremented whenever the check_interval ticker
     * reaches zero. The purpose is to give a useful measure of the number
     * of interpreted bytecode instructions in a given thread.  This
     * extremely lightweight statistic collector may be of interest to
     * profilers (like psyco.jit()), although nothing in the core uses it.
     */
    int tick_counter;

    int gilstate_counter;

    PyObject *async_exc; /* Asynchronous exception to raise */
    long thread_id; /* Thread id where this tstate was created */

    /* XXX signal handlers should also be here */
    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && (minorVersion >= 0 && minorVersion <= 3);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_30 && version <= PythonVersion_33;
    }
};

class PyThreadState_34_36 : public PyThreadState {
public:
    PyThreadState *prev;
    PyThreadState *next;
    PyInterpreterState *interp;

    PyFrameObject *frame;
    int recursion_depth;
    char overflowed; /* The stack has overflowed. Allow 50 more calls
                        to handle the runtime error. */
    char recursion_critical; /* The current calls must not cause
                                a stack overflow. */
    /* 'tracing' keeps track of the execution depth when tracing/profiling.
        This is to prevent the actual trace/profile code from being recorded in
        the trace/profile. */
    int tracing;
    int use_tracing;

    Py_tracefunc c_profilefunc;
    Py_tracefunc c_tracefunc;
    PyObject *c_profileobj;
    PyObject *c_traceobj;

    PyObject *curexc_type;
    PyObject *curexc_value;
    PyObject *curexc_traceback;

    PyObject *exc_type;
    PyObject *exc_value;
    PyObject *exc_traceback;

    PyObject *dict;  /* Stores per-thread state */

    int gilstate_counter;

    PyObject *async_exc; /* Asynchronous exception to raise */

    long thread_id; /* Thread id where this tstate was created */
    /* XXX signal handlers should also be here */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion >= 4 && minorVersion <= 6;
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_34 && version <= PythonVersion_36;
    }
};

struct _PyErr_StackItem {
    PyObject *exc_type, *exc_value, *exc_traceback;
    struct _PyErr_StackItem *previous_item;
};

// 3.7 - 3.9
class PyThreadState_37_39 : public PyThreadState {
public:
    PyThreadState * prev;
    PyThreadState *next;
    PyInterpreterState *interp;

    PyFrameObject *frame;
    int recursion_depth;
    char overflowed; /* The stack has overflowed. Allow 50 more calls
                     to handle the runtime error. */
    char recursion_critical; /* The current calls must not cause
                             a stack overflow. */
                             /* 'tracing' keeps track of the execution depth when tracing/profiling.
                             This is to prevent the actual trace/profile code from being recorded in
                             the trace/profile. */
    int stackcheck_counter;

    int tracing;
    int use_tracing;

    Py_tracefunc c_profilefunc;
    Py_tracefunc c_tracefunc;
    PyObject *c_profileobj;
    PyObject *c_traceobj;

    PyObject *curexc_type;
    PyObject *curexc_value;
    PyObject *curexc_traceback;

    _PyErr_StackItem exc_state;
    _PyErr_StackItem *exc_info;

    PyObject *dict;  /* Stores per-thread state */

    int gilstate_counter;

    PyObject *async_exc; /* Asynchronous exception to raise */

    unsigned long thread_id; /* Thread id where this tstate was created */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && (minorVersion >= 7 && minorVersion <= 9);
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_37 && version <= PythonVersion_39;
    }
};

typedef struct _cframe {
    /* This struct will be threaded through the C stack
     * allowing fast access to per-thread state that needs
     * to be accessed quickly by the interpreter, but can
     * be modified outside of the interpreter.
     *
     * WARNING: This makes data on the C stack accessible from
     * heap objects. Care must be taken to maintain stack
     * discipline and make sure that instances of this struct cannot
     * accessed outside of their lifetime.
     */
    int use_tracing;
    struct _cframe *previous;
} CFrame;

// 3.10
// corresponds to the _ts struct in include\cpython\pystate.h in the CPython install
class PyThreadState_310 : public PyThreadState {
public:
    PyThreadState * prev;
    PyThreadState *next;
    PyInterpreterState *interp;

    PyFrameObject *frame;
    int recursion_depth;
    int recursion_headroom; /* Allow 50 more calls to handle any errors. */
    int stackcheck_counter;

    /* 'tracing' keeps track of the execution depth when tracing/profiling.
       This is to prevent the actual trace/profile code from being recorded in
       the trace/profile. */
    int tracing;

    /* Pointer to current CFrame in the C stack frame of the currently,
     * or most recently, executing _PyEval_EvalFrameDefault. */
    CFrame *cframe;

    Py_tracefunc c_profilefunc;
    Py_tracefunc c_tracefunc;
    PyObject *c_profileobj;
    PyObject *c_traceobj;

    /* The exception currently being raised */
    PyObject *curexc_type;
    PyObject *curexc_value;
    PyObject *curexc_traceback;

    /* The exception currently being handled, if no coroutines/generators
     * are present. Always last element on the stack referred to be exc_info.
     */
    _PyErr_StackItem exc_state;

    /* Pointer to the top of the stack of the exceptions currently
     * being handled */
    _PyErr_StackItem *exc_info;

    PyObject *dict;  /* Stores per-thread state */

    int gilstate_counter;

    PyObject *async_exc; /* Asynchronous exception to raise */
    unsigned long thread_id; /* Thread id where this tstate was created */

    static bool IsFor(int majorVersion, int minorVersion) {
        return majorVersion == 3 && minorVersion >= 10;
    }

    static bool IsFor(PythonVersion version) {
        return version >= PythonVersion_310;
    }
};

class PyIntObject : public PyObject {
public:
    long ob_ival;
};

class Py3kLongObject : public PyVarObject {
public:
    DWORD ob_digit[1];
};

class PyOldStyleClassObject : public PyObject {
public:
    PyObject *cl_bases; /* A tuple of class objects */
    PyObject *cl_dict; /* A dictionary */
    PyObject *cl_name; /* A string */
    /* The following three are functions or NULL */
    PyObject *cl_getattr;
    PyObject *cl_setattr;
    PyObject *cl_delattr;
};

class PyInstanceObject : public PyObject {
public:
    PyOldStyleClassObject *in_class; /* The class object */
    PyObject *in_dict; /* A dictionary */
    PyObject *in_weakreflist; /* List of weak references */
};

#endif
