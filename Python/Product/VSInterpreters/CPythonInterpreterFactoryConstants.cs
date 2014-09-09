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

using System;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides constants used to identify interpreters that are detected from
    /// the CPython registry settings.
    /// 
    /// This class used by Microsoft.PythonTools.dll to register the
    /// interpreters.
    /// </summary>
    public static class CPythonInterpreterFactoryConstants {
        public const string Id32 = "{2AF0F10D-7135-4994-9156-5D01C9C11B7E}";
        public const string Id64 = "{9A7A9026-48C1-4688-9D5D-E5699D47D074}";

        public static readonly Guid Guid32 = new Guid(Id32);
        public static readonly Guid Guid64 = new Guid(Id64);

        public const string ConsoleExecutable = "python.exe";
        public const string WindowsExecutable = "pythonw.exe";
        public const string LibrarySubPath = "lib";
        public const string PathEnvironmentVariableName = "PYTHONPATH";

        public const string Description32 = "Python";
        public const string Description64 = "Python 64-bit";
    }
}
