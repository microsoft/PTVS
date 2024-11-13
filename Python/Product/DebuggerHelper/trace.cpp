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

#include "stdafx.h"


// Opaque data type; we only use field offsets and function pointers provided to us by debugger to work with values of this type.
struct PyObject {};

template<class T>
T ReadField(const void* p, int64_t offset) {
    if (offset < 0)
        return 0;
    return *reinterpret_cast<const T*>(reinterpret_cast<const char*>(p) + offset);
}


extern "C" {

void EvalLoop(void (*bp)());
void ReleasePendingObjects();

// The following struct definitions should be kept in perfect sync with the corresponding ones in C# in Debugger.
// To ensure matching layout, only types which are the same size on all platforms should be used. This means
// no pointers.

#pragma pack(push, 8)

// Offsets of various fields of Python structs.
// These are used in lieu of actual definitions in python.h so that the same helper binary can work on any supported
// CPython version and build.
__declspec(dllexport)
struct {
    struct {
        int64_t ob_type;
    } PyObject;
    struct {
        int64_t ob_size;
    } PyVarObject;
    struct {
        int64_t f_back, f_code, f_globals, f_locals, f_lineno, f_frame;
    } PyFrameObject;
    struct {
        int64_t co_filename, co_name;
    } PyCodeObject;
    struct {
        int64_t ob_sval;
    } PyBytesObject;
    struct {
        int64_t sizeof_PyAsciiObjectData, sizeof_PyCompactUnicodeObjectData;
        int64_t length, state, wstr, wstr_length, utf8, utf8_length, data;
    } PyUnicodeObject;
} fieldOffsets;

// Addresses of various Py..._Type globals.
__declspec(dllexport)
struct {
    uint64_t PyBytes_Type;
    uint64_t PyUnicode_Type;
} types;

// Python API functions
__declspec(dllexport)
struct {
    uint64_t Py_DecRef;
    uint64_t PyFrame_FastToLocals;
    uint64_t PyRun_StringFlags;
    uint64_t PyErr_Fetch;
    uint64_t PyErr_Restore;
    uint64_t PyErr_Occurred;
    uint64_t PyObject_Str;
    uint64_t PyEval_SetTraceAllThreads;
    uint64_t PyGILState_Ensure;
    uint64_t PyGILState_Release;
    uint64_t Py_Initialize;
	uint64_t Py_Finalize;
} functionPointers;

void Py_DecRef(PyObject* a1) {
    return reinterpret_cast<decltype(&Py_DecRef)>(functionPointers.Py_DecRef)(a1);
}

void PyFrame_FastToLocals(PyObject* a1) {
    return reinterpret_cast<decltype(&PyFrame_FastToLocals)>(functionPointers.PyFrame_FastToLocals)(a1);
}

PyObject* PyRun_StringFlags(const char* a1, int a2, PyObject* a3, PyObject* a4, void* a5) {
    return reinterpret_cast<decltype(&PyRun_StringFlags)>(functionPointers.PyRun_StringFlags)(a1, a2, a3, a4, a5);
}

void PyErr_Fetch(PyObject** a1, PyObject** a2, PyObject** a3) {
    return reinterpret_cast<decltype(&PyErr_Fetch)>(functionPointers.PyErr_Fetch)(a1, a2, a3);
}

void PyErr_Restore(PyObject* a1, PyObject* a2, PyObject* a3) {
    return reinterpret_cast<decltype(&PyErr_Restore)>(functionPointers.PyErr_Restore)(a1, a2, a3);
}

PyObject* PyErr_Occurred() {
    return reinterpret_cast<decltype(&PyErr_Occurred)>(functionPointers.PyErr_Occurred)();
}

PyObject* PyObject_Str(PyObject* o) {
    return reinterpret_cast<decltype(&PyObject_Str)>(functionPointers.PyObject_Str)(o);
}

/* Py_tracefunc return -1 when raising an exception, or 0 for success. */
typedef int (*Py_tracefunc)(void* obj, void* frame, int what, void* arg);

void
PyEval_SetTraceAllThreads(Py_tracefunc func, PyObject* arg) {
    return reinterpret_cast<decltype(&PyEval_SetTraceAllThreads)>(functionPointers.PyEval_SetTraceAllThreads)(func, arg);
}

typedef
enum { PyGILState_LOCKED, PyGILState_UNLOCKED } PyGILState_STATE;

PyGILState_STATE PyGILState_Ensure() {
	return reinterpret_cast<decltype(&PyGILState_Ensure)>(functionPointers.PyGILState_Ensure)();
}

void PyGILState_Release(PyGILState_STATE state) {
    return reinterpret_cast<decltype(&PyGILState_Release)>(functionPointers.PyGILState_Release)(state);
}

void Py_Initialize() {
	return reinterpret_cast<decltype(&Py_Initialize)>(functionPointers.Py_Initialize)();
}

void Py_Finalize() {
	return reinterpret_cast<decltype(&Py_Finalize)>(functionPointers.Py_Finalize)();
}

// A string provided by the debugger (e.g. for file names). This is actually a variable-length struct,
// with _countof(data) == length + 1 - the extra wchar_t is the null terminator.
struct DebuggerString {
    int32_t length;
    wchar_t data[1];
};

// Information about active breakpoints, communicated by debugger to be used by TraceFunc.
struct BreakpointData {
    // Highest line number for which there is a breakpoint (and therefore an element in lineNumbers).
    int32_t maxLineNumber;
    // Pointer to array of line numbers.
    // Indices are line numbers, elements are indices into fileNamesOffsets. Every line number is associated
    // with zero or more consecutive elements in fileNames, starting at the given index. Each sequence of
    // offsets in fileNamesOffsets is terminated by 0. Sequence at index 0 is reserved for an empty sequence -
    // the corresponding elements in fileNames are set accordingly (i.e. fileNames[0]==fileNames[1]==-1),
    // but this can be assumed for any null index.
    uint64_t lineNumbers;
    // Pointer to array of string offsets.
    // Elements are offsets to strings stored inside strings, relative to its beginning.
    uint64_t fileNamesOffsets;
    // Pointer to a block of memory containing DebuggerString objects that are referenced by fileNamesOffsets.
    // The first string (offset 0) is always zero-length empty string.
    uint64_t strings;
};

// It is possible that the process is paused and a breakpoint is set while we are inside the trace function.
// To prevent debugger from stepping on the trace function's toes, a simple swapping scheme is used.
//
// When TraceFunc is entered and starts checking for a breakpoint hit, it assumes that the current data it
// should use is the one in breakpointData[currentBreakpointData]. Before doing anything else, it atomically
// sets breakpointDataInUseByTraceFunc = currentBreakpointData, which indicates to debugger that this data
// is in use and should not be modified. TraceFunc then checks the value of currentBreakpointData again
// to make sure it was not modified (which is possible because debugger could have done that between reading
// currentBreakpointData and setting breakpointDataInUseByTraceFunc). If it was modified, then the whole
// process restarts from the beginning; otherwise, TraceFunc uses the data to match trace info against.
//
// From debugger perspective, when it needs to write breakpoint data, it looks at breakpointDataInUseByTraceFunc,
// and picks the other data as the one it will be writing to. It writes to that other data, overwriting existing
// values (and freeing any allocated memory), and then sets currentBreakpointData to index that new data.
//
// Note that while TraceFunc can be interrupted midway through by debugger, debugger cannot be interrupted by
// TraceFunc (because the debuggee is paused when we're writing breakpoints). So debugger doesn't need to sync
// further, aside from picking the correct BreakpointData to overwrite and communicating the choice.

__declspec(dllexport)
volatile BreakpointData breakpointData[2];

__declspec(dllexport)
volatile uint8_t currentBreakpointData, breakpointDataInUseByTraceFunc;

// Only valid when inside OnBreakpointHit.
__declspec(dllexport)
struct {
    int32_t lineNumber;
    uint64_t fileName;
} volatile currentSourceLocation;

// Current stepping action, if any.
__declspec(dllexport)
enum : int32_t {
    STEP_NONE = 0,
    STEP_INTO = 1,
    STEP_OVER = 2,
    STEP_OUT = 3,
} volatile stepKind;

__declspec(dllexport)
volatile uint64_t stepThreadId;

// When step begins, debugger sets this to zero. TraceFunc increments and decrements it whenever a new frame
// is entered or left, and uses the value to determine whenever a step-in completes, or a step falls off the
// end of the originating frame.
__declspec(dllexport)
volatile int32_t steppingStackDepth;

// An entry in a linked list of objects that need Py_DecRef called on them as soon as possible.
// TraceFunc checks this list and does decrefs if needed on every trace event.
struct ObjectToRelease {
    uint64_t pyObject;
    uint64_t next;
};

// The first entry in that list.
__declspec(dllexport)
volatile int64_t objectsToRelease;

__declspec(dllexport)
volatile uint64_t evalLoopThreadId; // 0 if no thread is running EvalLoop

__declspec(dllexport)
volatile uint64_t evalLoopFrame; // pointer to PyFrameObject that should be the context

const int EXPRESSION_EVALUATION_BUFFER_SIZE = 0x1000;

__declspec(dllexport)
volatile char evalLoopInput[EXPRESSION_EVALUATION_BUFFER_SIZE]; // text of the expression, encoded in UTF-8

__declspec(dllexport)
volatile uint64_t evalLoopResult; // pointer to object that was the result of evaluation

__declspec(dllexport)
volatile uint64_t evalLoopExcType; // pointer to exc_type fetched after evaluation

__declspec(dllexport)
volatile uint64_t evalLoopExcValue; // pointer to exc_value fetched after evaluation

__declspec(dllexport)
volatile uint64_t evalLoopExcStr; // pointer to str(exc_value)

__declspec(dllexport)
volatile uint32_t evalLoopSEHCode; // if a structured exception occurred during eval, the return value of GetExceptionCode

#pragma pack(pop)



__declspec(dllexport)
bool StringEquals(const DebuggerString* debuggerString, const void* pyString) {
    // In 3.x, we only need to support Unicode strings - bytes is no longer a string type.
    void* ob_type = ReadField<void*>(pyString, fieldOffsets.PyObject.ob_type);
    if (reinterpret_cast<uint64_t>(ob_type) != types.PyUnicode_Type) {
        return false;
    }

    int32_t my_length = debuggerString->length;
    const wchar_t* my_data = debuggerString->data;

#pragma warning(push)
#pragma warning(disable:4201) // nonstandard extension used: nameless struct/union
    union {
        struct {
            unsigned interned: 2;
            unsigned kind: 3;
            unsigned compact: 1;
            unsigned ascii: 1;
            unsigned ready: 1;
        };
        char state;
    };
#pragma warning(pop)

    state = ReadField<char>(pyString, fieldOffsets.PyUnicodeObject.state);

    if (!ready && fieldOffsets.PyUnicodeObject.wstr != 0) {
        auto wstr = ReadField<wchar_t*>(pyString, fieldOffsets.PyUnicodeObject.wstr);
        if (!wstr) {
            return false;
        }

        auto wstr_length = ReadField<uint32_t>(pyString, fieldOffsets.PyUnicodeObject.wstr_length);
        if (static_cast<int32_t>(wstr_length) != my_length) {
            return false;
        }

        return memcmp(wstr, my_data, my_length * static_cast<size_t>(2)) == 0;
    }

    auto length = ReadField<SSIZE_T>(pyString, fieldOffsets.PyUnicodeObject.length);
    if (length != my_length) {
        return false;
    }

    const void* data;
    if (!compact) {
        data = ReadField<void*>(pyString, fieldOffsets.PyUnicodeObject.data);
    } else if (ascii) {
        data = reinterpret_cast<const char*>(pyString) + fieldOffsets.PyUnicodeObject.sizeof_PyAsciiObjectData;
    } else {
        data = reinterpret_cast<const char*>(pyString) + fieldOffsets.PyUnicodeObject.sizeof_PyCompactUnicodeObjectData;
    }

    if (kind == 2) {
        return memcmp(data, my_data, my_length * static_cast<size_t>(2)) == 0;
    } else if (kind == 1 || ascii) {
        auto asciiData = reinterpret_cast<const char*>(data);
        for (int32_t i = 0; i < my_length; ++i) {
            if (asciiData[i] != my_data[i]) {
                return false;
            }
        }
        return true;
    } else if (kind == 4) {
        auto ucs4Data = reinterpret_cast<const uint32_t*>(data);
        for (int32_t i = 0; i < my_length; ++i) {
            if (ucs4Data[i] != my_data[i]) {
                return false;
            }
        }
        return true;
    } else {
        return false;
    }
}


// The sole purpose of these functions is to serve as debugger callbacks - debugger sets breakpoints on them,
// and thus gets notifications when they're called.

__declspec(dllexport) __declspec(noinline)
void OnBreakpointHit() {
    volatile char dummy = 0;
    UNREFERENCED_PARAMETER(dummy);
}

// Note that this is only reported for step in/over, not for step out - debugger handles the latter via native breakpoints.
__declspec(dllexport) __declspec(noinline)
void OnStepComplete() {
    volatile char dummy = 0;
    UNREFERENCED_PARAMETER(dummy);
}

// Stepping operation fell through the end of the frame on which it began - debugger should handle the rest of the step.
__declspec(dllexport) __declspec(noinline)
void OnStepFallThrough() {
    volatile char dummy = 0;
    UNREFERENCED_PARAMETER(dummy);
}

// EvalLoop completed evaluation of input; evalLoopResult points at the resulting object if any, and evalLoopException points at exception if any.
__declspec(dllexport) __declspec(noinline)
void OnEvalComplete() {
    volatile char dummy = 0;
    UNREFERENCED_PARAMETER(dummy);
}


#pragma warning(push)
#pragma warning(disable:6320) // Exception-filter expression is the constant EXCEPTION_EXECUTE_HANDLER
void EvalLoop(void (*bp)()) {
    evalLoopThreadId = GetCurrentThreadId();
    bp();
    __try {
        while (*evalLoopInput) {
            // Prevent re-entrant eval
            evalLoopThreadId = 0;

            __try {
                auto frame = reinterpret_cast<PyObject*>(evalLoopFrame);
                PyFrame_FastToLocals(frame);

                auto f_globals = ReadField<PyObject*>(frame, fieldOffsets.PyFrameObject.f_globals);
                auto f_locals = ReadField<PyObject*>(frame, fieldOffsets.PyFrameObject.f_locals);

                PyObject *orig_exc_type, *orig_exc_value, *orig_exc_tb;
                PyErr_Fetch(&orig_exc_type, &orig_exc_value, &orig_exc_tb);
                PyErr_Restore(nullptr, nullptr, nullptr);

                evalLoopResult = 0;
                evalLoopExcType = 0;
                evalLoopExcValue = 0;
                evalLoopExcStr = 0;
                evalLoopSEHCode = 0;
                PyObject* result = PyRun_StringFlags((char*)evalLoopInput, /*Py_eval_input*/ 258, f_globals, f_locals, nullptr);
                *evalLoopInput = '\0';

                PyObject *exc_type, *exc_value, *exc_tb, *exc_str;
                PyErr_Fetch(&exc_type, &exc_value, &exc_tb);
                exc_str = exc_value ? PyObject_Str(exc_value) : nullptr;

                evalLoopResult = reinterpret_cast<uint64_t>(result);
                evalLoopExcType = reinterpret_cast<uint64_t>(exc_type);
                evalLoopExcValue = reinterpret_cast<uint64_t>(exc_value);
                evalLoopExcStr = reinterpret_cast<uint64_t>(exc_str);
                evalLoopThreadId = GetCurrentThreadId();
                OnEvalComplete();

                result = reinterpret_cast<PyObject*>(evalLoopResult);
                Py_DecRef(result);
                Py_DecRef(exc_type);
                Py_DecRef(exc_value);
                Py_DecRef(exc_tb);
                Py_DecRef(exc_str);

                PyErr_Restore(orig_exc_type, orig_exc_value, orig_exc_tb);
            } __except (EXCEPTION_EXECUTE_HANDLER) {
                evalLoopResult = 0;
                evalLoopSEHCode = GetExceptionCode();
                evalLoopThreadId = GetCurrentThreadId();
                OnEvalComplete();
            }

            ReleasePendingObjects();
        }
    } __finally {
        evalLoopThreadId = 0;
    }
}
#pragma warning(pop)


static void TraceLine(void* frame) {
    // Let's check for stepping first.
    if (stepKind == STEP_INTO || (stepKind == STEP_OVER && steppingStackDepth == 0)) {
        if (stepThreadId == GetCurrentThreadId()) {
            EvalLoop(OnStepComplete);
            return;
        }
    }

    // See the large comment at the declaration of breakpointData for details of how the below synchronization scheme works.
    uint8_t iData;
    do {
        iData = currentBreakpointData;
        // BreakpointManager.WriteBreakpoints may run at this point and change currentBreakpointData ...
        breakpointDataInUseByTraceFunc = iData; // (locks breakpointData[iData] from any modification by debugger)
        // ... so check it again to ensure that it's still the same, and retry if it's not.
    } while (iData != currentBreakpointData);

    // We can now safely use breakpointData[iData]
    const auto& bpData = breakpointData[iData];

    int f_lineno = ReadField<int>(frame, fieldOffsets.PyFrameObject.f_lineno);
    if (f_lineno > bpData.maxLineNumber) {
        return;
    }

    auto lineNumbers = reinterpret_cast<const int32_t*>(bpData.lineNumbers);
    int fileNamesIndex = lineNumbers[f_lineno];
    if (!fileNamesIndex) {
        return;
    }

    void* co_filename = nullptr;
    if (fieldOffsets.PyFrameObject.f_frame == 0) {
        // We're on 3.10 or earlier. f_code is directly off of the frame.
        void* f_code = ReadField<void*>(frame, fieldOffsets.PyFrameObject.f_code);
        co_filename = ReadField<void*>(f_code, fieldOffsets.PyCodeObject.co_filename);
    }
    else {
        // We're on 3.11 or later. f_frame (PyInterpreterFrame) is off of the frame. It has
        // the f_code object.
        void* f_frame = ReadField<void*>(frame, fieldOffsets.PyFrameObject.f_frame);
        void* f_code = ReadField<void*>(f_frame, fieldOffsets.PyFrameObject.f_code);
        co_filename = ReadField<void*>(f_code, fieldOffsets.PyCodeObject.co_filename);
    }
    if (co_filename == nullptr) {
        return;
    }

    auto fileNamesOffsets = reinterpret_cast<const int32_t*>(bpData.fileNamesOffsets);
    auto strings = reinterpret_cast<const char*>(bpData.strings);

    for (const int32_t* pFileNameOffset = &fileNamesOffsets[fileNamesIndex]; *pFileNameOffset; ++pFileNameOffset) {
        const DebuggerString* fileName = reinterpret_cast<const DebuggerString*>(strings + *pFileNameOffset);
        if (StringEquals(fileName,co_filename)) {
            currentSourceLocation.lineNumber = f_lineno;
            currentSourceLocation.fileName = reinterpret_cast<uint64_t>(fileName);
            EvalLoop(OnBreakpointHit);
            return;
        }
    }
}


static void TraceCall(void* frame) {
    UNREFERENCED_PARAMETER(frame);

    if (stepThreadId == GetCurrentThreadId()) {
        ++steppingStackDepth;
        if (stepKind == STEP_INTO) {
            stepKind = STEP_NONE;
            EvalLoop(OnStepComplete);
        }
    }
}


static void TraceReturn(void* frame) {
    UNREFERENCED_PARAMETER(frame);

    if (stepThreadId == GetCurrentThreadId()) {
        --steppingStackDepth;
        if (stepKind != STEP_NONE) {
            if (steppingStackDepth < 0) {
                EvalLoop(OnStepFallThrough);
            }
        }
    }
}

#pragma warning(push)
#pragma warning(disable:4211 28112) // nonstandard extension used: redefined extern to static
                                    // If "objectsToRelease" is accessed through interlocked function even once,
                                        //it must always be accessed through interlocked functions
                                        //Waring supressed because there is no other interlocked operation performed on
                                        //This variable and we want to avoid it here due to performance reasons

static void ReleasePendingObjects() {
    if (objectsToRelease) {
        auto otr = reinterpret_cast<ObjectToRelease*>(InterlockedExchange64(&objectsToRelease, 0));
        while (otr != nullptr) {
            // Releasing an object may trigger execution of its __del__ function, which will cause re-entry to this code,
            // so null out the reference before releasing, and check for nulls in the list.
            auto obj = otr->pyObject;
            if (obj != 0) {
                otr->pyObject = 0;
                Py_DecRef(reinterpret_cast<PyObject*>(obj));
            }

            auto next = reinterpret_cast<ObjectToRelease*>(otr->next);
            VirtualFree(otr, 0, MEM_RELEASE);
            otr = next;
        }
    }
}

#pragma warning(pop)

#define PyTrace_CALL 0
#define PyTrace_EXCEPTION 1
#define PyTrace_LINE 2
#define PyTrace_RETURN 3
#define PyTrace_C_CALL 4
#define PyTrace_C_EXCEPTION 5
#define PyTrace_C_RETURN 6

__declspec(dllexport)
int TraceFunc(void* obj, void* frame, int what, void* arg) {
    UNREFERENCED_PARAMETER(arg);
    UNREFERENCED_PARAMETER(obj);

    ReleasePendingObjects();

    switch (what) {
    case PyTrace_LINE:
        TraceLine(frame);
        break;
    case PyTrace_CALL:
        TraceCall(frame);
        break;
    case PyTrace_RETURN:
        TraceReturn(frame);
        break;
    }

    return 0;
}

void* InitialEvalFrameFunc(void* ts, void* f, int throwFlag);

typedef void* (*_PyFrameEvalFunction)(void*, void*, int);
_PyFrameEvalFunction CurrentEvalFrameFunc = InitialEvalFrameFunc;

__declspec(dllexport)
_PyFrameEvalFunction DefaultEvalFrameFunc = nullptr;

// Initial EvalFrameFunc that is used to set the trace function. 
volatile unsigned long _isTracing = 0;
void *InitialEvalFrameFunc(void* ts, void* f, int throwFlag)
{
    // If were in 3.12, we need to set the trace function ourselves. 
    // This is because just writing to the use_tracing flag is no longer enough. Internally CPython
    // doesn't just trace everything if the flag is set. Instead tracing now uses the sys.monitoring
    // api under the covers. That's a lot more than just flipping a flag.
    if (functionPointers.PyEval_SetTraceAllThreads != 0 && ::InterlockedCompareExchange(&_isTracing, 1, 0) == 0) {
        auto gilState = PyGILState_Ensure();
        PyEval_SetTraceAllThreads(TraceFunc, nullptr);
        PyGILState_Release(gilState);
    }

	// Rewrite the current EvalFrameFunc so we don't bother attempting to register the trace function again.
	CurrentEvalFrameFunc = DefaultEvalFrameFunc;

    if (DefaultEvalFrameFunc)
        return (*DefaultEvalFrameFunc)(ts, f, throwFlag);

    return nullptr;
}



// Function that is inserted into the current thread state by the debugger as the function to call
// in order to evaluate a frame. This is done so that the debugger can intercept the call
__declspec(dllexport)
void* EvalFrameFunc(void* ts, void* f, int throwFlag)
{
	return (*CurrentEvalFrameFunc)(ts, f, throwFlag);
}


}
