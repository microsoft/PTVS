using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false)]

    public sealed class AnalysisExtensionNameAttribute : Attribute {
        private readonly string _name;

        public AnalysisExtensionNameAttribute(string name) {
            _name = name;
        }

        public string Name => _name;
    }

}
