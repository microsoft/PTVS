using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Intellisense {
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    sealed class FileExtensionAttribute  : Attribute {
        private readonly string _extension;

        public FileExtensionAttribute(string extension) {
            _extension = extension;
        }

        public string Extension => _extension;
    }
}
