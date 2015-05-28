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

// PyDebugAttach.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "PyDebugAttach.h"
#include "..\VsPyProf\python.h"

// _Always_ is not defined for all versions, so make it a no-op if missing.
#ifndef _Always_
#define _Always_(x) x
#endif

using namespace std;

typedef int (Py_IsInitialized)();
typedef void (PyEval_Lock)(); // Acquire/Release lock
typedef void (PyThreadState_API)(PyThreadState *); // Acquire/Release lock
typedef PyInterpreterState* (PyInterpreterState_Head)();
typedef PyThreadState* (PyInterpreterState_ThreadHead)(PyInterpreterState* interp);
typedef PyThreadState* (PyThreadState_Next)(PyThreadState *tstate);
typedef PyThreadState* (PyThreadState_Swap)(PyThreadState *tstate);
typedef int (PyRun_SimpleString)(const char *command);
typedef PyObject* (PyDict_New)();
typedef PyObject* (PyModule_New)(const char *name);
typedef PyObject* (PyModule_GetDict)(PyObject *module);
typedef PyObject* (Py_CompileString)(const char *str, const char *filename, int start);
typedef PyObject* (PyEval_EvalCode)(PyObject *co, PyObject *globals, PyObject *locals);
typedef PyObject* (PyDict_GetItemString)(PyObject *p, const char *key);
typedef PyObject* (PyObject_CallFunctionObjArgs)(PyObject *callable, ...);    // call w/ varargs, last arg should be NULL
typedef void (PyErr_Fetch)(PyObject **, PyObject **, PyObject **);
typedef PyObject* (PyEval_GetBuiltins)();
typedef int (PyDict_SetItemString)(PyObject *dp, const char *key, PyObject *item);
typedef int (PyEval_ThreadsInitialized)();
typedef void (Py_AddPendingCall)(int (*func)(void *), void*);
typedef PyObject* (PyInt_FromLong)(long);
typedef PyObject* (PyString_FromString)(const char* s);
typedef void PyEval_SetTrace(Py_tracefunc func, PyObject *obj);
typedef void (PyErr_Restore)(PyObject *type, PyObject *value, PyObject *traceback);
typedef void (PyErr_Fetch)(PyObject **ptype, PyObject **pvalue, PyObject **ptraceback);
typedef PyObject* (PyErr_Occurred)();
typedef PyObject* (PyErr_Print)();
typedef PyObject* (PyImport_ImportModule) (const char *name);
typedef PyObject* (PyObject_GetAttrString)(PyObject *o, const char *attr_name);
typedef PyObject* (PyObject_SetAttrString)(PyObject *o, const char *attr_name, PyObject* value);
typedef PyObject* (PyBool_FromLong)(long v);
typedef enum { PyGILState_LOCKED, PyGILState_UNLOCKED } PyGILState_STATE;
typedef PyGILState_STATE(PyGILState_Ensure)();
typedef void (PyGILState_Release)(PyGILState_STATE);
typedef unsigned long (_PyEval_GetSwitchInterval)(void);
typedef void (_PyEval_SetSwitchInterval)(unsigned long microseconds);
typedef void* (PyThread_get_key_value)(int);
typedef int (PyThread_set_key_value)(int, void*);
typedef void (PyThread_delete_key_value)(int);
typedef PyGILState_STATE PyGILState_EnsureFunc(void);
typedef void PyGILState_ReleaseFunc(PyGILState_STATE);
typedef PyObject* PyInt_FromSize_t(size_t ival);
typedef PyThreadState *PyThreadState_NewFunc(PyInterpreterState *interp);

class PyObjectHolder;
PyObject* GetPyObjectPointerNoDebugInfo(bool isDebug, PyObject* object);
void DecRef(PyObject* object, bool isDebug);
void IncRef(PyObject* object, bool isDebug);

#define MAX_INTERPRETERS 10

// Helper class so we can use RAII for freeing python objects when they go out of scope
class PyObjectHolder {
private:
    PyObject* _object;
public:
    bool _isDebug;

    PyObjectHolder(bool isDebug) {
        _object = nullptr;
        _isDebug = isDebug;
    }

    PyObjectHolder(bool isDebug, PyObject *object) {
        _object = object;
        _isDebug = isDebug;
    };

    PyObjectHolder(bool isDebug, PyObject *object, bool addRef) {
        _object = object;
        _isDebug = isDebug;
        if (_object != nullptr && addRef) {
            GetPyObjectPointerNoDebugInfo(_isDebug, _object)->ob_refcnt++;
        }
    };

    PyObject* ToPython() {
        return _object;
    }

    ~PyObjectHolder() {
        DecRef(_object, _isDebug);
    }

    PyObject* operator* () {
        return GetPyObjectPointerNoDebugInfo(_isDebug, _object);
    }
};

class InterpreterInfo {
public:
    InterpreterInfo(HMODULE module, bool debug) :
        Interpreter(module),
        CurrentThread(nullptr),
        NewThreadFunction(nullptr),
        PyGILState_Ensure(nullptr),
        Version(PythonVersion_Unknown),
        Call(nullptr),
        IsDebug(debug),
        SetTrace(nullptr),
        PyThreadState_New(nullptr),
        ThreadState_Swap(nullptr) {
    }

    ~InterpreterInfo() {
        if (NewThreadFunction != nullptr) {
            delete NewThreadFunction;
        }
    }

    PyObjectHolder* NewThreadFunction;
    PyThreadState** CurrentThread;

    HMODULE Interpreter;
    PyGILState_EnsureFunc* PyGILState_Ensure;
    PyEval_SetTrace* SetTrace;
    PyThreadState_NewFunc* PyThreadState_New;
    PyThreadState_Swap* ThreadState_Swap;

    PythonVersion GetVersion() {
        if (Version == PythonVersion_Unknown) {
            Version = ::GetPythonVersion(Interpreter);
        }
        return Version;
    }

    PyObject_CallFunctionObjArgs* GetCall() {
        if (Call == nullptr) {
            Call = (PyObject_CallFunctionObjArgs*)GetProcAddress(Interpreter, "PyObject_CallFunctionObjArgs");
        }

        return Call;
    }

    bool EnsureSetTrace() {
        if (SetTrace == nullptr) {
            auto setTrace = (PyEval_SetTrace*)(void*)GetProcAddress(Interpreter, "PyEval_SetTrace");
            SetTrace = setTrace;
        }
        return SetTrace != nullptr;
    }

    bool EnsureThreadStateSwap() {
        if (ThreadState_Swap == nullptr) {
            auto swap = (PyThreadState_Swap*)(void*)GetProcAddress(Interpreter, "PyThreadState_Swap");
            ThreadState_Swap = swap;
        }
        return ThreadState_Swap != nullptr;
    }

    bool EnsureCurrentThread() {
        if (CurrentThread == nullptr) {
            auto curPythonThread = (PyThreadState**)(void*)GetProcAddress(
                Interpreter, "_PyThreadState_Current");
            CurrentThread = curPythonThread;
        }

        return CurrentThread != nullptr;
    }

private:
    PythonVersion Version;
    PyObject_CallFunctionObjArgs* Call;
    bool IsDebug;
};

DWORD _interpreterCount = 0;
InterpreterInfo* _interpreterInfo[MAX_INTERPRETERS];

void PatchIAT(PIMAGE_DOS_HEADER dosHeader, PVOID replacingFunc, LPSTR exportingDll, LPVOID newFunction) {
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) {
        return;
    }

    auto ntHeader = (IMAGE_NT_HEADERS*)(((BYTE*)dosHeader) + dosHeader->e_lfanew);
    if (ntHeader->Signature != IMAGE_NT_SIGNATURE) {
        return;
    }

    auto importAddr = ntHeader->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
    if (importAddr == 0) {
        return;
    }

    auto import = (PIMAGE_IMPORT_DESCRIPTOR)(importAddr + ((BYTE*)dosHeader));

    while (import->Name) {
        char* name = (char*)(import->Name + ((BYTE*)dosHeader));
        if (_stricmp(name, exportingDll) == 0) {
            auto thunkData = (PIMAGE_THUNK_DATA)((import->FirstThunk) + ((BYTE*)dosHeader));

            while (thunkData->u1.Function) {
                PVOID funcAddr = (char*)(thunkData->u1.Function);

                if (funcAddr == replacingFunc) {
                    DWORD flOldProtect;
                    if (VirtualProtect(&thunkData->u1, sizeof(SIZE_T), PAGE_READWRITE, &flOldProtect)) {
                        thunkData->u1.Function = (SIZE_T)newFunction;
                        VirtualProtect(&thunkData->u1, sizeof(SIZE_T), flOldProtect, &flOldProtect);
                    }
                }
                thunkData++;
            }
        }

        import++;
    }
}

