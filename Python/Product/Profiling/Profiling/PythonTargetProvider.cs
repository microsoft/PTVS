namespace Microsoft.PythonTools.Profiling
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EnvDTE;
    using Microsoft.DiagnosticsHub;
    using Microsoft.DiagnosticsHub.Diagnostics;
    using Microsoft.DiagnosticsHub.Targets;
    using Microsoft.Internal.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.Threading;
    using static Microsoft.VisualStudio.VSConstants;


    /// <summary>
    /// The processes target provider.
    /// </summary>
    [Export(typeof(ITargetProvider))]
    public class PythonTargetProvider : ITargetProvider, ITargetProviderAsync {


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
            return new Guid("bb47b6ec-a8f6-48e9-a7f6-e38795a609d5");
            // return Guids.RunningWindowsStoreAppTargetProviderGuid;
        }

        /// <inheritdoc />
        public int GetOrder() {
            return (int)0x9000;
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
            var targetView = new ProfilingTargetView(PythonProfilingPackage.Instance);
            var pythonProfilingPackage = PythonProfilingPackage.Instance;
            var dialog = new LaunchProfiling(pythonProfilingPackage, targetView);

            var res = dialog.ShowModal() ?? false;

            if (res && targetView.IsValid) {
                var target = targetView.GetTarget();
                if (target != null) {
                    var targetInfo = getTargetInfo(target, pythonProfilingPackage);
                    return new PythonTarget[] { };
                }
            }

            return new PythonTarget[] { };

        }

        private object getTargetInfo(ProfilingTarget target, PythonProfilingPackage pythonProfilingPackage) {
            try {
                var joinableTaskFactory = pythonProfilingPackage.JoinableTaskFactory;
                joinableTaskFactory.Run(async () => {
                    await joinableTaskFactory.SwitchToMainThreadAsync();

                    var name = target.GetProfilingName(pythonProfilingPackage, out var save);
                    var explorer = await pythonProfilingPackage.ShowPerformanceExplorerAsync();
                    var session = explorer.Sessions.AddTarget(target, name, save);

                    pythonProfilingPackage.StartProfiling(target, session);

                });
            } catch (Exception ex) {
                // Log or handle the exception
                Debug.Fail($"Error in ProfileTarget: {ex.Message}");
                throw;
            }
            return null;
        }

    }
}
