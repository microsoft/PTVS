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

namespace Microsoft.PythonTools.Refactoring
{
    /// <summary>
    /// Provides inputs/UI to the extract method refactoring.  Enables driving of the refactoring programmatically
    /// or via UI.
    /// </summary>
    interface IExtractMethodInput
    {
        /// <summary>
        /// Returns true if the user wants us to expand the selection to cover an entire expression.
        /// </summary>
        bool ShouldExpandSelection();

        /// <summary>
        /// Returns null or an ExtractMethodRequest instance which specifies the options for extracting the method.
        /// </summary>
        /// <param name="creator"></param>
        /// <returns></returns>
        ExtractMethodRequest GetExtractionInfo(ExtractedMethodCreator creator);

        /// <summary>
        /// Reports that we cannot extract the method and provides a specific reason why the extraction failed.
        /// </summary>
        void CannotExtract(string reason);
    }
}