typedef BOOL WINAPI EnumProcessModulesFunc(
    __in   HANDLE hProcess,
    __out  HMODULE *lphModule,
    __in   DWORD cb,
    __out  LPDWORD lpcbNeeded
    );

typedef __kernel_entry NTSTATUS NTAPI
    NtQueryInformationProcessFunc(
    IN HANDLE ProcessHandle,
    IN PROCESSINFOCLASS ProcessInformationClass,
    OUT PVOID ProcessInformation,
    IN ULONG ProcessInformationLength,
    OUT PULONG ReturnLength OPTIONAL
    );


// A helper version of EnumProcessModules.  On Win7 uses the real EnumProcessModules which
// lives in kernel32, and so is safe to use in DLLMain.  Pre-Win7 we use NtQueryInformationProcess
// (http://msdn.microsoft.com/en-us/library/windows/desktop/ms684280(v=vs.85).aspx) and walk the 
// LDR_DATA_TABLE_ENTRY data structures http://msdn.microsoft.com/en-us/library/windows/desktop/aa813708(v=vs.85).aspx
// which have changed in Windows 7, and may change more in the future, so we can't use them there.
__success(return) BOOL EnumProcessModulesHelper(
    __in   HANDLE hProcess,
    __out  HMODULE *lphModule,
    __in   DWORD cb,
    _Always_(__out) LPDWORD lpcbNeeded
    ) {
        if (lpcbNeeded == nullptr) {
            return FALSE;
        }
        *lpcbNeeded = 0;

        auto kernel32 = GetModuleHandle(L"kernel32.dll");
        if (kernel32 == nullptr) {
            return FALSE;
        }

        auto enumProc = (EnumProcessModulesFunc*)GetProcAddress(kernel32, "K32EnumProcessModules");
        if (enumProc == nullptr) {
            // Fallback to pre-Win7 method
            PROCESS_BASIC_INFORMATION basicInfo;
            auto ntdll = GetModuleHandle(L"ntdll.dll");
            if (ntdll == nullptr) {
                return FALSE;
            }

            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms684280(v=vs.85).aspx
            NtQueryInformationProcessFunc* queryInfo = (NtQueryInformationProcessFunc*)GetProcAddress(ntdll, "NtQueryInformationProcess");
            if (queryInfo == nullptr) {
                return FALSE;
            }

            auto result = queryInfo(
                GetCurrentProcess(),
                ProcessBasicInformation,
                &basicInfo,
                sizeof(PROCESS_BASIC_INFORMATION),
                NULL
                );

            if (FAILED(result)) {
                return FALSE;
            }

            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa813708(v=vs.85).aspx
            PEB* peb = basicInfo.PebBaseAddress;
            auto start = (LDR_DATA_TABLE_ENTRY*)(peb->Ldr->InMemoryOrderModuleList.Flink);

            auto cur = start;
            *lpcbNeeded = 0;

            do {
                if ((*lpcbNeeded + sizeof(SIZE_T)) <= cb) {
                    PVOID *curLink = (PVOID*)cur;
                    curLink -= 2;
                    LDR_DATA_TABLE_ENTRY* curTable = (LDR_DATA_TABLE_ENTRY*)curLink;
                    if (curTable->DllBase == nullptr) {
                        break;
                    }
                    lphModule[(*lpcbNeeded) / sizeof(SIZE_T)] = (HMODULE)curTable->DllBase;
                }

                (*lpcbNeeded) += sizeof(SIZE_T);
                cur = (LDR_DATA_TABLE_ENTRY*)((LIST_ENTRY*)cur)->Flink;
            } while (cur != start && cur != 0);

            return *lpcbNeeded <= cb;
        }

        return enumProc(hProcess, lphModule, cb, lpcbNeeded);
}

// This function will work with Win7 and later versions of the OS and is safe to call under
// the loader lock (all APIs used are in kernel32).
BOOL PatchFunction(LPSTR exportingDll, PVOID replacingFunc, LPVOID newFunction) {
    HANDLE hProcess = GetCurrentProcess();
    DWORD modSize = sizeof(HMODULE) * 1024;
    HMODULE* hMods = (HMODULE*)_malloca(modSize);
    DWORD modsNeeded = 0;
    if (hMods == nullptr) {
        modsNeeded = 0;
        return FALSE;
    }

    while (!EnumProcessModulesHelper(hProcess, hMods, modSize, &modsNeeded)) {
        // try again w/ more space...
        _freea(hMods);
        hMods = (HMODULE*)_malloca(modsNeeded);
        if (hMods == nullptr) {
            modsNeeded = 0;
            break;
        }
        modSize = modsNeeded;
    }

    for (DWORD tmp = 0; tmp < modsNeeded / sizeof(HMODULE); tmp++) {
        PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)hMods[tmp];

        PatchIAT(dosHeader, replacingFunc, exportingDll, newFunction);
    }

    return TRUE;
}

wstring GetCurrentModuleFilename() {
    HMODULE hModule = NULL;
    if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCTSTR)GetCurrentModuleFilename, &hModule) != 0) {
        wchar_t filename[MAX_PATH];
        GetModuleFileName(hModule, filename, MAX_PATH);
        return filename;
    }
    return wstring();
}

struct AttachInfo {
    PyEval_Lock* InitThreads;
    HANDLE Event;
};

HANDLE g_initedEvent;
int AttachCallback(void *initThreads) {
    // initialize us for threading, this will acquire the GIL if not already created, and is a nop if the GIL is created.
    // This leaves us in the proper state when we return back to the runtime whether the GIL was created or not before
    // we were called.
    ((PyEval_Lock*)initThreads)();
    SetEvent(g_initedEvent);
    return 0;
}

char* ReadCodeFromFile(wchar_t* filePath) {
    ifstream filestr;
    filestr.open(filePath, ios::binary);
    if (filestr.fail()) {
        return nullptr;
    }

    // get length of file:
    filestr.seekg(0, ios::end);
    auto length = filestr.tellg();
    filestr.seekg(0, ios::beg);

    int len = (int)length;
    char* buffer = new char[len + 1];
    filestr.read(buffer, len);
    buffer[len] = 0;

    // remove carriage returns, copy zero byte
    for (int read = 0, write = 0; read <= len; read++) {
        if (buffer[read] == '\r') {
            continue;
        } else if (write != read) {
            buffer[write] = buffer[read];
        }
        write++;
    }

    return buffer;
}

// create a custom heap for our unordered map.  This is necessary because if we suspend a thread while in a heap function
// then we could deadlock here.  We need to be VERY careful about what we do while the threads are suspended.
static HANDLE g_heap = 0;

template<typename T>
class PrivateHeapAllocator {
public:
    typedef size_t    size_type;
    typedef ptrdiff_t difference_type;
    typedef T*        pointer;
    typedef const T*  const_pointer;
    typedef T&        reference;
    typedef const T&  const_reference;
    typedef T         value_type;

    template<class U>
    struct rebind {
        typedef PrivateHeapAllocator<U> other;
    };

    explicit PrivateHeapAllocator() {}

    PrivateHeapAllocator(PrivateHeapAllocator const&) {}

    ~PrivateHeapAllocator() {}

    template<typename U>
    PrivateHeapAllocator(PrivateHeapAllocator<U> const&) {}

    pointer allocate(size_type size, allocator<void>::const_pointer hint = 0) {
        if (g_heap == nullptr) {
            g_heap = HeapCreate(0, 0, 0);
        }
        auto mem = HeapAlloc(g_heap, 0, size * sizeof(T));
        return static_cast<pointer>(mem);
    }

    void deallocate(pointer p, size_type n) {
        HeapFree(g_heap, 0, p);
    }

    size_type max_size() const {
        return (std::numeric_limits<size_type>::max)() / sizeof(T);
    }

    void construct(pointer p, const T& t) {
        new(p) T(t);
    }

    void destroy(pointer p) {
        p->~T();
    }
};

typedef unordered_map<DWORD, HANDLE, std::hash<DWORD>, std::equal_to<DWORD>, PrivateHeapAllocator<pair<DWORD, HANDLE>>> ThreadMap;

void ResumeThreads(ThreadMap &suspendedThreads) {
    for (auto start = suspendedThreads.begin();  start != suspendedThreads.end(); start++) {
        ResumeThread((*start).second);
        CloseHandle((*start).second);
    }
    suspendedThreads.clear();
}

