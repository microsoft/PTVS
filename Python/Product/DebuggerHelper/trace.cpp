/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

#include "stdafx.h"


template<class T>
T ReadField(const void* p, int64_t offset) {
    return *reinterpret_cast<const T*>(reinterpret_cast<const char*>(p) + offset);
}


extern "C" {

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
        int64_t f_code, f_locals, f_lineno, f_localsplus;
    } PyFrameObject;
    struct {
        int64_t co_varnames, co_filename, co_name;
    } PyCodeObject;
    struct {
        int64_t ob_sval;
    } PyBytesObject;
    struct {
        int64_t length, str;
    } PyUnicodeObject27;
    struct {
        int64_t PyAsciiObjectData, PyCompactUnicodeObjectData;
        int64_t length, state, wstr, wstr_length, data;
    } PyUnicodeObject33;
} fieldOffsets;

// A function to compare DebuggerString to a Python string object. This is set to either StringEquals27 or
// to StringEquals33 by debugger, depending on the language version.
__declspec(dllexport)
bool (*stringEquals)(const struct DebuggerString* debuggerString, const void* pyString);

// Pointers to the corresponding Python type objects.
__declspec(dllexport)
const void *PyBytes_Type, *PyUnicode_Type;

// A string provided by the debugger (e.g. for file names). This is actually a variable-length struct,
// with _countof(data) == length + 1 - the extra wchar_t is the null terminator.
struct DebuggerString {
    int32_t length;
    wchar_t data[1];
    bool Equals(const void* pyString) const {
        return stringEquals(this, pyString);
    }
};

// Information about active breakpoints, communucated by debugger to be used by TraceFunc.
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
volatile void (*Py_DecRef)(void*);

#pragma pack(pop)


extern "C" __declspec(dllexport)
bool StringEquals27(const DebuggerString* debuggerString, const void* pyString) {
    int32_t my_length = debuggerString->length;
    const wchar_t* my_data = debuggerString->data;

    // In 2.7, we have to be able to compare against either ASCII or Unicode strings, so check type and branch.
    auto ob_type = ReadField<const void*>(pyString, fieldOffsets.PyObject.ob_type);
    if (ob_type == PyBytes_Type) {
        auto ob_size = ReadField<SSIZE_T>(pyString, fieldOffsets.PyVarObject.ob_size);
        if (ob_size != my_length) {
            return false;
        }

        const char* data = reinterpret_cast<const char*>(pyString) + fieldOffsets.PyBytesObject.ob_sval;
        for (int32_t i = 0; i < my_length; ++i) {
            if (data[i] != my_data[i]) {
                return false;
            }
        }
        return true;
    } else if (ob_type == PyUnicode_Type) {
        auto length = ReadField<SSIZE_T>(pyString, fieldOffsets.PyUnicodeObject27.length);
        if (length != my_length) {
            return false;
        }

        const wchar_t* data = ReadField<const wchar_t*>(pyString, fieldOffsets.PyUnicodeObject27.str);
        return memcmp(data, my_data, my_length * 2) == 0;
    } else {
        return false;
    }
}

