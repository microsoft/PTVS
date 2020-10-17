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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.PythonTools {
    static class GuidList {
        public const string guidPythonProjectString = "A7656004-6CDD-4FEE-8132-483C642CA453";
        public const string guidPythonLanguageService = "B79D45B5-BED9-40AC-A365-DA6008CA27AA";
        public const string guidLoadedProjectInterpreterFactoryProviderString = "B8989820-F55D-4272-92F9-AA8D5954BA0A";
        public const string guidPythonInteractiveWindow = "99C69BDA-EDD8-4732-B827-957A97E92142";
        
        public const string guidPythonToolsCmdSetString = "44AFF35A-AA6E-46AA-BC75-D4AC9F7E7655";
        public static readonly Guid guidPythonToolsCmdSet = new Guid(guidPythonToolsCmdSetString);

        public static readonly Guid guidPythonProjectGuid = new Guid(guidPythonProjectString);
        public static readonly Guid guidPythonLanguageServiceGuid = new Guid(guidPythonLanguageService);
        public static readonly Guid guidPythonInteractiveWindowGuid = new Guid(guidPythonInteractiveWindow);
        public static readonly Guid guidPythonToolbarUIContext = new Guid("D03DDFA9-C339-468E-A0DC-CF917371ED57");
    };
}