// Suspends all threads ensuring that they are not currently in a call to Py_AddPendingCall.
void SuspendThreads(ThreadMap &suspendedThreads, Py_AddPendingCall* addPendingCall, PyEval_ThreadsInitialized* threadsInited) {
    DWORD curThreadId = GetCurrentThreadId();
    DWORD curProcess = GetCurrentProcessId();
    // suspend all the threads in the process so we can do things safely...
    bool suspended;

    do {
        suspended = false;
        HANDLE h = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (h != INVALID_HANDLE_VALUE) {

            THREADENTRY32 te;
            te.dwSize = sizeof(te);
            if (Thread32First(h, &te)) {
                do {
                    if (te.dwSize >= FIELD_OFFSET(THREADENTRY32, th32OwnerProcessID) + sizeof(te.th32OwnerProcessID) && te.th32OwnerProcessID == curProcess) {


                        if (te.th32ThreadID != curThreadId && suspendedThreads.find(te.th32ThreadID) == suspendedThreads.end()) {
                            auto hThread = OpenThread(THREAD_ALL_ACCESS, FALSE, te.th32ThreadID);
                            if (hThread != nullptr) {
                                SuspendThread(hThread);

                                bool addingPendingCall = false;

                                CONTEXT context;
                                memset(&context, 0x00, sizeof(CONTEXT));
                                context.ContextFlags = CONTEXT_ALL;
                                GetThreadContext(hThread, &context);

#if defined(_X86_)
                                if(context.Eip >= *((DWORD*)addPendingCall) && context.Eip <= (*((DWORD*)addPendingCall)) + 0x100) {
                                    addingPendingCall = true;
                                }
#elif defined(_AMD64_)
                                if (context.Rip >= *((DWORD64*)addPendingCall) && context.Rip <= *((DWORD64*)addPendingCall + 0x100)) {
                                    addingPendingCall = true;
                                }
#endif

                                if (addingPendingCall) {
                                    // we appear to be adding a pending call via this thread - wait for this to finish so we can add our own pending call...
                                    ResumeThread(hThread);
                                    SwitchToThread();   // yield to the resumed thread if it's on our CPU...
                                    CloseHandle(hThread);
                                } else {
                                    suspendedThreads[te.th32ThreadID] = hThread;
                                }
                                suspended = true;
                            }
                        }
                    }

                    te.dwSize = sizeof(te);
                } while (Thread32Next(h, &te) && !threadsInited());
            }
            CloseHandle(h);
        }
    } while (suspended && !threadsInited());
}

PyObject* GetPyObjectPointerNoDebugInfo(bool isDebug, PyObject* object) {
    if (object != nullptr && isDebug) {
        // debug builds have 2 extra pointers at the front that we don't care about
        return (PyObject*)((size_t*)object + 2);
    }
    return object;
}

void DecRef(PyObject* object, bool isDebug) {
    auto noDebug = GetPyObjectPointerNoDebugInfo(isDebug, object);

    if (noDebug != nullptr && --noDebug->ob_refcnt == 0) {
        ((PyTypeObject*)GetPyObjectPointerNoDebugInfo(isDebug, noDebug->ob_type))->tp_dealloc(object);
    }
}

void IncRef(PyObject* object) {
    object->ob_refcnt++;
}

// Structure for our shared memory communication, aligned to be identical on 64-bit and 32-bit
struct MemoryBuffer {
    int32_t PortNumber;                                 // offset 0-4
    unsigned : 4;                                       // offset 4-8 (padding)
    __declspec(align(8)) HANDLE AttachStartingEvent;    // offset 8-16
    __declspec(align(8)) HANDLE AttachDoneEvent;        // offset 16-24
    __declspec(align(8)) int32_t ErrorNumber;           // offset 24-28
    int32_t VersionNumber;                              // offset 28-32
    char DebugId[64];                                   // null terminated string
    char DebugOptions[1];                               // null terminated string (VLA)
};

class ConnectionInfo {
public:
    HANDLE FileMapping;
    MemoryBuffer *Buffer;
    bool Succeeded;

    ConnectionInfo() : Succeeded(false) {
    }

    ConnectionInfo(MemoryBuffer *memoryBuffer, HANDLE fileMapping) :
        Succeeded(true), Buffer(memoryBuffer), FileMapping(fileMapping) {
    }

    // Reports an error while we're initially setting up the attach.  These errors can all be 
    // reported quickly and are reported across the shared memory buffer.
    void ReportError(int errorNum) {
        Buffer->ErrorNumber = errorNum;
    }

    // Reports an error after we've started the attach via our socket.  These are errors which
    // may take a while to get to because the GIL is held and we cannot continue with the attach.
    // Because the UI for attach is gone by the time we report this error it gets reported 
    // in the debug output pane.
    // These errors should also be extremely rare - for example a broken PTVS install, or a broken
    // Python interpreter.  We'd much rather give the user a message box error earlier than the
    // error logged in the output window which they might miss.
    void ReportErrorAfterAttachDone(DWORD errorNum) {
        WSADATA data;
        if (!WSAStartup(MAKEWORD(2, 0), &data)) {
            auto sock = socket(PF_INET, SOCK_STREAM, IPPROTO_TCP);
            if (sock != INVALID_SOCKET) {
                sockaddr_in serveraddr = {0};

                serveraddr.sin_family = AF_INET;
                serveraddr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
                serveraddr.sin_port = htons(Buffer->PortNumber);

                // connect to our DebugConnectionListener and report the error.
                if (connect(sock, (sockaddr*)&serveraddr, sizeof(sockaddr_in)) == 0) {
                    // send our debug ID as an ASCII string.  
                    send(sock, "A", 1, 0);
                    int len = (int)strlen(Buffer->DebugId);
                    unsigned long long lenBE64 = _byteswap_uint64(len);
                    send(sock, (const char*)&lenBE64, sizeof(lenBE64), 0);
                    send(sock, Buffer->DebugId, (int)len, 0);

                    // send our error number
                    unsigned long long errorNumBE64 = _byteswap_uint64(errorNum);
                    send(sock, (const char*)&errorNumBE64, sizeof(errorNumBE64), 0);
                }
            }
        }
    }

    void SetVersion(PythonVersion version) {
        Buffer->VersionNumber = version;
    }

    ~ConnectionInfo() {
        if (Succeeded) {
            CloseHandle(Buffer->AttachStartingEvent);

            auto attachDoneEvent = Buffer->AttachDoneEvent;
            UnmapViewOfFile(Buffer);
            CloseHandle(FileMapping);

            // we may set this multiple times, but that doesn't matter...
            SetEvent(attachDoneEvent);
            CloseHandle(attachDoneEvent);
        }
    }
};

ConnectionInfo GetConnectionInfo() {
    HANDLE hMapFile;
    char* pBuf;

    wchar_t fullMappingName[1024];
    _snwprintf_s(fullMappingName, _countof(fullMappingName), L"PythonDebuggerMemory%d", GetCurrentProcessId());

    hMapFile = OpenFileMapping(
        FILE_MAP_ALL_ACCESS,   // read/write access
        FALSE,                 // do not inherit the name
        fullMappingName);            // name of mapping object 

    if (hMapFile == NULL) {
        return ConnectionInfo();
    }

    pBuf = (char*)MapViewOfFile(hMapFile, // handle to map object
        FILE_MAP_ALL_ACCESS,  // read/write permission
        0,
        0,
        1024);

    if (pBuf == NULL) {
        CloseHandle(hMapFile);
        return ConnectionInfo();
    }

    return ConnectionInfo((MemoryBuffer*)pBuf, hMapFile);
}

// Error messages - must be kept in sync with PythonAttach.cs
enum ConnErrorMessages {
    ConnError_None,
    ConnError_InterpreterNotInitialized,
    ConnError_UnknownVersion,
    ConnError_LoadDebuggerFailed,
    ConnError_LoadDebuggerBadDebugger,
    ConnError_PythonNotFound,
    ConnError_TimeOut,
    ConnError_CannotOpenProcess,
    ConnError_OutOfMemory,
    ConnError_CannotInjectThread,
    ConnError_SysNotFound,
    ConnError_SysSetTraceNotFound,
    ConnError_SysGetTraceNotFound,
    ConnError_PyDebugAttachNotFound,
    ConnError_RemoteNetworkError,
    ConnError_RemoteSslError,
    ConnError_RemoteUnsupportedServer,
    ConnError_RemoteSecretMismatch,
    ConnError_RemoteAttachRejected,
    ConnError_RemoteInvalidUri,
    ConnError_RemoteUnsupportedTransport
};

// Ensures handles are closed when they go out of scope
class HandleHolder {
    HANDLE _handle;
public:
    HandleHolder(HANDLE handle) : _handle(handle) {
    }

    ~HandleHolder() {
        CloseHandle(_handle);
    }
};

long GetPythonThreadId(PythonVersion version, PyThreadState* curThread) {
    long threadId = 0;
    if (PyThreadState_25_27::IsFor(version)) {
        threadId = ((PyThreadState_25_27*)curThread)->thread_id;
    } else if (PyThreadState_30_33::IsFor(version)) {
        threadId = ((PyThreadState_30_33*)curThread)->thread_id;
    } else if (PyThreadState_34_35::IsFor(version)) {
        threadId = ((PyThreadState_34_35*)curThread)->thread_id;
    }
    return threadId;
}