extern "C" __declspec(dllexport)
bool StringEquals33(const DebuggerString* debuggerString, const void* pyString) {
    // In 3.x, we only need to support Unicode strings - bytes is no longer a string type.
    void* ob_type = ReadField<void*>(pyString, fieldOffsets.PyObject.ob_type);
    if (ob_type != PyUnicode_Type) {
        return false;
    }

    int32_t my_length = debuggerString->length;
    const wchar_t* my_data = debuggerString->data;

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
    state = ReadField<char>(pyString, fieldOffsets.PyUnicodeObject33.state);

    if (!ready) {
        auto wstr = ReadField<wchar_t*>(pyString, fieldOffsets.PyUnicodeObject33.wstr);
        if (!wstr) {
            return false;
        }

        auto wstr_length = ReadField<uint32_t>(pyString, fieldOffsets.PyUnicodeObject33.wstr_length);
        if (wstr_length != my_length) {
            return false;
        }

        return memcmp(wstr, my_data, my_length * 2) == 0;
    }

    auto length = ReadField<SSIZE_T>(pyString, fieldOffsets.PyUnicodeObject33.length);
    if (length != my_length) {
        return false;
    }

    const void* data;
    if (!compact) {
        data = ReadField<void*>(pyString, fieldOffsets.PyUnicodeObject33.data);
    } else if (ascii) {
        data = reinterpret_cast<const char*>(pyString) + fieldOffsets.PyUnicodeObject33.PyAsciiObjectData;
    } else {
        data = reinterpret_cast<const char*>(pyString) + fieldOffsets.PyUnicodeObject33.PyCompactUnicodeObjectData;
    }

    if (kind == 2) {
        return memcmp(data, my_data, my_length * 2) == 0;
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
}

// Note that this is only reported for step in/over, not for step out - debugger handles the latter via native breakpoints.
__declspec(dllexport) __declspec(noinline)
void OnStepComplete() { 
    volatile char dummy = 0;
}

// Stepping operation fell through the end of the frame on which it began - debugger should handle the rest of the step.
__declspec(dllexport) __declspec(noinline)
void OnStepFallThrough() {
    volatile char dummy = 0;
}


static void TraceLine(void* frame) {
	// Let's check for stepping first.
    if (stepKind == STEP_INTO || (stepKind == STEP_OVER && steppingStackDepth == 0)) {
        if (stepThreadId == GetCurrentThreadId()) {
            OnStepComplete();
			return;
        }
    }

    // See the large comment at the declaration of breakpointData for details of how the below synchronization scheme works.
	unsigned iData;
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

    void* f_code = ReadField<void*>(frame, fieldOffsets.PyFrameObject.f_code);
    void* co_filename = ReadField<void*>(f_code, fieldOffsets.PyCodeObject.co_filename);

    auto fileNamesOffsets = reinterpret_cast<const int32_t*>(bpData.fileNamesOffsets);
    auto strings = reinterpret_cast<const char*>(bpData.strings);

    for (const int32_t* pFileNameOffset = &fileNamesOffsets[fileNamesIndex]; *pFileNameOffset; ++pFileNameOffset) {
        const DebuggerString* fileName = reinterpret_cast<const DebuggerString*>(strings + *pFileNameOffset);
        if (fileName->Equals(co_filename)) {
            currentSourceLocation.lineNumber = f_lineno;
            currentSourceLocation.fileName = (uint64_t)fileName;
            OnBreakpointHit();
            return;
        }
    }
}


static void TraceCall(void* frame) {
	if (stepThreadId == GetCurrentThreadId()) {
		++steppingStackDepth;
		if (stepKind == STEP_INTO) {
			stepKind = STEP_NONE;
			OnStepComplete();
		}
	}
}


static void TraceReturn(void* frame) {
	if (stepThreadId == GetCurrentThreadId()) {
		--steppingStackDepth;
		if (stepKind != STEP_NONE) {
			if (steppingStackDepth < 0) {
				OnStepFallThrough();
			}
		}
	}
}


static void ReleasePendingObjects() {
    if (objectsToRelease) {
        auto otr = reinterpret_cast<ObjectToRelease*>(InterlockedExchange64(&objectsToRelease, 0));
        while (otr != nullptr) {
            // Releasing an object may trigger execution of its __del__ function, which will cause re-entry to this code,
            // so null out the reference before releasing, and check for nulls in the list.
            auto obj = otr->pyObject;
            if (obj != 0) {
                otr->pyObject = 0;
                Py_DecRef(reinterpret_cast<void*>(obj));
            }

            auto next = reinterpret_cast<ObjectToRelease*>(otr->next);
            VirtualFree(otr, 0, MEM_RELEASE);
            otr = next;
        }
    }
}


#define PyTrace_CALL 0
#define PyTrace_EXCEPTION 1
#define PyTrace_LINE 2
#define PyTrace_RETURN 3
#define PyTrace_C_CALL 4
#define PyTrace_C_EXCEPTION 5
#define PyTrace_C_RETURN 6

__declspec(dllexport)
int TraceFunc(void* obj, void* frame, int what, void* arg) {
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

}
