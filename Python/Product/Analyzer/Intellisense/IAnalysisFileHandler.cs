using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis {
    public interface IAnalysisFileHandler {
        IExternalProjectEntry AnalyzeFile(PythonAnalyzer analyzer, string path);
    }
}
