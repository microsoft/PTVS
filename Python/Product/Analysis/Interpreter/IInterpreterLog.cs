using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    public interface IInterpreterLog {
        void Log(string msg);
    }
}
