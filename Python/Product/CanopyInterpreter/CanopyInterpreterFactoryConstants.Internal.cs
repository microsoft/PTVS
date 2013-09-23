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

namespace CanopyInterpreter {
    /// <summary>
    /// Provides constants used to identify interpreters that are detected from
    /// Canopy's registry settings.
    /// </summary>
    static class CanopyInterpreterFactoryConstants {
        /// <summary>
        /// The GUID that identifies the base 32-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid BaseGuid32 = new Guid("{DBDF625C-9CEA-4B63-BDDD-3B02551B7AD2}");
        /// <summary>
        /// The GUID that identifies the base 64-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid BaseGuid64 = new Guid("{935325EE-65B9-4F54-BEDB-0FB02DC8C9BE}");

        /// <summary>
        /// The GUID that identifies the user 32-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid UserGuid32 = new Guid("{3A259895-9005-42A1-8100-71C47667EB29}");
        /// <summary>
        /// The GUID that identifies the user 64-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid UserGuid64 = new Guid("{6A73168F-9101-4E5A-B8CB-E6763579096D}");

        /// <summary>
        /// The name of the console executable to use.
        /// <see cref="CanopyInterpreterFactory" /> will resolve this to a full
        /// path.
        /// </summary>
        public const string ConsoleExecutable = "python.exe";
        /// <summary>
        /// The name of the windowed executable to use.
        /// <see cref="CanopyInterpreterFactory" /> will resolve this to a full
        /// path.
        /// </summary>
        public const string WindowsExecutable = "pythonw.exe";
        /// <summary>
        /// The name of the library folder to use.
        /// <see cref="CanopyInterpreterFactory" /> will resolve this to a full
        /// path.
        /// </summary>
        public const string LibrarySubPath = "lib";
        /// <summary>
        /// The name of the environment variable that is added to sys.path by
        /// the interpreter. Users' search paths are stored in this variable
        /// when running their projects.
        /// </summary>
        public const string PathEnvironmentVariableName = "PYTHONPATH";
    }
}
