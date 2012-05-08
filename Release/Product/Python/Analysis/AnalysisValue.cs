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

using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Base class for all analysis values.  Exposes the public API of the analysis engine.
    /// 
    /// New in 1.5.
    /// </summary>
    public class AnalysisValue {
        internal AnalysisValue() {
        }

        /// <summary>
        /// Gets a list of locations where this value is defined.
        /// </summary>
        public virtual IEnumerable<LocationInfo> Locations {
            get { return LocationInfo.Empty; }
        }

        /// <summary>
        /// Gets the constant value that this object represents, if it's a constant.
        /// 
        /// Returns Type.Missing if the value is not constant (because it returns null
        /// if the type is None).
        /// </summary>
        /// <returns></returns>
        public virtual object GetConstantValue() {
            return Type.Missing;
        }
    }
}