// holder to ensure we release the GIL even in error conditions
class GilHolder {
    PyGILState_STATE _gilState;
    PyGILState_Release* _release;
public:
    GilHolder(PyGILState_Ensure* acquire, PyGILState_Release* release) {
        _gilState = acquire();
        _release = release;
    }

    ~GilHolder() {
        _release(_gilState);
    }
};

bool LoadAndEvaluateCode(
    wchar_t* filePath, const char* fileName, ConnectionInfo& connInfo, bool isDebug, PyObject* globalsDict,
    Py_CompileString* pyCompileString, PyDict_SetItemString* dictSetItem,
    PyEval_EvalCode* pyEvalCode, PyString_FromString* strFromString, PyEval_GetBuiltins* getBuiltins,
    PyErr_Print pyErrPrint
 ) {
    auto debuggerCode = ReadCodeFromFile(filePath);
    if (debuggerCode == nullptr) {
        connInfo.ReportErrorAfterAttachDone(ConnError_LoadDebuggerFailed);
        return false;
    }

    auto code = PyObjectHolder(isDebug, pyCompileString(debuggerCode, fileName, 257 /*Py_file_input*/));
    delete[] debuggerCode;

    if (*code == nullptr) {
        connInfo.ReportErrorAfterAttachDone(ConnError_LoadDebuggerFailed);
        return false;
    }

    dictSetItem(globalsDict, "__builtins__", getBuiltins());
    auto size = WideCharToMultiByte(CP_UTF8, 0, filePath, (DWORD)wcslen(filePath), NULL, 0, NULL, NULL);
    char* filenameBuffer = new char[size];
    if (WideCharToMultiByte(CP_UTF8, 0, filePath, (DWORD)wcslen(filePath), filenameBuffer, size, NULL, NULL) != 0) {
        filenameBuffer[size] = 0;
        dictSetItem(globalsDict, "__file__", strFromString(filenameBuffer));
    }

    auto evalResult = PyObjectHolder(isDebug, pyEvalCode(code.ToPython(), globalsDict, globalsDict));
#if !NDEBUG
    if (*evalResult == nullptr) {
        pyErrPrint();
    }
#endif

    return true;
}

