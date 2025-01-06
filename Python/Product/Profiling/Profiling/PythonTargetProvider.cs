namespace Microsoft.PythonTools.Profiling
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.DiagnosticsHub;
    using Microsoft.DiagnosticsHub.Diagnostics;
    using Microsoft.DiagnosticsHub.Targets;
    using Microsoft.Internal.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.Threading;


    /// <summary>
    /// The processes target provider.
    /// </summary>
    [Export(typeof(ITargetProvider))]
    class PythonTargetProvider : ITargetProvider, ITargetProviderAsync {


        /// <summary>
        /// Gets or sets the hub service provider.
        /// </summary>
        [Import(typeof(IHubServiceProvider))]
        public IHubServiceProvider HubServiceProvider { get; set; } // This is populated during imports. Failure to do so will cause a MEF exception

        public PythonTargetProvider() {
            Debug.WriteLine("PythonTargetProvider: MEF component initialized.");

        }


        public string GetName() {
            return "Python Profiler";
        }

        public string GetDescription() {
            return "Provides profiling support for Python projects.";
        }

        public string GetIconPath() {
            return string.Empty;
        }

        public Guid GetId() {
            return Guids.RunningWindowsStoreAppTargetProviderGuid;
        }

        /// <inheritdoc />
        public int GetOrder() {
            return (int)DefaultTargetProviderConstants.Order.ProcessTarget;
        }

        /// <inheritdoc />
        public string GetMenuName() {
            return "Python Profiler...";
        }

        /// <inheritdoc />
        public IEnumerable<ITarget> GetTargets(IDictionary<string, object> properties, bool chooseTarget) {
            // This shouldn't be used - call GetTargetsAsync instead
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ITarget>> GetTargetsAsync(IDictionary<string, object> properties, bool chooseTarget, CancellationToken cancellationToken) {

            return Process.GetProcesses()
            .Where(p => p.ProcessName.Equals("python", StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.Equals("pythonw", StringComparison.OrdinalIgnoreCase))
            .Cast<ITarget>();

        }

        private IEnumerable<ITarget> GetTargets() {
            throw new NotImplementedException();
        }

        internal static IEnumerable<Process> GetProcesses(int currentProcessId) {
            // Get the processes on this machine
            return Process.GetProcesses()
                .Where((p) => p.Id != currentProcessId);
        }
    }
}
