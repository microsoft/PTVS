using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUtilities.Python {
    internal static class EventTaskSources {
        public static class VsProjectAnalyzer {
            public static readonly EventTaskSource<Microsoft.PythonTools.Intellisense.VsProjectAnalyzer> AnalysisStarted =
                new EventTaskSource<Microsoft.PythonTools.Intellisense.VsProjectAnalyzer>(
                    (o, e) => o.AnalysisStarted += e,
                    (o, e) => o.AnalysisStarted -= e);
        }
    }
}
