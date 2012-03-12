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

using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Project {
    [ContentType(PythonCoreConstants.ContentType)]
    [Export(typeof(IEncodingDetector))]
    [Order(Before = "XmlEncodingDetector")]
    [Name("PythonEncodingDetector")]
    class PythonEncodingDetector : IEncodingDetector {
        public Encoding GetStreamEncoding(Stream stream) {
            var res = Parser.GetEncodingFromStream(stream) ?? Parser.DefaultEncodingNoFallback;
            if (res == Parser.DefaultEncoding) {
                // return a version of the fallback buffer that doesn't throw exceptions, VS will detect the failure, and inform
                // the user of the problem.
                return Parser.DefaultEncodingNoFallback;
            }
            return res;
        }
    }
}