bool DoAttach(HMODULE module, ConnectionInfo& connInfo, bool isDebug) {
    // Python DLL?
    auto isInit = (Py_IsInitialized*)GetProcAddress(module, "Py_IsInitialized");

    if (isInit != nullptr && isInit()) {
        DWORD interpreterId = INFINITE;
        for (DWORD curInterp = 0; curInterp < MAX_INTERPRETERS; curInterp++) {
            if (_interpreterInfo[curInterp] != nullptr &&
                _interpreterInfo[curInterp]->Interpreter == module) {
                    interpreterId = curInterp;
                    break;
            }
        }

        if (interpreterId == INFINITE) {
            connInfo.ReportError(ConnError_UnknownVersion);
            return FALSE;
        }

        auto version = GetPythonVersion(module);

        // found initialized Python runtime, gather and check the APIs we need for a successful attach...
        auto addPendingCall = (Py_AddPendingCall*)GetProcAddress(module, "Py_AddPendingCall");
        auto curPythonThread = (PyThreadState**)(void*)GetProcAddress(module, "_PyThreadState_Current");
        auto interpHead = (PyInterpreterState_Head*)GetProcAddress(module, "PyInterpreterState_Head");
        auto gilEnsure = (PyGILState_Ensure*)GetProcAddress(module, "PyGILState_Ensure");
        auto gilRelease = (PyGILState_Release*)GetProcAddress(module, "PyGILState_Release");
        auto threadHead = (PyInterpreterState_ThreadHead*)GetProcAddress(module, "PyInterpreterState_ThreadHead");
        auto initThreads = (PyEval_Lock*)GetProcAddress(module, "PyEval_InitThreads");
        auto acquireLock = (PyEval_Lock*)GetProcAddress(module, "PyEval_AcquireLock");
        auto releaseLock = (PyEval_Lock*)GetProcAddress(module, "PyEval_ReleaseLock");
        auto threadsInited = (PyEval_ThreadsInitialized*)GetProcAddress(module, "PyEval_ThreadsInitialized");
        auto threadNext = (PyThreadState_Next*)GetProcAddress(module, "PyThreadState_Next");
        auto threadSwap = (PyThreadState_Swap*)GetProcAddress(module, "PyThreadState_Swap");
        auto pyDictNew = (PyDict_New*)GetProcAddress(module, "PyDict_New");
        auto pyModuleNew = (PyModule_New*)GetProcAddress(module, "PyModule_New");
        auto pyModuleGetDict = (PyModule_GetDict*)GetProcAddress(module, "PyModule_GetDict");
        auto pyCompileString = (Py_CompileString*)GetProcAddress(module, "Py_CompileString");
        auto pyEvalCode = (PyEval_EvalCode*)GetProcAddress(module, "PyEval_EvalCode");
        auto getDictItem = (PyDict_GetItemString*)GetProcAddress(module, "PyDict_GetItemString");
        auto call = (PyObject_CallFunctionObjArgs*)GetProcAddress(module, "PyObject_CallFunctionObjArgs");
        auto getBuiltins = (PyEval_GetBuiltins*)GetProcAddress(module, "PyEval_GetBuiltins");
        auto dictSetItem = (PyDict_SetItemString*)GetProcAddress(module, "PyDict_SetItemString");
        PyInt_FromLong* intFromLong;
        PyString_FromString* strFromString;
        PyInt_FromSize_t* intFromSizeT;
        if (version >= PythonVersion_30) {
            intFromLong = (PyInt_FromLong*)GetProcAddress(module, "PyLong_FromLong");
            intFromSizeT = (PyInt_FromSize_t*)GetProcAddress(module, "PyLong_FromSize_t");
            if (version >= PythonVersion_33) {
                strFromString = (PyString_FromString*)GetProcAddress(module, "PyUnicode_FromString");
            } else {
                strFromString = (PyString_FromString*)GetProcAddress(module, "PyUnicodeUCS2_FromString");
            }
        } else {
            intFromLong = (PyInt_FromLong*)GetProcAddress(module, "PyInt_FromLong");
            strFromString = (PyString_FromString*)GetProcAddress(module, "PyString_FromString");
            intFromSizeT = (PyInt_FromSize_t*)GetProcAddress(module, "PyInt_FromSize_t");
        }
        auto intervalCheck = (int*)GetProcAddress(module, "_Py_CheckInterval");
        auto errOccurred = (PyErr_Occurred*)GetProcAddress(module, "PyErr_Occurred");
        auto pyErrFetch = (PyErr_Fetch*)GetProcAddress(module, "PyErr_Fetch");
        auto pyErrRestore = (PyErr_Restore*)GetProcAddress(module, "PyErr_Restore");
        auto pyErrPrint = (PyErr_Print*)GetProcAddress(module, "PyErr_Print");
        auto pyImportMod = (PyImport_ImportModule*) GetProcAddress(module, "PyImport_ImportModule");
        auto pyGetAttr = (PyObject_GetAttrString*)GetProcAddress(module, "PyObject_GetAttrString");
        auto pySetAttr = (PyObject_SetAttrString*)GetProcAddress(module, "PyObject_SetAttrString");
        auto pyNone = (PyObject*)GetProcAddress(module, "_Py_NoneStruct");
        auto getSwitchInterval = (_PyEval_GetSwitchInterval*)GetProcAddress(module, "_PyEval_GetSwitchInterval");
        auto setSwitchInterval = (_PyEval_SetSwitchInterval*)GetProcAddress(module, "_PyEval_SetSwitchInterval");
        auto boolFromLong = (PyBool_FromLong*)GetProcAddress(module, "PyBool_FromLong");
        auto getThreadTls = (PyThread_get_key_value*)GetProcAddress(module, "PyThread_get_key_value");
        auto setThreadTls = (PyThread_set_key_value*)GetProcAddress(module, "PyThread_set_key_value");
        auto delThreadTls = (PyThread_delete_key_value*)GetProcAddress(module, "PyThread_delete_key_value");
        auto pyGilStateEnsure = (PyGILState_EnsureFunc*)GetProcAddress(module, "PyGILState_Ensure");
        auto pyGilStateRelease = (PyGILState_ReleaseFunc*)GetProcAddress(module, "PyGILState_Release");
        auto PyCFrame_Type = (PyTypeObject*)GetProcAddress(module, "PyCFrame_Type");

        if (addPendingCall == nullptr || curPythonThread == nullptr || interpHead == nullptr || gilEnsure == nullptr || gilRelease == nullptr || threadHead == nullptr ||
            initThreads == nullptr || releaseLock == nullptr || threadsInited == nullptr || threadNext == nullptr || threadSwap == nullptr ||
            pyDictNew == nullptr || pyCompileString == nullptr || pyEvalCode == nullptr || getDictItem == nullptr || call == nullptr ||
            getBuiltins == nullptr || dictSetItem == nullptr || intFromLong == nullptr || pyErrRestore == nullptr || pyErrFetch == nullptr ||
            errOccurred == nullptr || pyImportMod == nullptr || pyGetAttr == nullptr || pyNone == nullptr || pySetAttr == nullptr || boolFromLong == nullptr ||
            getThreadTls == nullptr || setThreadTls == nullptr || delThreadTls == nullptr || releaseLock == nullptr ||
            pyGilStateEnsure == nullptr || pyGilStateRelease == nullptr) {
                // we're missing some APIs, we cannot attach.
                connInfo.ReportError(ConnError_PythonNotFound);
                return false;
        }

        auto head = interpHead();
        if (head == nullptr) {
            // this interpreter is loaded but not initialized.
            connInfo.ReportError(ConnError_InterpreterNotInitialized);
            return false;
        }

        bool threadSafeAddPendingCall = false;

        // check that we're a supported version
        if (version == PythonVersion_Unknown) {
            connInfo.ReportError(ConnError_UnknownVersion);
            return false;
        } else if (version >= PythonVersion_27 && version != PythonVersion_30) {
            threadSafeAddPendingCall = true;
        }
        connInfo.SetVersion(version);

        // we know everything we need for VS to continue the attach.
        connInfo.ReportError(ConnError_None);
        SetEvent(connInfo.Buffer->AttachStartingEvent);

        if (!threadsInited()) {
            int saveIntervalCheck;
            unsigned long saveLongIntervalCheck;
            if (intervalCheck != nullptr) {
                // not available on 3.2
                saveIntervalCheck = *intervalCheck;
                *intervalCheck = -1;    // lower the interval check so pending calls are processed faster
            } else if (getSwitchInterval != nullptr && setSwitchInterval != nullptr) {
                saveLongIntervalCheck = getSwitchInterval();
                setSwitchInterval(0);
            }

            // 
            // Multiple thread support has not been initialized in the interpreter.   We need multi threading support
            // to block any actively running threads and setup the debugger attach state.
            //
            // We need to initialize multiple threading support but we need to do so safely.  One option is to call
            // Py_AddPendingCall and have our callback then initialize multi threading.  This is completely safe on 2.7
            // and up.  Unfortunately that doesn't work if we're not actively running code on the main thread (blocked on a lock 
            // or reading input).  It's also not thread safe pre-2.7 so we need to make sure it's safe to call on down-level 
            // interpreters.
            //
            // Another option is to make sure no code is running - if there is no active thread then we can safely call
            // PyEval_InitThreads and we're in business.  But to know this is safe we need to first suspend all the other
            // threads in the process and then inspect if any code is running.
            //
            // Finally if code is running after we've suspended the threads then we can go ahead and do Py_AddPendingCall
            // on down-level interpreters as long as we're sure no one else is making a call to Py_AddPendingCall at the same
            // time.
            //
            // Therefore our strategy becomes: Make the Py_AddPendingCall on interpreters where it's thread safe.  Then suspend
            // all threads - if a threads IP is in Py_AddPendingCall resume and try again.  Once we've got all of the threads
            // stopped and not in Py_AddPendingCall (which calls no functions its self, you can see this and it's size in the
            // debugger) then see if we have a current thread.   If not go ahead and initialize multiple threading (it's now safe,
            // no Python code is running).  Otherwise add the pending call and repeat.  If at any point during this process 
            // threading becomes initialized (due to our pending call or the Python code creating a new thread)  then we're done 
            // and we just resume all of the presently suspended threads.

            ThreadMap suspendedThreads;

            g_initedEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
            HandleHolder holder(g_initedEvent);

            bool addedPendingCall = false;
            if (addPendingCall != nullptr && threadSafeAddPendingCall) {
                // we're on a thread safe Python version, go ahead and pend our call to initialize threading.
                addPendingCall(&AttachCallback, initThreads);
                addedPendingCall = true;
            }

#define TICKS_DIFF(prev, cur) ((cur) >= (prev)) ? ((cur)-(prev)) : ((0xFFFFFFFF-(prev))+(cur)) 
            const DWORD ticksPerSecond = 1000;

            DWORD startTickCount = GetTickCount();
            do {
                SuspendThreads(suspendedThreads, addPendingCall, threadsInited);

                if (!threadsInited()) {
                    if (*curPythonThread == nullptr) {
                        // no threads are currently running, it is safe to initialize multi threading.
                        PyGILState_STATE gilState;
                        if (version >= PythonVersion_34) {
                            // in 3.4 due to http://bugs.python.org/issue20891,
                            // we need to create our thread state manually
                            // before we can call PyGILState_Ensure() before we
                            // can call PyEval_InitThreads().
                            
                            // Don't require this function unless we need it.
                            auto threadNew = (PyThreadState_NewFunc*)GetProcAddress(module, "PyThreadState_New");
                            if (threadNew != nullptr) {
                                threadNew(head);
                            }
                        }
                        
                        if (version >= PythonVersion_32) {
                            // in 3.2 due to the new GIL and later we can't call Py_InitThreads 
                            // without a thread being initialized.  
                            // So we use PyGilState_Ensure here to first
                            // initialize the current thread, and then we use
                            // Py_InitThreads to bring up multi-threading.
                            // Some context here: http://bugs.python.org/issue11329
                            // http://pytools.codeplex.com/workitem/834
                            gilState = pyGilStateEnsure();
                        }
                        initThreads();

                        if (version >= PythonVersion_32) {
                            // we will release the GIL here
                            pyGilStateRelease(gilState);
                        } else {
                            releaseLock();
                        }
                    } else if (!addedPendingCall) {
                        // someone holds the GIL but no one is actively adding any pending calls.  We can pend our call
                        // and initialize threads.
                        addPendingCall(&AttachCallback, initThreads);
                        addedPendingCall = true;
                    }
                }
                ResumeThreads(suspendedThreads);
            } while (!threadsInited() &&
                (TICKS_DIFF(startTickCount, GetTickCount())) < (ticksPerSecond * 20) &&
                !addedPendingCall);

            if (!threadsInited()) {
                if (addedPendingCall) {
                    // we've added our call to initialize multi-threading, we can now wait
                    // until Python code actually starts running.
                    SetEvent(connInfo.Buffer->AttachDoneEvent);
                    ::WaitForSingleObject(g_initedEvent, INFINITE);
                } else {
                    connInfo.ReportError(ConnError_TimeOut);
                    return false;
                }
            } else {
                SetEvent(connInfo.Buffer->AttachDoneEvent);
            }

            if (intervalCheck != nullptr) {
                *intervalCheck = saveIntervalCheck;
            } else if (setSwitchInterval != nullptr) {
                setSwitchInterval(saveLongIntervalCheck);
            }
        } else {
            SetEvent(connInfo.Buffer->AttachDoneEvent);
        }

        if (g_heap != nullptr) {
            HeapDestroy(g_heap);
            g_heap = nullptr;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // go ahead and bring in the debugger module and initialize all threads in the process...
        GilHolder gilLock(gilEnsure, gilRelease);   // acquire and hold the GIL until done...

        auto pyTrue = boolFromLong(1);
        auto pyFalse = boolFromLong(0);

        auto filename = GetCurrentModuleFilename();
        if (filename.length() == 0) {
            return nullptr;
        }

        wchar_t drive[_MAX_DRIVE], dir[_MAX_DIR], file[_MAX_FNAME], ext[_MAX_EXT];
        _wsplitpath_s(filename.c_str(), drive, _MAX_DRIVE, dir, _MAX_DIR, file, _MAX_FNAME, ext, _MAX_EXT);

        wchar_t debuggerModuleFilePath[MAX_PATH];
        wchar_t replModuleFilePath[MAX_PATH];
        wchar_t utilModuleFilePath[MAX_PATH];
        _wmakepath_s(debuggerModuleFilePath, drive, dir, L"visualstudio_py_debugger", L".py");
        _wmakepath_s(replModuleFilePath, drive, dir, L"visualstudio_py_repl", L".py");
        _wmakepath_s(utilModuleFilePath, drive, dir, L"visualstudio_py_util", L".py");

        // Load modules in dependency order. Dependency graph:
        //
        // visualstudio_py_debugger --> visualstudio_py_repl --> visualstudio_py_util
        //                        \______________________________^

        auto utilModule = PyObjectHolder(isDebug, pyModuleNew("visualstudio_py_util"));
        auto utilModuleDict = pyModuleGetDict(utilModule.ToPython());
        LoadAndEvaluateCode(utilModuleFilePath, "visualstudio_py_util.py", connInfo, isDebug, utilModuleDict,
            pyCompileString, dictSetItem, pyEvalCode, strFromString, getBuiltins, pyErrPrint);

        auto replModule = PyObjectHolder(isDebug, pyModuleNew("visualstudio_py_repl"));
        auto replModuleDict = pyModuleGetDict(replModule.ToPython());
        dictSetItem(replModuleDict, "visualstudio_py_util", utilModule.ToPython());
        LoadAndEvaluateCode(replModuleFilePath, "visualstudio_py_repl.py", connInfo, isDebug, replModuleDict,
            pyCompileString, dictSetItem, pyEvalCode, strFromString, getBuiltins, pyErrPrint);

        auto globalsDict = PyObjectHolder(isDebug, pyDictNew());
        dictSetItem(globalsDict.ToPython(), "visualstudio_py_util", utilModule.ToPython());
        dictSetItem(globalsDict.ToPython(), "visualstudio_py_repl", replModule.ToPython());
        LoadAndEvaluateCode(debuggerModuleFilePath, "visualstudio_py_debugger.py", connInfo, isDebug, globalsDict.ToPython(),
            pyCompileString, dictSetItem, pyEvalCode, strFromString, getBuiltins, pyErrPrint);

        // now initialize debugger process wide state
        auto attach_process = PyObjectHolder(isDebug, getDictItem(globalsDict.ToPython(), "attach_process"), true);
        auto new_thread = PyObjectHolder(isDebug, getDictItem(globalsDict.ToPython(), "new_thread"), true);

        _interpreterInfo[interpreterId]->NewThreadFunction = new PyObjectHolder(
            isDebug,
            getDictItem(globalsDict.ToPython(),
            "new_external_thread"),
            true);

        if (*attach_process == nullptr || *new_thread == nullptr) {
            connInfo.ReportErrorAfterAttachDone(ConnError_LoadDebuggerBadDebugger);
            return false;
        }

        auto pyPortNum = PyObjectHolder(isDebug, intFromLong(connInfo.Buffer->PortNumber));
        auto debugId = PyObjectHolder(isDebug, strFromString(connInfo.Buffer->DebugId));
        auto debugOptions = PyObjectHolder(isDebug, strFromString(connInfo.Buffer->DebugOptions));
        DecRef(call(attach_process.ToPython(), pyPortNum.ToPython(), debugId.ToPython(), debugOptions.ToPython(), pyTrue, pyFalse, NULL), isDebug);

        auto sysMod = PyObjectHolder(isDebug, pyImportMod("sys"));
        if (*sysMod == nullptr) {
            connInfo.ReportErrorAfterAttachDone(ConnError_SysNotFound);
            return false;
        }

        auto settrace = PyObjectHolder(isDebug, pyGetAttr(sysMod.ToPython(), "settrace"));
        if (*settrace == nullptr) {
            connInfo.ReportErrorAfterAttachDone(ConnError_SysSetTraceNotFound);
            return false;
        }

        auto gettrace = PyObjectHolder(isDebug, pyGetAttr(sysMod.ToPython(), "gettrace"));

        // we need to walk the thread list each time after we've initialized a thread so that we are always
        // dealing w/ a valid thread list (threads can exit when we run code and therefore the current thread
        // could be corrupt).  We also don't care about newly created threads as our start_new_thread wrapper
        // will handle those.  So we collect the initial set of threads first here so that we don't keep iterating
        // if the program is spawning large numbers of threads.
        unordered_set<PyThreadState*> initialThreads;
        for (auto curThread = threadHead(head); curThread != nullptr; curThread = threadNext(curThread)) {
            initialThreads.insert(curThread);
        }

        unordered_set<PyThreadState*> seenThreads;
        {
            // find what index is holding onto the thread state...
            auto curPyThread = *curPythonThread;
            int threadStateIndex = -1;
            for (int i = 0; i < 100000; i++) {
                void* value = getThreadTls(i);
                if (value == curPyThread) {
                    threadStateIndex = i;
                    break;
                }
            }

            bool foundThread;
            int processedThreads = 0;
            do {
                foundThread = false;
                for (auto curThread = threadHead(head); curThread != nullptr; curThread = threadNext(curThread)) {
                    if (initialThreads.find(curThread) == initialThreads.end() ||
                        seenThreads.insert(curThread).second == false) {
                            continue;
                    }
                    foundThread = true;
                    processedThreads++;

                    long threadId = GetPythonThreadId(version, curThread);
                    // skip this thread - it doesn't really have any Python code on it...
                    if (threadId != GetCurrentThreadId()) {
                        // create new debugger Thread object on our injected thread
                        auto pyThreadId = PyObjectHolder(isDebug, intFromLong(threadId));
                        PyFrameObject* frame;
                        // update all of the frames so they have our trace func
                        if (PyThreadState_25_27::IsFor(version)) {
                            frame = ((PyThreadState_25_27*)curThread)->frame;
                        } else if (PyThreadState_30_33::IsFor(version)) {
                            frame = ((PyThreadState_30_33*)curThread)->frame;
                        } else if (PyThreadState_34_35::IsFor(version)) {
                            frame = ((PyThreadState_34_35*)curThread)->frame;
                        }

                        auto threadObj = PyObjectHolder(isDebug, call(new_thread.ToPython(), pyThreadId.ToPython(), pyTrue, frame, NULL));
                        if (threadObj.ToPython() == pyNone || *threadObj == nullptr) {
                            break;
                        }

                        // switch to our new thread so we can call sys.settrace on it...
                        // all of the work here needs to be minimal - in particular we shouldn't
                        // ever evaluate user defined code as we could end up switching to this
                        // thread on the main thread and corrupting state.
                        auto prevThreadState = getThreadTls(threadStateIndex);
                        delThreadTls(threadStateIndex);
                        setThreadTls(threadStateIndex, curThread);
                        auto prevThread = threadSwap(curThread);

                        // save and restore the error in case something funky happens...
                        auto errOccured = errOccurred();
                        PyObject *type, *value, *traceback;
                        if (errOccured) {
                            pyErrFetch(&type, &value, &traceback);
                        }

                        auto traceFunc = PyObjectHolder(isDebug, pyGetAttr(threadObj.ToPython(), "trace_func"));

                        if (*gettrace == NULL) {
                            DecRef(call(settrace.ToPython(), traceFunc.ToPython(), NULL), isDebug);
                        } else {
                            auto existingTraceFunc = PyObjectHolder(isDebug, call(gettrace.ToPython(), NULL));

                            DecRef(call(settrace.ToPython(), traceFunc.ToPython(), NULL), isDebug);

                            if (existingTraceFunc.ToPython() != pyNone) {
                                pySetAttr(threadObj.ToPython(), "prev_trace_func", existingTraceFunc.ToPython());
                            }
                        }

                        if (errOccured) {
                            pyErrRestore(type, value, traceback);
                        }

                        // update all of the frames so they have our trace func
                        auto curFrame = (PyFrameObject*)GetPyObjectPointerNoDebugInfo(isDebug, frame);
                        while (curFrame != nullptr) {
                            // Special case for CFrame objects
                            // Stackless CFrame does not have a trace function
                            // This will just prevent a crash on attach.
                            if (((PyObject*)curFrame)->ob_type != PyCFrame_Type) {
                                DecRef(curFrame->f_trace, isDebug);
                                IncRef(*traceFunc);
                                curFrame->f_trace = traceFunc.ToPython();
                            }
                            curFrame = (PyFrameObject*)GetPyObjectPointerNoDebugInfo(isDebug, curFrame->f_back);
                        }

                        delThreadTls(threadStateIndex);
                        setThreadTls(threadStateIndex, prevThread);
                        threadSwap(prevThread);
                    }
                    break;
                }
            } while (foundThread);
        }

        HMODULE hModule = NULL;
        if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCTSTR)GetCurrentModuleFilename, &hModule) != 0) {
            // set our handle so we can be unloaded on detach...
            dictSetItem(globalsDict.ToPython(),
                "debugger_dll_handle",
                intFromSizeT((size_t)hModule)
                );
        }

        return true;
    }

    connInfo.ReportError(ConnError_PythonNotFound);
    return false;
}

