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

using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.Project {
    public interface IPublishProject {
        /// <summary>
        /// Gets the list of files which need to be published.
        /// </summary>
        IList<IPublishFile> Files {
            get;
        }

        /// <summary>
        /// Gets the root directory of the project.
        /// </summary>
        string ProjectDir {
            get;
        }

        /// <summary>
        /// Gets or sets the progress of the publishing.
        /// </summary>
        int Progress {
            get;
            set;
        }
    }
}
