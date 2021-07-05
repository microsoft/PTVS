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

namespace Microsoft.CookiecutterTools.Resources {
    static class ImageMonikers {
        public static readonly Guid Guid = new Guid("{50EDF200-5C99-4968-ABC0-CF1A2C490F00}");
        public static readonly ImageMoniker Cancel = new ImageMoniker { Guid = Guid, Id = 1 };
        public static readonly ImageMoniker Cookiecutter = new ImageMoniker { Guid = Guid, Id = 2 };
        public static readonly ImageMoniker CookiecutterTemplate = new ImageMoniker { Guid = Guid, Id = 3 };
        public static readonly ImageMoniker CookiecutterTemplateOK = new ImageMoniker { Guid = Guid, Id = 4 };
        public static readonly ImageMoniker CookiecutterTemplateUpdate = new ImageMoniker { Guid = Guid, Id = 5 };
        public static readonly ImageMoniker CookiecutterTemplateWarning = new ImageMoniker { Guid = Guid, Id = 6 };
        public static readonly ImageMoniker Download = new ImageMoniker { Guid = Guid, Id = 7 };
        public static readonly ImageMoniker NewCookiecutter = new ImageMoniker { Guid = Guid, Id = 8 };
    }
}
