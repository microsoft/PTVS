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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;

namespace CanopyInterpreter {
    /// <summary>
    /// Provides constants used to identify interpreters that are detected from
    /// Canopy's registry settings. These values are only used by
    /// CanopyInterpreterFactory.
    /// </summary>
    static class CanopyInterpreterFactoryConstants {
#error Update these GUIDs and delete this #error.
        /// <summary>
        /// The GUID that identifies the base 32-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid BaseGuid32 = new Guid("{PLACE-NEW-GUID-HERE}");
        /// <summary>
        /// The GUID that identifies the base 64-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid BaseGuid64 = new Guid("{PLACE-NEW-GUID-HERE}");

        /// <summary>
        /// The GUID that identifies the user 32-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid UserGuid32 = new Guid("{PLACE-NEW-GUID-HERE}");
        /// <summary>
        /// The GUID that identifies the user 64-bit environment. This will be
        /// used to store user preferences and references for the factory.
        /// </summary>
        public static readonly Guid UserGuid64 = new Guid("{PLACE-NEW-GUID-HERE}");

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
