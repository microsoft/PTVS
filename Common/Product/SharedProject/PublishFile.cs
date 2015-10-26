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


namespace Microsoft.VisualStudioTools.Project {
    class PublishFile : IPublishFile {
        private readonly string _filename, _destFile;

        public PublishFile(string filename, string destFile) {
            _filename = filename;
            _destFile = destFile;
        }

        #region IPublishFile Members

        public string SourceFile {
            get { return _filename; }
        }

        public string DestinationFile {
            get { return _destFile; }
        }

        #endregion
    }
}
