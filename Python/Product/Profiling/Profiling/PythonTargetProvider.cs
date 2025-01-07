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
        public IHubServiceProvider HubServiceProvider { get; set; } = null!; // This is populated during imports. Failure to do so will cause a MEF exception

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

            // Copied from ExeTargetProvider, to be replaced with Python-specific logic
            IRecentOptionsService settings = this.HubServiceProvider.GetService<IRecentOptionsService>();
            var loaded = await settings.LoadSettingsAsync<ExeDialogSettingsConfig>(ExeDialogSettingsConfig.StreamName);
            var viewModel = new ExeTargetPropertiesViewModel(this.HubServiceProvider);
            viewModel.FromConfig(loaded); // ignore return value
            var dialog = new ExeTargetPropertiesDialog(viewModel);

            if (chooseTarget) {
                int result = WindowHelper.ShowModal(dialog);
                if (result == DialogResult.OK) {
                    // persist config when user presses Ok
                    settings.PersistSettings(ExeDialogSettingsConfig.StreamName, viewModel.ToConfig());
                    return new List<ITarget>() { viewModel.GetTarget() };
                }

                return Enumerable.Empty<ITarget>();
            } else {
                return new List<ITarget>() { viewModel.GetTarget() };
            }

        }

        private IEnumerable<ITarget> GetTargets() {
            throw new NotImplementedException();
        }

    }
}
