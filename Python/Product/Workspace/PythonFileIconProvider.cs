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

using System;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Workspace;

namespace Microsoft.PythonTools.Workspace {
    [Export(typeof(IVsFileIconProvider))]
    class PythonFileIconProvider : IVsFileIconProvider {
        public AsyncEvent<FileIconsChangedEvent> OnFileIconsChanged { get; set; }

        public bool GetIconForFile(string fullPath, out ImageMoniker imageMoniker, out int priority) {
            if (!string.IsNullOrEmpty(fullPath)) {
                var ext = Path.GetExtension(fullPath);
                if (PythonConstants.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    PythonConstants.WindowsFileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)) {
                    imageMoniker = KnownMonikers.PYFileNode;
                    priority = 1000;
                    return true;
                }
            }

            imageMoniker = default(ImageMoniker);
            priority = 0;

            return false;
        }
    }
}
