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
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Provides a common interface for our elements of the preview list.  Currently we break this into a top-level
    /// tree node for each file (FilePreviewItem) and a leaf node for each individual rename location (LocationPreviewItem).
    /// </summary>
    interface IPreviewItem {
        ushort Glyph {
            get;
        }

        IntPtr ImageList {
            get;
        }

        bool IsExpandable {
            get;
        }

        PreviewList Children {
            get;
        }

        string GetText(VSTREETEXTOPTIONS options);

        _VSTREESTATECHANGEREFRESH ToggleState();

        __PREVIEWCHANGESITEMCHECKSTATE CheckState {
            get;
        }

        void DisplayPreview(IVsTextView view);

        void Close(VSTREECLOSEACTIONS vSTREECLOSEACTIONS);

        Span? Selection {
            get;
        }
    }
}
