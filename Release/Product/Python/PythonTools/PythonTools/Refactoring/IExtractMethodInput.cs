/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Provides inputs/UI to the extract method refactoring.  Enables driving of the refactoring programmatically
    /// or via UI.
    /// </summary>
    interface IExtractMethodInput {
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
