// Visual Studio Shared Project
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

namespace Microsoft.VisualStudioTools.Project {
    internal partial class ProjectNode {
        public event EventHandler<ProjectPropertyChangedArgs> OnProjectPropertyChanged;

        protected virtual void RaiseProjectPropertyChanged(string propertyName, string oldValue, string newValue) {
            var onPropChanged = OnProjectPropertyChanged;
            if (onPropChanged != null) {
                onPropChanged(this, new ProjectPropertyChangedArgs(propertyName, oldValue, newValue));
            }
        }
    }
}
