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

namespace Microsoft.CookiecutterTools.Model
{
    class ReplacedFile
    {
        public string OriginalFilePath { get; }
        public string BackupFilePath { get; }
        public ReplacedFile(string original, string backup)
        {
            OriginalFilePath = original;
            BackupFilePath = backup;
        }
    }

    class CreateFilesOperationResult
    {
        public string[] FoldersCreated { get; }
        public string[] FilesCreated { get; }
        public ReplacedFile[] FilesReplaced { get; }
        public CreateFilesOperationResult(string[] foldersCreated, string[] filesCreated, ReplacedFile[] filesReplaced)
        {
            FoldersCreated = foldersCreated;
            FilesCreated = filesCreated;
            FilesReplaced = filesReplaced;
        }
    }
}
