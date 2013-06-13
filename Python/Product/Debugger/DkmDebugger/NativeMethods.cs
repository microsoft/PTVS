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

namespace Microsoft.PythonTools.DkmDebugger {
    internal static class NativeMethods {
        public const int MEM_COMMIT = 0x1000;
        public const int MEM_RESERVE = 0x2000;
        public const int MEM_RELEASE = 0x8000;

        public const int PAGE_READWRITE = 0x04;
    }

    public enum EXCEPTION_CODE : uint {
        EXCEPTION_ACCESS_VIOLATION = 0xc0000005,
        EXCEPTION_ARRAY_BOUNDS_EXCEEDED = 0xc000008c,
        EXCEPTION_BREAKPOINT = 0x80000003,
        EXCEPTION_DATATYPE_MISALIGNMENT = 0x80000002,
        EXCEPTION_FLT_DENORMAL_OPERAND = 0xc000008d,
        EXCEPTION_FLT_DIVIDE_BY_ZERO = 0xc000008e,
        EXCEPTION_FLT_INEXACT_RESULT = 0xc000008f,
        EXCEPTION_FLT_INVALID_OPERATION = 0xc0000090,
        EXCEPTION_FLT_OVERFLOW = 0xc0000091,
        EXCEPTION_FLT_STACK_CHECK = 0xc0000092,
        EXCEPTION_FLT_UNDERFLOW = 0xc0000093,
        EXCEPTION_GUARD_PAGE = 0x80000001,
        EXCEPTION_ILLEGAL_INSTRUCTION = 0xc000001d,
        EXCEPTION_IN_PAGE_ERROR = 0xc0000006,
        EXCEPTION_INT_DIVIDE_BY_ZERO = 0xc0000094,
        EXCEPTION_INT_OVERFLOW = 0xc0000095,
        EXCEPTION_INVALID_DISPOSITION = 0xc0000026,
        EXCEPTION_INVALID_HANDLE = 0xc0000008,
        EXCEPTION_NONCONTINUABLE_EXCEPTION = 0xc0000025,
        EXCEPTION_PRIV_INSTRUCTION = 0xc0000096,
        EXCEPTION_SINGLE_STEP = 0x80000004,
        EXCEPTION_STACK_OVERFLOW = 0xc00000fd,
        STATUS_UNWIND_CONSOLIDATE = 0x80000029,
    }
}
