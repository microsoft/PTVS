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

#if !DEV12_OR_LATER

using System.Linq;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class ProjectBlockCompletionContext : ProjectBlockCompletionContextBase {
        public ProjectBlockCompletionContext(DjangoAnalyzer analyzer, ITextBuffer buffer)
            : base(analyzer, buffer, TemplateProjectionBuffer.GetFilePath(buffer)) {
            TemplateProjectionBuffer projBuffer;
            if (buffer.Properties.TryGetProperty(typeof(TemplateProjectionBuffer), out projBuffer)) {
                foreach (var span in projBuffer.Spans) {
                    if (span.Block != null) {
                        foreach (var variable in span.Block.GetVariables()) {
                            AddLoopVariable(variable);
                        }
                    }
                }
            }
        }
    }
}

#endif