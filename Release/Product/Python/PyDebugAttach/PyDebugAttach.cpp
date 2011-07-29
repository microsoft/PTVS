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
#include <Windows.h>
#include <Psapi.h>
#include <TlHelp32.h>
#include <strsafe.h>
#include <hash_map>
#include <string>
#include <fstream>
#include <hash_set>
#include "..\VsPyProf\python.h"
    
using namespace std;

typedef int (Py_IsInitialized) ();
typedef void (PyEval_Lock) (); // Acquire/Release lock
typedef void (PyThreadState_API) (PyThreadState *); // Acquire/Release lock
typedef PyInterpreterState* (PyInterpreterState_Head)();
typedef PyThreadState* (PyInterpreterState_ThreadHead)(PyInterpreterState* interp);
typedef PyThreadState* (PyThreadState_Next)(PyThreadState *tstate);
typedef PyThreadState* (PyThreadState_Swap)(PyThreadState *tstate);
typedef int (PyRun_SimpleString)(const char *command);
typedef PyObject* (PyDict_New)();
typedef PyObject* (Py_CompileString)(const char *str, const char *filename, int start);
typedef int (PyEval_EvalCode)(PyObject *co, PyObject *globals, PyObject *locals);
typedef PyObject* (PyDict_GetItemString)(PyObject *p, const char *key);
typedef PyObject* (PyObject_CallFunctionObjArgs)(PyObject *callable, ...);    // call w/ varargs, last arg should be NULL
typedef void (PyErr_Fetch)(PyObject **, PyObject **, PyObject **);
typedef PyObject* (PyEval_GetBuiltins)();
typedef int (PyDict_SetItemString)(PyObject *dp, const char *key, PyObject *item);
typedef int (PyEval_ThreadsInitialized)();
typedef void (Py_AddPendingCall)(int (*func)(void *), void*);
typedef const char* (GetVersionFunc) ();
typedef PyObject* (PyInt_FromLong)(long);
typedef PyObject* (PyString_FromString)(const char* s);
typedef void PyEval_SetTrace(Py_tracefunc func, PyObject *obj);
typedef void (PyErr_Restore)(PyObject *type, PyObject *value, PyObject *traceback);
typedef void (PyErr_Fetch)(PyObject **ptype, PyObject **pvalue, PyObject **ptraceback);
typedef PyObject* (PyErr_Occurred)();
typedef PyObject* (PyImport_ImportModule)(const char *name);
typedef PyObject* (PyObject_GetAttrString)(PyObject *o, const char *attr_name);
typedef PyObject* (PyObject_SetAttrString)(PyObject *o, const char *attr_name, PyObject* value);
typedef PyObject* (PyBool_FromLong)(long v);
typedef enum {PyGILState_LOCKED, PyGILState_UNLOCKED} PyGILState_STATE;
typedef PyGILState_STATE (PyGILState_Ensure)();
typedef void (PyGILState_Release)(PyGILState_STATE);
typedef unsigned long (_PyEval_GetSwitchInterval)(void);
typedef void (_PyEval_SetSwitchInterval)(unsigned long microseconds);
typedef void* (PyThread_get_key_value)(int);
typedef int (PyThread_set_key_value)(int, void*);
typedef void (PyThread_delete_key_value)(int);

