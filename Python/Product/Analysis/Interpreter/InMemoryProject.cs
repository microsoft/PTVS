using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Represents a project which is loaded into memory.
    /// 
    /// Consists of a filename and an arbitrary set of properties stored in a dictionary.
    /// </summary>
    public sealed class InMemoryProject {
        private readonly string _file;
        private readonly IReadOnlyDictionary<string, object> _props;

        public InMemoryProject(string file, IReadOnlyDictionary<string, object> properties) {
            _file = file;
            _props = properties;
        }

        public string FullPath => _file;

        public IReadOnlyDictionary<string, object> Properties => _props;
    }
}
