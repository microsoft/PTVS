using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Microsoft.PythonTools.Django {
    internal static class TemplateContentType {
        public const string ContentTypeName = "DjangoTemplate";

        [Export, Name(ContentTypeName), BaseDefinition("code")]
        internal static ContentTypeDefinition ContentTypeDefinition = null; // set via MEF
    }
}
