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

// VsPyProf.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "VsPyProf.h"
#include "PythonApi.h"
#include <Windows.h>

int TraceFunction(PyObject *obj, PyFrameObject *frame, int what, PyObject *arg) {
    return ((VsPyProfThread*)obj)->Trace(frame, what, arg);
}

extern "C" VSPYPROF_API VsPyProf* CreateProfiler(HMODULE module) 
{
    return VsPyProf::Create(module);
}

// This is an example of an exported function.
extern "C" VSPYPROF_API VsPyProfThread* InitProfiler(VsPyProf* profiler)
{    
    auto thread = profiler->CreateThread();

    if (thread != nullptr) {
        thread->GetProfiler()->PyEval_SetProfile(&TraceFunction, thread);
    }

    return thread;
}

extern "C" VSPYPROF_API void CloseProfiler(VsPyProf* profiler) {
    profiler->Release();
}

extern "C" VSPYPROF_API void CloseThread(VsPyProfThread* thread) {
    thread->GetProfiler()->PyEval_SetProfile(nullptr, nullptr);    
    delete thread;
}

// used for compat w/ Python 2.4 where we don't have ctypes.
extern "C" VSPYPROF_API void initvspyprof(void) {
}