// Checks to see if the specified module is likely a Python interpreter.
bool IsPythonModule(HMODULE module, bool &isDebug) {
    wchar_t mod_name[MAX_PATH];
    isDebug = false;
    if (GetModuleBaseName(GetCurrentProcess(), module, mod_name, MAX_PATH)) {
        if (_wcsnicmp(mod_name, L"python", 6) == 0) {
            if (wcslen(mod_name) >= 10 && _wcsnicmp(mod_name + 8, L"_d", 2) == 0) {
                isDebug = true;
            }
            return true;
        }
    }
    return false;
}

DWORD __stdcall AttachWorker(LPVOID arg) {
    HANDLE hProcess = GetCurrentProcess();
    DWORD modSize = sizeof(HMODULE) * 1024;
    HMODULE* hMods = (HMODULE*)_malloca(modSize);
    if (hMods == nullptr) {
        return 0;
    }

    DWORD modsNeeded;
    while (!EnumProcessModules(hProcess, hMods, modSize, &modsNeeded)) {
        // try again w/ more space...
        _freea(hMods);
        hMods = (HMODULE*)_malloca(modsNeeded);
        if (hMods == nullptr) {
            return 0;
        }
        modSize = modsNeeded;
    }
    bool attached = false;
    {
        // scoped to clean connection info before we unload
        auto connInfo = GetConnectionInfo();
        bool pythonFound = false;
        if (connInfo.Succeeded) {
            for (size_t i = 0; i < modsNeeded / sizeof(HMODULE); i++) {
                bool isDebug;
                if (IsPythonModule(hMods[i], isDebug)) {
                    pythonFound = true;
                    if (DoAttach(hMods[i], connInfo, isDebug)) {
                        // we've successfully attached the debugger
                        attached = true;
                        break;
                    }

                }
            }
        }

        if (!attached) {
            if (connInfo.Buffer->ErrorNumber == 0) {
                if (!pythonFound) {
                    connInfo.ReportError(ConnError_PythonNotFound);
                } else {
                    connInfo.ReportError(ConnError_InterpreterNotInitialized);
                }
            }
            SetEvent(connInfo.Buffer->AttachStartingEvent);
        }
    }


    HMODULE hModule = NULL;
    if (!attached &&
        GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCTSTR)GetCurrentModuleFilename, &hModule) != 0) {
            // unload ourselves and exit if we failed to attach...
            FreeLibraryAndExitThread(hModule, 0);
    }
    return 0;
}

