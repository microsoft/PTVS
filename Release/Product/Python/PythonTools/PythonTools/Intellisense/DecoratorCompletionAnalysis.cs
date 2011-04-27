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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Intellisense {
    class DecoratorCompletionAnalysis : CompletionAnalysis {
        internal DecoratorCompletionAnalysis(string text, int pos, ITrackingSpan span, ITextBuffer textBuffer)
            : base(text, pos, span, textBuffer) {
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            // TODO: We should support more decorator types than just these 3, including the new property support of:
            // @property
            // def f(): pass
            // @f.setter
            //
            // which was added in 2.6.  Ideally we would display all callable objects in this list.
            return new CompletionSet(
                Text,
                Text,
                Span,
                new[] { 
                    PythonCompletion(glyphService, "classmethod", "Marks a function as a class method (first argument is the type object of the instance).", StandardGlyphGroup.GlyphGroupClass),
                    PythonCompletion(glyphService, "property", "Marks a function as a property whose value is returned without requiring paranthesis for a call.", StandardGlyphGroup.GlyphGroupClass),
                    PythonCompletion(glyphService, "staticmethod", "Marks a function as a static method which does not receive self.", StandardGlyphGroup.GlyphGroupClass),
                },
                new Completion[0]
            );
        }
    }
}
