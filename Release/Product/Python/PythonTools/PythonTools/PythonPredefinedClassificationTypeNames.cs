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


namespace Microsoft.PythonTools {
    public static class PythonPredefinedClassificationTypeNames {
        /// <summary>
        /// Open grouping classification.  Used for (, [, etc...  A subtype of the pre-defined
        /// operator grouping.
        /// </summary>
        public const string OpenGrouping = "Python open grouping";
        /// <summary>
        /// Closed grouping classification.  Used for ), ], etc...  A subtype of the pre-defined
        /// operator grouping.
        /// </summary>
        public const string CloseGrouping = "Python close grouping";

        /// <summary>
        /// Classification used for comma characters when used outside of a literal, comment, etc...
        /// </summary>
        public const string Comma = "Python comma";

        /// <summary>
        /// Classification used for . characters when used outside of a literal, comment, etc...
        /// </summary>
        public const string Dot = "Python dot";
    }
}