// initialize the new thread - we hold the GIL while this is running because
// we're being called from the main interpreter loop.  Here we call into the Python
// portion of the debugger, let it setup the thread object, and then we dispatch
// to it so that it gets the 1st call event.
int TraceGeneral(int interpreterId, PyObject *obj, PyFrameObject *frame, int what, PyObject *arg) {
    auto curInterpreter = _interpreterInfo[interpreterId];

    auto new_thread = curInterpreter->NewThreadFunction;
    if (new_thread == nullptr) {
        // attach isn't complete yet, we're racing with other threads...
        return 0;
    }

    auto call = curInterpreter->GetCall();
    if (call != nullptr && curInterpreter->EnsureCurrentThread()) {
        auto curThread = *curInterpreter->CurrentThread;

        bool isDebug = new_thread->_isDebug;

        _ASSERTE(curInterpreter->SetTrace != nullptr);
        curInterpreter->SetTrace(nullptr, nullptr);

        DecRef(
            call(
            new_thread->ToPython(),
            NULL
            ),
            isDebug
            );

        // now deliver the event we received to our trace object which just got installed.
        auto version = curInterpreter->GetVersion();
        if (PyThreadState_25_27::IsFor(version)) {
            ((PyThreadState_25_27*)curThread)->c_tracefunc(((PyThreadState_25_27*)curThread)->c_traceobj, frame, what, arg);
        } else if (PyThreadState_30_33::IsFor(version)) {
            ((PyThreadState_30_33*)curThread)->c_tracefunc(((PyThreadState_30_33*)curThread)->c_traceobj, frame, what, arg);
        } else if (PyThreadState_34_35::IsFor(version)) {
            ((PyThreadState_34_35*)curThread)->c_tracefunc(((PyThreadState_34_35*)curThread)->c_traceobj, frame, what, arg);
        }
    }
    return 0;
}

#define TRACE_FUNC(n) \
    int Trace ## n(PyObject *obj, PyFrameObject *frame, int what, PyObject *arg) { \
    return TraceGeneral(n, obj, frame, what, arg);                                 \
}

    TRACE_FUNC(0)
    TRACE_FUNC(1)
    TRACE_FUNC(2)
    TRACE_FUNC(3)
    TRACE_FUNC(4)
    TRACE_FUNC(5)
    TRACE_FUNC(6)
    TRACE_FUNC(7)
    TRACE_FUNC(8)
    TRACE_FUNC(9)

    Py_tracefunc traceFuncs[MAX_INTERPRETERS] = {
        Trace0,
        Trace1,
        Trace2,
        Trace3,
        Trace4,
        Trace5,
        Trace6,
        Trace7,
        Trace8,
        Trace9
};

void SetInitialTraceFunc(DWORD interpreterId, PyThreadState *thread) {
    auto curInterpreter = _interpreterInfo[interpreterId];

    auto version = curInterpreter->GetVersion();
    int gilstate_counter = 0;
    if (PyThreadState_25_27::IsFor(version)) {
        gilstate_counter = ((PyThreadState_25_27*)thread)->gilstate_counter;
    } else if (PyThreadState_30_33::IsFor(version)) {
        gilstate_counter = ((PyThreadState_30_33*)thread)->gilstate_counter;
    } else if (PyThreadState_34_35::IsFor(version)) {
        gilstate_counter = ((PyThreadState_34_35*)thread)->gilstate_counter;
    }

    if (gilstate_counter == 1) {
        // this was a newly created thread
        curInterpreter->SetTrace(traceFuncs[interpreterId], nullptr);
    }

}

PyThreadState *PyThreadState_NewGeneral(DWORD interpreterId, PyInterpreterState *interp) {
    auto curInterpreter = _interpreterInfo[interpreterId];
    _ASSERTE(curInterpreter->PyThreadState_New != nullptr);

    auto res = curInterpreter->PyThreadState_New(interp);
    if (res != nullptr &&
        curInterpreter->EnsureSetTrace() &&
        curInterpreter->EnsureThreadStateSwap()) {
            // we hold the GIL, but we could not have a valid thread yet, or
            // we could currently be on the wrong thread, so swap in the
            // new thread, set our trace func, and then swap it back out.

            PyThreadState* oldTs = curInterpreter->ThreadState_Swap(res);

            SetInitialTraceFunc(interpreterId, res);

            curInterpreter->ThreadState_Swap(oldTs);
    }

    return res;
}

#define PYTHREADSTATE_NEW(n)   \
    PyThreadState* PyThreadStateNew ## n(PyInterpreterState *interp) { \
    return PyThreadState_NewGeneral(n, interp);                        \
}

PYTHREADSTATE_NEW(0)
    PYTHREADSTATE_NEW(1)
    PYTHREADSTATE_NEW(2)
    PYTHREADSTATE_NEW(3)
    PYTHREADSTATE_NEW(4)
    PYTHREADSTATE_NEW(5)
    PYTHREADSTATE_NEW(6)
    PYTHREADSTATE_NEW(7)
    PYTHREADSTATE_NEW(8)
    PYTHREADSTATE_NEW(9)

    PyThreadState_NewFunc* newThreadStateFuncs[MAX_INTERPRETERS] = {
        PyThreadStateNew0,
        PyThreadStateNew1,
        PyThreadStateNew2,
        PyThreadStateNew3,
        PyThreadStateNew4,
        PyThreadStateNew5,
        PyThreadStateNew6,
        PyThreadStateNew7,
        PyThreadStateNew8,
        PyThreadStateNew9
};


// Handles calls to PyGILState_Ensure.  These calls come from C++ code and we've
// intercepted them by patching the import table in any DLLs importing the code.
// We then intercept the call, and setup tracing on the newly created thread.
PyGILState_STATE MyGilEnsureGeneral(DWORD interpreterId) {
    auto curInterpreter = _interpreterInfo[interpreterId];
    auto res = curInterpreter->PyGILState_Ensure();
    // we now hold the global interpreter lock

    if (res == PyGILState_UNLOCKED) {
        if (curInterpreter->EnsureCurrentThread()) {
            auto thread = *curInterpreter->CurrentThread;

            if (thread != nullptr && curInterpreter->EnsureSetTrace()) {
                SetInitialTraceFunc(interpreterId, thread);
            }
        }
    }

    return res;
}

#define GIL_ENSURE(n)                   \
    PyGILState_STATE GilEnsure ## n() { \
    return MyGilEnsureGeneral(n);       \
}

GIL_ENSURE(0)
GIL_ENSURE(1)
GIL_ENSURE(2)
GIL_ENSURE(3)
GIL_ENSURE(4)
GIL_ENSURE(5)
GIL_ENSURE(6)
GIL_ENSURE(7)
GIL_ENSURE(8)
GIL_ENSURE(9)

