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

#ifndef __PYTHONAPI_H__
#define __PYTHONAPI_H__

#include <Windows.h>
#include <stdio.h>
#include <stdlib.h>
#include "python.h"
#include "VSPerf.h"
#include <hash_set>
#include <hash_map>
#include <string>
#include <strsafe.h>

using namespace std;

typedef void PyEval_SetProfileFunc(Py_tracefunc func, PyObject *obj);
typedef PyObject* PyDict_GetItemString(PyObject *dp, const char *key);

// function definitions so we can dynamically link to profiler APIs
// turns name into token
typedef PROFILE_COMMAND_STATUS (_stdcall *NameTokenFunc)(DWORD_PTR token, const wchar_t* name);
// reports line number of a function
typedef PROFILE_COMMAND_STATUS (_stdcall *SourceLineFunc)(DWORD_PTR functionToken, DWORD_PTR fileToken, unsigned int lineNumber);
// notify function entry
typedef void (__stdcall *EnterFunctionFunc)(DWORD_PTR FunctionToken, DWORD_PTR ModuleToken);
// notify function exit
typedef void (__stdcall *ExitFunctionFunc)(DWORD_PTR FunctionToken, DWORD_PTR ModuleToken);

class VsPyProf;

class VsPyProfThread : public PyObject {
	VsPyProf* _profiler;
	int _depth;
	static const int _skippedFrames = 1;
public:
	VsPyProfThread(VsPyProf* profiler);
	~VsPyProfThread();
	VsPyProf* GetProfiler();

	int Trace(PyFrameObject *frame, int what, PyObject *arg);
};

// Implements Python profiling.  Supports multiple Python versions (2.4 - 3.1) simultaneously.
// This code is always called w/ the GIL held (either from a ctypes call where we're a PyDll or
// from the runtime for our trace func).
class VsPyProf {
	friend class VsPyProfThread;

	HMODULE _pythonModule;
	PyEval_SetProfileFunc* _setProfileFunc;
	PyDict_GetItemString* _getItemStringFunc;
	
	hash_set<DWORD_PTR> _registeredObjects;
	hash_set<PyObject*> _referencedObjects;
	hash_map<DWORD_PTR, wstring> _registeredModules;
	
	// Python type objects
	PyObject* PyCode_Type;
	PyObject* PyStr_Type;
	PyObject* PyUni_Type;
	PyObject* PyCFunction_Type;
	PyObject* PyDict_Type;
	PyObject* PyTuple_Type;
	PyObject* PyType_Type;
	PyObject* PyFunction_Type;
	PyObject* PyModule_Type;
	PyObject* PyInstance_Type;

	// profiler APIs
	EnterFunctionFunc _enterFunction;
	ExitFunctionFunc _exitFunction;
	NameTokenFunc _nameToken;
	SourceLineFunc _sourceLine;

	VsPyProf(HMODULE pythonModule, int majorVersion, int minorVersion, EnterFunctionFunc enterFunction, ExitFunctionFunc exitFunction, NameTokenFunc nameToken, SourceLineFunc sourceLine, PyObject* pyCodeType, PyObject* pyStringType, PyObject* pyUnicodeType, PyEval_SetProfileFunc* setProfileFunc, PyObject* cfunctionType, PyDict_GetItemString* getItemStringFunc, PyObject* pyDictType, PyObject* pyTupleType, PyObject* pyTypeType, PyObject* pyFuncType, PyObject* pyModuleType, PyObject* pyInstType);

	// Extracts the function and module identifier from a user defined function
	bool GetUserToken(PyFrameObject *frame, DWORD_PTR& func, DWORD_PTR& module);	
	bool GetBuiltinToken(PyObject* codeObj, DWORD_PTR& func, DWORD_PTR& module);
	void GetModuleName(wstring module_name, wstring& finalName);
	wstring GetClassNameFromSelf(PyObject* self, PyObject *codeObj);
	wstring GetClassNameFromFrame(PyFrameObject* frameObj, PyObject *codeObj);

	void RegisterName(DWORD_PTR token, PyObject* name, wstring* moduleName = nullptr);
	bool GetName(PyObject* object, wstring& name);
	void GetNameAscii(PyObject* object, string& name);
	
	void ReferenceObject(PyObject* object) {
		object->ob_refcnt++;
		_referencedObjects.insert(object);
	}

	int MajorVersion, MinorVersion, _refCount;

public:
	// Creates a new instance of the PythonApi from the given DLL.  Returns null if the
	// version is unsupported or another error occurs.
	static VsPyProf* Create(HMODULE pythonModule);

	void PyEval_SetProfile(Py_tracefunc func, PyObject* object);
	VsPyProfThread* CreateThread() {
		return new VsPyProfThread(this);
	}

	void AddRef() {
		_refCount++;
	}

	void Release() {
		if(--_refCount == 0) {
			delete this;
		}
	}

	~VsPyProf() {
		// release all objects we hold onto...
		for(auto cur = _referencedObjects.begin(); cur != _referencedObjects.end(); cur++) {
			if(--(*cur)->ob_refcnt == 0) {
				(*cur)->ob_type->tp_dealloc(*cur);
			}
		}
	}

};


#endif