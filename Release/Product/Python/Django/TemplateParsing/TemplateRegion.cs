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

namespace Microsoft.PythonTools.Django.TemplateParsing {
    internal class TemplateRegion {
        public readonly string Text;
        public readonly TemplateTokenKind Kind;
        public readonly DjangoBlock Block;
        public readonly int Start;

        public TemplateRegion(string text, TemplateTokenKind kind, DjangoBlock block, int start) {
            Text = text;
            Kind = kind;
            Start = start;
            Block = block;
        }
    }
}
