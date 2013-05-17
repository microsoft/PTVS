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

// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the PYDEBUGATTACH
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// PYDEBUGATTACH_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#define PYDEBUGATTACH_API __declspec(dllexport) 
#ifdef PYDEBUGATTACH_EXPORTS
#else
#define PYDEBUGATTACH_API __declspec(dllimport)
#endif

