using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Debugger
{
    public static class DTEDebuggerExtensions
    {
        /// <summary> 
        /// Forces debugger to refresh its variable views (Locals, Autos etc) by re-querying the debug engine. 
        /// </summary> 
        /// <param name="debugger"></param> 
        public static void RefreshVariableViews(this EnvDTE.Debugger debugger) {
            AD7Engine engine = AD7Engine.GetEngines().SingleOrDefault(target => target.Process != null && target.Process.Id == debugger.CurrentProcess.ProcessID);
            if (engine != null) {
                engine.RefreshThreadFrames(debugger.CurrentThread.ID);
            }

            var vsDebugger = (IDebugRefreshNotification140)ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger));
            // Passing fCallstackFormattingAffected = TRUE to OnExpressionEvaluationRefreshRequested to force refresh
            vsDebugger.OnExpressionEvaluationRefreshRequested(1); 
        }
    }
}