PyGILState_EnsureFunc* gilEnsureFuncs[MAX_INTERPRETERS] = {
    GilEnsure0,
    GilEnsure1,
    GilEnsure2,
    GilEnsure3,
    GilEnsure4,
    GilEnsure5,
    GilEnsure6,
    GilEnsure7,
    GilEnsure8,
    GilEnsure9
};

// http://msdn.microsoft.com/en-us/library/dd347460(v=VS.85).aspx
typedef struct _LDR_DLL_LOADED_NOTIFICATION_DATA {
    ULONG Flags;                    //Reserved.
    PCUNICODE_STRING FullDllName;   //The full path name of the DLL module.
    PCUNICODE_STRING BaseDllName;   //The base file name of the DLL module.
    PVOID DllBase;                  //A pointer to the base address for the DLL in memory.
    ULONG SizeOfImage;              //The size of the DLL image, in bytes.
} LDR_DLL_LOADED_NOTIFICATION_DATA, *PLDR_DLL_LOADED_NOTIFICATION_DATA;

typedef struct _LDR_DLL_UNLOADED_NOTIFICATION_DATA {
    ULONG Flags;                    //Reserved.
    PCUNICODE_STRING FullDllName;   //The full path name of the DLL module.
    PCUNICODE_STRING BaseDllName;   //The base file name of the DLL module.
    PVOID DllBase;                  //A pointer to the base address for the DLL in memory.
    ULONG SizeOfImage;              //The size of the DLL image, in bytes.
} LDR_DLL_UNLOADED_NOTIFICATION_DATA, *PLDR_DLL_UNLOADED_NOTIFICATION_DATA;

typedef union _LDR_DLL_NOTIFICATION_DATA {
    LDR_DLL_LOADED_NOTIFICATION_DATA Loaded;
    LDR_DLL_UNLOADED_NOTIFICATION_DATA Unloaded;
} LDR_DLL_NOTIFICATION_DATA, *PLDR_DLL_NOTIFICATION_DATA;

typedef VOID CALLBACK LDR_DLL_NOTIFICATION_FUNCTION(
    __in      ULONG NotificationReason,
    __in     _LDR_DLL_NOTIFICATION_DATA* NotificationData,
    __in_opt  PVOID Context
    );

typedef NTSTATUS NTAPI LdrRegisterDllNotificationFunction(
    __in      ULONG Flags,
    __in      LDR_DLL_NOTIFICATION_FUNCTION* NotificationFunction,
    __in_opt  PVOID Context,
    __out     PVOID *Cookie
    );

typedef NTSTATUS NTAPI LdrUnregisterDllNotification(
    __in  PVOID Cookie
    );

#define LDR_DLL_NOTIFICATION_REASON_LOADED 1
#define LDR_DLL_NOTIFICATION_REASON_UNLOADED 2

void CALLBACK DllLoadNotify(ULONG NotificationReason, _LDR_DLL_NOTIFICATION_DATA* NotificationData, PVOID Context) {
    if (NotificationReason == LDR_DLL_NOTIFICATION_REASON_LOADED) {
        // patch any Python functions the newly loaded DLL is calling.
        for (DWORD i = 0; i < _interpreterCount; i++) {
            PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)NotificationData->Loaded.DllBase;
            InterpreterInfo* curInterpreter = _interpreterInfo[i];

            char mod_name[MAX_PATH];
            if (GetModuleBaseNameA(GetCurrentProcess(), curInterpreter->Interpreter, mod_name, MAX_PATH)) {
                if (curInterpreter->PyGILState_Ensure != nullptr) {
                    PatchIAT(dosHeader,
                        curInterpreter->PyGILState_Ensure,
                        mod_name,
                        gilEnsureFuncs[i]);
                }

                if (curInterpreter->PyThreadState_New != nullptr) {
                    PatchIAT(dosHeader,
                        curInterpreter->PyThreadState_New,
                        mod_name,
                        newThreadStateFuncs[i]);
                }
            }
        }
    }
}

PVOID _LoaderCookie = nullptr;

void Attach() {
    HANDLE hProcess = GetCurrentProcess();
    DWORD modSize = sizeof(HMODULE) * 1024;
    HMODULE* hMods = (HMODULE*)_malloca(modSize);
    DWORD modsNeeded;
    if (hMods == nullptr) {
        modsNeeded = 0;
        return;
    } else {
        while (!EnumProcessModulesHelper(hProcess, hMods, modSize, &modsNeeded)) {
            // try again w/ more space...
            _freea(hMods);
            hMods = (HMODULE*)_malloca(modsNeeded);
            if (hMods == nullptr) {
                modsNeeded = 0;
                break;
            }
            modSize = modsNeeded;
        }

        for (size_t i = 0; i < modsNeeded / sizeof(HMODULE); i++) {
            bool isDebug;
            if (IsPythonModule(hMods[i], isDebug)) {
                if (_interpreterCount >= MAX_INTERPRETERS) {
                    break;
                }

                InterpreterInfo* curInterpreter = _interpreterInfo[_interpreterCount] = new InterpreterInfo(hMods[i], isDebug);

                char mod_name[MAX_PATH];
                if (GetModuleBaseNameA(GetCurrentProcess(), hMods[i], mod_name, MAX_PATH)) {
                    auto gilEnsure = GetProcAddress(hMods[i], "PyGILState_Ensure");
                    if (gilEnsure != nullptr) {
                        curInterpreter->PyGILState_Ensure = (PyGILState_EnsureFunc*)gilEnsure;

                        PatchFunction(mod_name, gilEnsure, gilEnsureFuncs[_interpreterCount]);
                    }

                    auto newTs = GetProcAddress(hMods[i], "PyThreadState_New");
                    if (newTs != nullptr) {
                        curInterpreter->PyThreadState_New = (PyThreadState_NewFunc*)newTs;

                        PatchFunction(mod_name, newTs, newThreadStateFuncs[_interpreterCount]);
                    }
                }

                _interpreterCount++;
            }
        }
    }

    HMODULE kernelMod = GetModuleHandle(L"kernel32.dll");
    if (kernelMod != nullptr) {
        // not available on Windows XP, not much we can do in that case...
        auto ldrRegisterNotify = (LdrRegisterDllNotificationFunction*)GetProcAddress(kernelMod, "LdrRegisterDllNotification");
        if (ldrRegisterNotify != nullptr) {
            ldrRegisterNotify(
                0,
                &DllLoadNotify,
                NULL,
                &_LoaderCookie
                );
        }
    }

    // create a new thread to run the attach code on so we're not running in DLLMain
    // Note we do no synchronization with other threads at all, and we don't care that
    // thread detach will be called w/o an attach, so this is safe.

    DWORD threadId;
    CreateThread(NULL, 0, &AttachWorker, NULL, 0, &threadId);
}


void Detach() {
    if (_LoaderCookie != nullptr) {
        HMODULE kernelMod = GetModuleHandle(L"kernel32.dll");
        if (kernelMod != nullptr) {
            // not available on Windows XP, not much we can do in that case...
            auto ldrUnreg = (LdrUnregisterDllNotification*)GetProcAddress(kernelMod, "LdrUnregisterDllNotification");
            if (ldrUnreg != nullptr) {
                ldrUnreg(_LoaderCookie);
            }
        }
    }

    HANDLE hProcess = GetCurrentProcess();
    DWORD modSize = sizeof(HMODULE) * 1024;
    HMODULE* hMods = (HMODULE*)_malloca(modSize);
    DWORD modsNeeded = 0;
    if (hMods == nullptr) {
        modsNeeded = 0;
        return;
    }

    while (!EnumProcessModulesHelper(hProcess, hMods, modSize, &modsNeeded)) {
        // try again w/ more space...
        _freea(hMods);
        hMods = (HMODULE*)_malloca(modsNeeded);
        if (hMods == nullptr) {
            modsNeeded = 0;
            break;
        }
        modSize = modsNeeded;
    }

    for (size_t i = 0; i < modsNeeded / sizeof(HMODULE); i++) {
        bool isDebug;
        if (IsPythonModule(hMods[i], isDebug)) {
            for (DWORD j = 0; j < _interpreterCount; j++) {
                InterpreterInfo* curInterpreter = _interpreterInfo[j];

                if (curInterpreter->Interpreter == hMods[i]) {
                    char mod_name[MAX_PATH];
                    if (GetModuleBaseNameA(GetCurrentProcess(), hMods[i], mod_name, MAX_PATH)) {
                        PatchFunction(mod_name, gilEnsureFuncs[j], curInterpreter->PyGILState_Ensure);
                        PatchFunction(mod_name, newThreadStateFuncs[j], curInterpreter->PyThreadState_New);
                    }
                }
            }
        }
    }
}