wstring GetCurrentModuleFilename()
{ 
    HMODULE hModule = NULL;
    if(GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCTSTR)GetCurrentModuleFilename, &hModule)!=0)
    {
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

int AttachCallback(void *initThreads) {
    // initialize us for threading, this will acquire the GIL if not already created, and is a nop if the GIL is created.
    // This leaves us in the proper state when we return back to the runtime whether the GIL was created or not before
    // we were called.
    ((PyEval_Lock*)initThreads)();
    return 0;
}

char* ReadDebuggerCode(wchar_t* newName) {
    ifstream filestr;
    filestr.open(newName, ios::binary);
    if(filestr.fail()) {
        return nullptr;
    }

    // get length of file:
    filestr.seekg (0, ios::end);
    auto length = filestr.tellg();
    filestr.seekg (0, ios::beg);

    int len = length;
    char* buffer = new char[len + 1];
    filestr.read(buffer, len);
    buffer[len] = 0;

    // remove carriage returns, copy zero byte
    for(int read = 0, write=0; read<=len; read++) {
        if(buffer[read] == '\r') {
            continue;
        }else if (write != read) {
            buffer[write] = buffer[read];
        }
        write++;
    }

    return buffer;
}

// create a custom heap for our hash map.  This is necessary because if we suspend a thread while in a heap function
// then we could deadlock here.  We need to be VERY careful about what we do while the threads are suspended.
class PrivateHeap {
public:
    HANDLE heap;

    PrivateHeap() {
        heap = HeapCreate(0, 0, 0);
    }
};

template <typename T> class PrivateHeapAllocator {
    
public:
      typedef size_t    size_type;
      typedef ptrdiff_t difference_type;
      typedef T*        pointer;
      typedef const T*  const_pointer;
      typedef T&        reference;
      typedef const T&  const_reference;
      typedef T         value_type;
      template <class U> struct rebind { typedef allocator<U>
                                         other; };

    PrivateHeapAllocator() {
    }
    
    pointer allocate(size_type size, allocator<void>::const_pointer hint = 0) {
        HeapAlloc(_heap.heap, 0, size);
    }

    void deallocate(pointer p, size_type n) {
        HeapFree(_heap.heap, 0, p);
    }

private:
    static PrivateHeap _heap;   
};

typedef hash_map<DWORD, HANDLE, stdext::hash_compare<DWORD, std::less<DWORD> >, PrivateHeapAllocator<pair<DWORD, HANDLE> > > MyHashMap;

void ResumeThreads(MyHashMap &suspendedThreads) {
    for(auto start = suspendedThreads.begin();  start != suspendedThreads.end(); start++) {
        ResumeThread((*start).second);
        CloseHandle((*start).second);
    }
    suspendedThreads.clear();
}

// Suspends all threads ensuring that they are not currently in a call to Py_AddPendingCall.
void SuspendThreads(MyHashMap &suspendedThreads, Py_AddPendingCall* addPendingCall, PyEval_ThreadsInitialized* threadsInited) {
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


                        if(te.th32ThreadID != curThreadId && suspendedThreads.find(te.th32ThreadID) == suspendedThreads.end()) {
                            auto hThread = OpenThread(THREAD_ALL_ACCESS, FALSE, te.th32ThreadID);
                            if(hThread != nullptr) {
                                SuspendThread(hThread);

                                bool addingPendingCall = false;
#if defined(_X86_)
                                CONTEXT context;
                                GetThreadContext(hThread, &context);
                                if(context.Eip >= *((DWORD*)addPendingCall) && context.Eip <= (*((DWORD*)addPendingCall)) + 0x100) {
                                    addingPendingCall = true;
                                }
#elif defined(_AMD64_)
                                CONTEXT context;
                                GetThreadContext(hThread, &context);
                                if(context.Rip >= *((DWORD64*)addPendingCall) && context.Rip <= *((DWORD64*)addPendingCall + 0x100)) {
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
    } while(suspended && !threadsInited());
}

PyObject* GetPyObjectPointerNoDebugInfo(bool isDebug, PyObject* object) {
    if(object != nullptr && isDebug) {
        // debug builds have 2 extra pointers at the front that we don't care about
        return (PyObject*)((size_t*)object+2);
    }
    return object;
}

void DecRef(PyObject* object, bool isDebug) {
    auto noDebug = GetPyObjectPointerNoDebugInfo(isDebug, object);  

    if(noDebug != nullptr && --noDebug->ob_refcnt == 0) {
        ((PyTypeObject*)GetPyObjectPointerNoDebugInfo(isDebug, noDebug->ob_type))->tp_dealloc(object);
    }
}

void IncRef(PyObject* object) {
    object->ob_refcnt++;
}

// Helper class so we can use RAII for freeing python objects when they go out of scope
class PyObjectHolder {
private:
    PyObject* _object;
    bool _isDebug;
public:
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
        if(_object != nullptr && addRef) {
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

// Structure for our shared memory communication, aligned to be identical on 64-bit and 32-bit
struct MemoryBuffer {
    int PortNumber;             // offset 0-4
    __declspec(align(8)) HANDLE AttachStartingEvent;  // offset 8 - 16
    __declspec(align(8)) HANDLE AttachDoneEvent;  // offset 16 - 24
    __declspec(align(8)) int ErrorNumber;            // offset 24-28
    int VersionNumber;          // offset 28-32
    char DebugId[1];            // null terminated string
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

    void ReportError(int errorNum) {
        Buffer->ErrorNumber = errorNum;
    }

    void SetVersion(int major, int minor) {
        // must be kept in sync with PythonLanguageVersion.cs
        Buffer->VersionNumber = (major << 8) | minor;
    }

    ~ConnectionInfo() {
        if(Succeeded) {
            CloseHandle(Buffer->AttachStartingEvent);

            auto attachDoneEvent = Buffer->AttachDoneEvent;
            UnmapViewOfFile(Buffer);
            CloseHandle(FileMapping);
        
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

    pBuf = (char*) MapViewOfFile(hMapFile, // handle to map object
        FILE_MAP_ALL_ACCESS,  // read/write permission
        0,                    
        0,                       
        1024);                   

    if (pBuf == NULL)  { 
        CloseHandle(hMapFile);
        return ConnectionInfo();
    }

    return ConnectionInfo((MemoryBuffer*)pBuf, hMapFile);
}

// Error messages - ust be kept in sync w/ PythonAttach.cs
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
	ConnError_PyDebugAttachNotFound
};

long GetPythonThreadId(const char* version, PyThreadState* curThread) {
    long threadId;
    if(version[0] == '3') {
        threadId = curThread->_30_31.thread_id;
    }else if(version[0] == '2' && version[2] == '4') {
        threadId = curThread->_24.thread_id;
    }else{
        threadId = curThread->_25_27.thread_id;
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

bool DoAttach(HMODULE module, ConnectionInfo& connInfo, bool isDebug) {
    // Python DLL?
    auto isInit = (Py_IsInitialized*)GetProcAddress(module, "Py_IsInitialized");
    auto getVersion = (GetVersionFunc*)GetProcAddress(module, "Py_GetVersion");

    if(getVersion != nullptr && isInit != nullptr && isInit()) {
        auto version = getVersion();

        // found initialized Python runtime, gather and check the APIs we need for a successful attach...
        auto addPendingCall = (Py_AddPendingCall*)GetProcAddress(module, "Py_AddPendingCall");
        auto curPythonThread = (PyThreadState**)(void*)GetProcAddress(module, "_PyThreadState_Current");
        auto interpHeap = (PyInterpreterState_Head*)GetProcAddress(module, "PyInterpreterState_Head");
        auto gilEnsure = (PyGILState_Ensure*)GetProcAddress(module, "PyGILState_Ensure");
        auto gilRelease = (PyGILState_Release*)GetProcAddress(module, "PyGILState_Release");
        auto threadHead = (PyInterpreterState_ThreadHead*)GetProcAddress(module, "PyInterpreterState_ThreadHead");
        auto initThreads = (PyEval_Lock*)GetProcAddress(module, "PyEval_InitThreads");
        auto releaseLock = (PyEval_Lock*)GetProcAddress(module, "PyEval_ReleaseLock");
        auto threadsInited = (PyEval_ThreadsInitialized*)GetProcAddress(module, "PyEval_ThreadsInitialized");
        auto threadNext = (PyThreadState_Next*)GetProcAddress(module, "PyThreadState_Next");
        auto threadSwap = (PyThreadState_Swap*)GetProcAddress(module, "PyThreadState_Swap");
        auto pyDictNew = (PyDict_New*)GetProcAddress(module, "PyDict_New");
        auto pyCompileString = (Py_CompileString*)GetProcAddress(module, "Py_CompileString");
        auto pyEvalCode = (PyEval_EvalCode*)GetProcAddress(module, "PyEval_EvalCode");
        auto getDictItem = (PyDict_GetItemString*)GetProcAddress(module, "PyDict_GetItemString");
        auto call = (PyObject_CallFunctionObjArgs*)GetProcAddress(module, "PyObject_CallFunctionObjArgs");
        auto getBuiltins = (PyEval_GetBuiltins*)GetProcAddress(module, "PyEval_GetBuiltins");
        auto dictSetItem = (PyDict_SetItemString*)GetProcAddress(module, "PyDict_SetItemString");
        PyInt_FromLong* intFromLong;
        PyString_FromString* strFromString;
        if(strlen(version) > 0 && version[0] == '3') {
            intFromLong = (PyInt_FromLong*)GetProcAddress(module, "PyLong_FromLong");
            strFromString = (PyString_FromString*)GetProcAddress(module, "PyUnicodeUCS2_FromString");
        }else{
            intFromLong = (PyInt_FromLong*)GetProcAddress(module, "PyInt_FromLong");
            strFromString = (PyString_FromString*)GetProcAddress(module, "PyString_FromString");
        }
        auto intervalCheck = (int*)GetProcAddress(module, "_Py_CheckInterval");
        auto errOccurred = (PyErr_Occurred*)GetProcAddress(module, "PyErr_Occurred");
        auto pyErrFetch = (PyErr_Fetch*)GetProcAddress(module, "PyErr_Fetch");
        auto pyErrRestore = (PyErr_Restore*)GetProcAddress(module, "PyErr_Restore");
        auto pyImportMod = (PyImport_ImportModule*)GetProcAddress(module, "PyImport_ImportModule");
        auto pyGetAttr = (PyObject_GetAttrString*)GetProcAddress(module, "PyObject_GetAttrString");
        auto pySetAttr = (PyObject_SetAttrString*)GetProcAddress(module, "PyObject_SetAttrString");
        auto pyNone = (PyObject*)GetProcAddress(module, "_Py_NoneStruct");
        auto getSwitchInterval = (_PyEval_GetSwitchInterval*)GetProcAddress(module, "_PyEval_GetSwitchInterval");
        auto setSwitchInterval = (_PyEval_SetSwitchInterval*)GetProcAddress(module, "_PyEval_SetSwitchInterval");
        auto boolFromLong = (PyBool_FromLong*)GetProcAddress(module, "PyBool_FromLong");
        auto getThreadTls = (PyThread_get_key_value*)GetProcAddress(module, "PyThread_get_key_value");
        auto setThreadTls = (PyThread_set_key_value*)GetProcAddress(module, "PyThread_set_key_value");
        auto delThreadTls = (PyThread_delete_key_value*)GetProcAddress(module, "PyThread_delete_key_value");

        if (addPendingCall== nullptr || curPythonThread == nullptr || interpHeap == nullptr || gilEnsure == nullptr || gilRelease== nullptr || threadHead==nullptr ||
            initThreads==nullptr || getVersion == nullptr || releaseLock== nullptr || threadsInited== nullptr || threadNext==nullptr || threadSwap==nullptr ||
            pyDictNew==nullptr || pyCompileString == nullptr || pyEvalCode == nullptr || getDictItem == nullptr || call == nullptr ||
            getBuiltins == nullptr || dictSetItem == nullptr || intFromLong == nullptr || pyErrRestore == nullptr || pyErrFetch == nullptr ||
            errOccurred == nullptr || pyImportMod == nullptr || pyGetAttr == nullptr || pyNone == nullptr || pySetAttr == nullptr || boolFromLong == nullptr ||
            getThreadTls == nullptr || setThreadTls == nullptr || delThreadTls == nullptr) {
            // we're missing some APIs, we cannot attach.
            connInfo.ReportError(ConnError_PythonNotFound);
            return false;
        }

        auto head = interpHeap();
        if(head == nullptr) {
            // this interpreter is loaded butt not initialized.
            connInfo.ReportError(ConnError_InterpreterNotInitialized);
            return false;
        }        

        bool threadSafeAddPendingCall = false;
        
        // check that we're a supported version
        if (strlen(version) < 3 ||                                             // not enough version info
            (version[0] != '2' && version[0] != '3') ||                        // not v2 or v3
            (version[0] == '2' && (version[2] < '4' || version[2] > '7'))  ||  // not v2.4 - v2.7
            (version[0] == '3' && (version[2] < '0' || version[2] > '2'))      // not v3.0 - 3.2
            ) {

            connInfo.ReportError(ConnError_UnknownVersion);
            return false;
        } else if(version[0] == '3' || (version[0] == '2' && version[2] >= '7')) {
            threadSafeAddPendingCall = true;
        }
        connInfo.SetVersion(version[0] - '0', version[2] - '0');

        if(!threadsInited()) {
            int saveIntervalCheck;
            unsigned long saveLongIntervalCheck;
            if (intervalCheck != nullptr) { // not available on 3.2
                saveIntervalCheck = *intervalCheck;
                *intervalCheck = -1;    // lower the interval check so pending calls are processed faster
            } else if(getSwitchInterval != nullptr && setSwitchInterval != nullptr) {
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

            MyHashMap suspendedThreads;            
            
            bool addedPendingCall = false;

            if(addPendingCall != nullptr && threadSafeAddPendingCall) {
                // we're on a thread safe Python version, go ahead and pend our call to initialize threading.
                addPendingCall(&AttachCallback, initThreads);
                addedPendingCall = true;
            }

            do {
                SuspendThreads(suspendedThreads, addPendingCall, threadsInited);

                if (!threadsInited()) { 
                    if(*curPythonThread == nullptr) {
                        // no threads are currently running, it is safe to initialize multi threading.
                        initThreads();
                        releaseLock();
                    }else if(!addedPendingCall) {
                        // someone holds the GIL but no one is actively adding any pending calls.  We can pend our call
                        // and initialize threads.
                        addPendingCall(&AttachCallback, initThreads);
                        addedPendingCall = true;
                    }
                }

                ResumeThreads(suspendedThreads);
            }while(!threadsInited());

            if (intervalCheck != nullptr) {
                *intervalCheck = saveIntervalCheck;
            } else if(setSwitchInterval != nullptr) {
                setSwitchInterval(saveLongIntervalCheck);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // go ahead and bring in the debugger module and initialize all threads in the process...
        GilHolder gilLock(gilEnsure, gilRelease);   // acquire and hold the GIL until done...

        auto filename = GetCurrentModuleFilename();
        if(filename.length() == 0) {
            return nullptr;
        }

        wchar_t drive[_MAX_DRIVE], dir[_MAX_DIR], file[_MAX_FNAME], ext[_MAX_EXT];
        _wsplitpath_s(filename.c_str(), drive, _MAX_DRIVE, dir, _MAX_DIR, file, _MAX_FNAME, ext, _MAX_EXT);

        wchar_t newName[MAX_PATH];
#if defined(_AMD64_)
        _wmakepath_s(newName, drive, dir, L"..\\visualstudio_py_debugger", L".py");
#else
        _wmakepath_s(newName, drive, dir, L"visualstudio_py_debugger", L".py");
#endif

        // run the debugger code...
        auto debuggerCode = ReadDebuggerCode(newName);
        if(debuggerCode == nullptr) {
            connInfo.ReportError(ConnError_LoadDebuggerFailed);
            return false;
        }

        auto code = PyObjectHolder(isDebug, pyCompileString(debuggerCode, "visualstudio_py_debugger.py", 257 /*Py_file_input*/));
        delete [] debuggerCode;

        if(*code == nullptr) {                
            connInfo.ReportError(ConnError_LoadDebuggerFailed);
            return false;
        }

        // create a globals/locals dict for evaluating the code...
        auto globalsDict = PyObjectHolder(isDebug, pyDictNew());
        dictSetItem(globalsDict.ToPython(), "__builtins__", getBuiltins());   
        int size = WideCharToMultiByte(CP_UTF8, 0, newName, wcslen(newName), NULL, 0, NULL, NULL);
        char* filenameBuffer = new char[size];
        if(WideCharToMultiByte(CP_UTF8, 0, newName, wcslen(newName), filenameBuffer, size, NULL, NULL) != 0) {
            filenameBuffer[size] = 0;
            dictSetItem (globalsDict.ToPython(), "__file__", strFromString(filenameBuffer));
        }
        pyEvalCode(code.ToPython(), globalsDict.ToPython(), globalsDict.ToPython());

        // now initialize debugger process wide state
        auto attach_process = PyObjectHolder(isDebug, getDictItem(globalsDict.ToPython(), "attach_process"), true);
        auto new_thread = PyObjectHolder(isDebug, getDictItem(globalsDict.ToPython(), "new_thread"), true);

        if (*attach_process == nullptr || *new_thread == nullptr) {
            connInfo.ReportError(ConnError_LoadDebuggerBadDebugger);
            return false;
        }

        auto pyPortNum = PyObjectHolder(isDebug, intFromLong(connInfo.Buffer->PortNumber));

        // we are about to open our socket and wait for the connection, let VS know.
        connInfo.ReportError(ConnError_None);
        SetEvent(connInfo.Buffer->AttachStartingEvent);   

        auto debugId = PyObjectHolder(isDebug, strFromString(connInfo.Buffer->DebugId));
        
        DecRef(call(attach_process.ToPython(), pyPortNum.ToPython(), debugId.ToPython(), NULL), isDebug);       

        auto sysMod = PyObjectHolder(isDebug, pyImportMod("sys"));
        if(*sysMod == nullptr) {
            connInfo.ReportError(ConnError_SysNotFound);
            return false;
        }

        auto settrace = PyObjectHolder(isDebug, pyGetAttr(sysMod.ToPython(), "settrace"));
        if(*settrace == nullptr) {
            connInfo.ReportError(ConnError_SysSetTraceNotFound); 
            return false;
        }
        
        auto gettrace = PyObjectHolder(isDebug, pyGetAttr(sysMod.ToPython(), "gettrace"));

        // we need to walk the thread list each time after we've initialized a thread so that we are always
        // dealing w/ a valid thread list (threads can exit when we run code and therefore the current thread
        // could be corrupt).  We also don't care about newly created threads as our start_new_thread wrapper
        // will handle those.  So we collect the initial set of threads first here so that we don't keep iterating
        // if the program is spawning large numbers of threads.
        hash_set<PyThreadState*> initialThreads;
        for(auto curThread = threadHead(head); curThread != nullptr; curThread = threadNext(curThread)) {
            initialThreads.insert(curThread);
        }

        auto pyTrue = boolFromLong(1);
        auto pyFalse = boolFromLong(0);
        hash_set<PyThreadState*> seenThreads;
        {
            // Python 3.2's GIL has changed and we need it to be less aggressive in the face of heavy contention
            // so that we can successfully attach.  So we prevent it from switching us out here.
            unsigned long saveLongIntervalCheck;
            if(getSwitchInterval != nullptr && setSwitchInterval != nullptr) {
                saveLongIntervalCheck = getSwitchInterval();
                setSwitchInterval(INFINITE);
            }

            // find what index is holding onto the thread state...
            auto curThread = *curPythonThread;
            int threadStateIndex = -1;
            for(int i = 0; i < 100000; i++) {
                void* value = getThreadTls(i);
                if(value == curThread) {
                    threadStateIndex = i;
                    break;
                }
            }

            bool foundThread;
            int processedThreads = 0;
            bool firstThread = true;
            do {
                foundThread = false;
                for(auto curThread = threadHead(head); curThread != nullptr; curThread = threadNext(curThread)) {
                    if(initialThreads.find(curThread) == initialThreads.end() ||
                        seenThreads.insert(curThread).second == false) {
                        continue;
                    }
                    foundThread = true;
                    processedThreads++;

                    long threadId = GetPythonThreadId(version, curThread);
                    // skip this thread - it doesn't really have any Python code on it...
                    if(threadId != GetCurrentThreadId()) {
                        // create new debugger Thread object on our injected thread
                        auto pyThreadId = PyObjectHolder(isDebug, intFromLong(threadId));           
                        PyFrameObject* frame;
                        // update all of the frames so they have our trace func
                        if(version[0] == '3') {
                            frame = curThread->_30_31.frame;
                        }else if(version[0] == '2' && version[2] == '4') {
                            frame = curThread->_24.frame;
                        }else{                            
                            frame = curThread->_25_27.frame;
                        }

                        auto threadObj = PyObjectHolder(isDebug, call(new_thread.ToPython(), pyThreadId.ToPython(), firstThread ? pyTrue : pyFalse, frame, NULL));
                        if(threadObj.ToPython() == pyNone || *threadObj == nullptr) {
                            break;
                        }

                        firstThread = false;
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
                        if(errOccured) {
                            pyErrFetch(&type, &value, &traceback);
                        }

                        auto traceFunc = PyObjectHolder(isDebug, pyGetAttr(threadObj.ToPython(), "trace_func"));
                        
                        if(*gettrace == NULL) {
                            DecRef(call(settrace.ToPython(), traceFunc.ToPython(), NULL), isDebug);
                        }else{
                            auto existingTraceFunc = PyObjectHolder(isDebug, call(gettrace.ToPython(), NULL));

                            DecRef(call(settrace.ToPython(), traceFunc.ToPython(), NULL), isDebug);

                            if (existingTraceFunc.ToPython() != pyNone) {
                                pySetAttr(threadObj.ToPython(), "prev_trace_func", existingTraceFunc.ToPython());
                            }
                        }

                        if(errOccured) {
                            pyErrRestore(type, value, traceback);
                        }

                        // update all of the frames so they have our trace func
                        auto curFrame = (PyFrameObject*)GetPyObjectPointerNoDebugInfo(isDebug, frame);
                        while(curFrame != nullptr) {
                            DecRef(curFrame->f_trace, isDebug);
                            IncRef(*traceFunc);
                            curFrame->f_trace = traceFunc.ToPython();
                            curFrame = (PyFrameObject*)GetPyObjectPointerNoDebugInfo(isDebug, curFrame->f_back);
                        }
                        
                        delThreadTls(threadStateIndex);
                        setThreadTls(threadStateIndex, prevThread);
                        threadSwap(prevThread);
                    }
                    break;
                }
            }while(foundThread);

            if(getSwitchInterval != nullptr && setSwitchInterval != nullptr) {
                setSwitchInterval(saveLongIntervalCheck);
            }
        }
        return true;
    }

    connInfo.ReportError(ConnError_PythonNotFound);
    return false;
}

DWORD __stdcall AttachWorker(LPVOID arg) {
    HANDLE hProcess = GetCurrentProcess();
    size_t modSize = sizeof(HMODULE) * 1024;
    HMODULE* hMods = (HMODULE*)_malloca(modSize);
    if(hMods == nullptr) {
        return 0;
    }

    DWORD modsNeeded;
    while(!EnumProcessModules(hProcess, hMods, modSize, &modsNeeded)) {               
        // try again w/ more space...
        _freea(hMods);
        hMods = (HMODULE*)_malloca(modsNeeded);
        if(hMods == nullptr) {
            return 0;
        }
        modSize = modsNeeded;
    }
    {
        // scoped to clean connection info before we unload
        auto connInfo = GetConnectionInfo();
        if(connInfo.Succeeded) {
            for(size_t i = 0; i<modsNeeded/sizeof(HMODULE); i++) {
                wchar_t mod_name[MAX_PATH];
                if(GetModuleBaseName(hProcess, hMods[i], mod_name, MAX_PATH)) {            
                    if(_wcsnicmp(mod_name, L"python", 6) == 0) {

                        bool isDebug = false;
                        if(wcslen(mod_name) >= 10 && _wcsnicmp(mod_name + 8, L"_d", 2) == 0) {
                            isDebug = true;
                        }

                        if(DoAttach(hMods[i], connInfo, isDebug)) {
                            // we've successfully attached the debugger
                            break;
                        }
                    }
                }
            }
        }
    }

    HMODULE hModule = NULL;
    if(GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCTSTR)GetCurrentModuleFilename, &hModule)!=0) {
        // unload ourselves and exit...
        FreeLibraryAndExitThread(hModule, 0);
    }
    return 0;
}

void Attach() {   
    // create a new thread to run the attach code on so we're not running in DLLMain
    // Note we do no synchronization with other threads at all, and we don't care that
    // thread detach will be called w/o an attach, so this is safe.

    DWORD threadId;
    CreateThread(NULL, 0, &AttachWorker, NULL, 0, &threadId);
}
