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
    public sealed class PublishProjectOptions {
        private readonly IPublishFile[] _additionalFiles;
        private readonly string _destination;
        public static readonly PublishProjectOptions Default = new PublishProjectOptions(new IPublishFile[0]);

        public PublishProjectOptions(IPublishFile[] additionalFiles = null, string destinationUrl = null) {
            _additionalFiles = additionalFiles ?? Default._additionalFiles;
            _destination = destinationUrl;
        }

        public IList<IPublishFile> AdditionalFiles {
            get {
                return _additionalFiles;
            }
        }

        /// <summary>
        /// Gets an URL which overrides the project publish settings or returns null if no override is specified.
        /// </summary>
        public string DestinationUrl {
            get {
                return _destination;
            }
        }
    }
}
