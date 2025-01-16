namespace Microsoft.PythonTools.Profiling {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.DiagnosticsHub;
    using Microsoft.DiagnosticsHub.Targets;


    /// <summary>
    /// The Python target provider.
    /// </summary>
    [Export(typeof(ITargetProvider))]
    public class PythonTargetProvider : ITargetProvider, ITargetProviderAsync {

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
