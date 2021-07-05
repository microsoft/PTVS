// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Environments {
    sealed class AddCondaEnvironmentView : EnvironmentViewBase {
        private readonly ICondaEnvironmentManager _condaMgr;
        private readonly Timer _updatePreviewTimer;
        private CancellationTokenSource _updatePreviewCancelTokenSource;
        private bool _suppressPreview;

        public AddCondaEnvironmentView(
            IServiceProvider serviceProvider,
            ProjectView[] projects,
            ProjectView selectedProject
        ) : base(serviceProvider, projects, selectedProject) {
            _condaMgr = CondaEnvironmentManager.Create(Site);
            _updatePreviewTimer = new Timer(UpdatePreviewTimerCallback);

            IsCondaMissing = _condaMgr == null;
            PageName = Strings.AddCondaEnvironmentTabHeader;
            AcceptCaption = Strings.AddEnvironmentCreateButton;
            AcceptAutomationName = Strings.AddEnvironmentCreateButtonAutomationName;
            IsAcceptEnabled = !IsCondaMissing;

            SetAsCurrent = SelectedProject != null;
            SetAsDefault = false;
            ViewInEnvironmentWindow = false;

            ResetProjectDependentProperties();
        }

        public static readonly DependencyProperty SelectedEnvFilePathProperty =
            DependencyProperty.Register(nameof(SelectedEnvFilePath), typeof(string), typeof(AddCondaEnvironmentView), new PropertyMetadata("", EnvParameter_Changed));

        public static readonly DependencyProperty PackagesProperty =
            DependencyProperty.Register(nameof(Packages), typeof(string), typeof(AddCondaEnvironmentView), new PropertyMetadata("", EnvParameter_Changed));

        public static readonly DependencyProperty IsEnvFileProperty =
            DependencyProperty.Register(nameof(IsEnvFile), typeof(bool), typeof(AddCondaEnvironmentView), new PropertyMetadata(false, EnvParameter_Changed));

        public static readonly DependencyProperty IsPackagesProperty =
            DependencyProperty.Register(nameof(IsPackages), typeof(bool), typeof(AddCondaEnvironmentView), new PropertyMetadata(true, EnvParameter_Changed));

        public static readonly DependencyProperty EnvNameProperty =
            DependencyProperty.Register(nameof(EnvName), typeof(string), typeof(AddCondaEnvironmentView), new PropertyMetadata("", EnvParameter_Changed));

        public static readonly DependencyProperty CondaPreviewProperty =
            DependencyProperty.Register(nameof(CondaPreview), typeof(CondaEnvironmentPreview), typeof(AddCondaEnvironmentView));

        public bool IsCondaMissing { get; }

        public IEnumerable<PackageSpec> PackagesSpecs {
            get {
                return (String.IsNullOrWhiteSpace(Packages) ? "Python" : Packages)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => new PackageSpec(null, fullSpec: p))
                    .ToArray();
            }
        }

        public bool IsEnvFile {
            get { return (bool)GetValue(IsEnvFileProperty); }
            set { SetValue(IsEnvFileProperty, value); }
        }

        public bool IsPackages {
            get { return (bool)GetValue(IsPackagesProperty); }
            set { SetValue(IsPackagesProperty, value); }
        }

        public string EnvName {
            get { return (string)GetValue(EnvNameProperty); }
            set { SetValue(EnvNameProperty, value); }
        }

        public string SelectedEnvFilePath {
            get { return (string)GetValue(SelectedEnvFilePathProperty); }
            set { SetValue(SelectedEnvFilePathProperty, value); }
        }

        public string Packages {
            get { return (string)GetValue(PackagesProperty); }
            set { SetValue(PackagesProperty, value); }
        }

        public CondaEnvironmentPreview CondaPreview {
            get { return (CondaEnvironmentPreview)GetValue(CondaPreviewProperty); }
            set { SetValue(CondaPreviewProperty, value); }
        }

        private static void EnvParameter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AddCondaEnvironmentView)d).EnvParameterChanged();
        }

        private static bool CondaEnvExists(IInterpreterRegistryService interpreterService, string prefixName) {
            // Known shortcoming:
            // Interpreter service only tracks conda envs that have Python in them,
            // so our check here will report no conflicts for conda envs without Python.
            return interpreterService.Configurations.Where(c => HasPrefixName(c, prefixName)).Any();
        }

        private static bool HasPrefixName(InterpreterConfiguration config, string name) {
            var current = PathUtils.GetFileOrDirectoryName(config.GetPrefixPath());
            return string.CompareOrdinal(current, name) == 0;
        }

        private void EnvParameterChanged() {
            var validFolder = !CondaEnvExists(RegistryService, EnvName);

            // For now, we enable but prompt when they click accept
            //if (IsEnvFile) {
            //    IsAcceptEnabled = validFolder && File.Exists(SelectedEnvFilePath);
            //} else {
            //    IsAcceptEnabled = validFolder;
            //}

            if (string.IsNullOrEmpty(EnvName.Trim())) {
                SetError(nameof(EnvName), Strings.AddCondaEnvironmentNameEmpty);
            } else if (!validFolder) {
                SetError(nameof(EnvName), Strings.AddCondaEnvironmentNameInvalid.FormatUI(EnvName ?? string.Empty));
            } else {
                ClearErrors(nameof(EnvName));
            }

            if (IsEnvFile && !File.Exists(SelectedEnvFilePath)) {
                SetError(nameof(SelectedEnvFilePath), Strings.AddCondaEnvironmentFileInvalid.FormatUI(SelectedEnvFilePath ?? string.Empty));
            } else {
                ClearErrors(nameof(SelectedEnvFilePath));
            }

            TriggerDelayedPreview();
        }

        protected override void ResetProjectDependentProperties() {
            _suppressPreview = true;

            SetAsCurrent = SetAsCurrent && SelectedProject != null;
            EnvName = GetDefaultEnvName();
            Packages = string.Empty;
            SelectedEnvFilePath = SelectedProject?.EnvironmentYmlPath;
            if (!string.IsNullOrEmpty(SelectedEnvFilePath)) {
                IsPackages = false;
                IsEnvFile = true;
            } else {
                IsPackages = true;
                IsEnvFile = false;
            }

            CondaPreview = new CondaEnvironmentPreview();

            _suppressPreview = false;

            TriggerDelayedPreview();
        }

        private string GetDefaultEnvName() {
            var existingName = SelectedProject?.MissingCondaEnvName;
            if (!string.IsNullOrEmpty(existingName) && !CondaEnvExists(RegistryService, existingName)) {
                return existingName;
            } else {
                string envName = "env";
                for (int i = 1; CondaEnvExists(RegistryService, envName); ++i) {
                    envName = "env" + i.ToString();
                }
                return envName;
            }
        }

        public override async Task ApplyAsync() {
            if (_condaMgr == null) {
                return;
            }

            var operation = new AddCondaEnvironmentOperation(
                Site,
                _condaMgr,
                SelectedProject?.Node,
                SelectedProject?.Workspace,
                EnvName,
                IsEnvFile ? SelectedEnvFilePath : null,
                IsPackages ? PackagesSpecs.ToList() : Enumerable.Empty<PackageSpec>().ToList(),
                SetAsCurrent,
                SetAsDefault,
                ViewInEnvironmentWindow
            );

            await operation.RunAsync();
        }

        private void TriggerDelayedPreview() {
            if (_suppressPreview) {
                return;
            }

            try {
                _updatePreviewTimer.Change(500, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void UpdatePreviewTimerCallback(object state) {
            Dispatcher.Invoke(() => {
                UpdatePreview();
            });
        }

        private void UpdatePreview() {
            if (_suppressPreview) {
                return;
            }

            if (_condaMgr == null) {
                return;
            }

            if (IsEnvFile && IsPackages) {
                // Temporary state while switching from one combo box to the other
                // We'll get called again with only one of them set to true
                return;
            }

            _updatePreviewCancelTokenSource?.Cancel();
            _updatePreviewCancelTokenSource = new CancellationTokenSource();

            // Clear the preview, display a progress icon/message
            var p = new CondaEnvironmentPreview();
            p.Progress.IsProgressDisplayed = true;
            CondaPreview = p;

            var envName = EnvName ?? string.Empty;
            if (IsEnvFile) {
                var envFilePath = SelectedEnvFilePath ?? string.Empty;
                Task.Run(() => UpdatePreviewAsync(envName, envFilePath, _updatePreviewCancelTokenSource.Token)).HandleAllExceptions(Site, typeof(AddCondaEnvironmentView)).DoNotWait();
            } else if (IsPackages) {
                var specs = PackagesSpecs.ToArray();
                Task.Run(() => UpdatePreviewAsync(envName, specs, _updatePreviewCancelTokenSource.Token)).HandleAllExceptions(Site, typeof(AddCondaEnvironmentView)).DoNotWait();
            }
        }

        private async Task UpdatePreviewAsync(string envName, string envFilePath, CancellationToken ct) {
            try {
                ct.ThrowIfCancellationRequested();
                string result;
                try {
                    result = File.ReadAllText(envFilePath);
                } catch (UnauthorizedAccessException ex) {
                    result = ex.Message;
                } catch (ArgumentException ex) {
                    result = ex.Message;
                } catch (FileNotFoundException ex) {
                    result = ex.Message;
                } catch (IOException ex) {
                    result = ex.Message;
                }
                ct.ThrowIfCancellationRequested();
                Dispatcher.Invoke(() => {
                    var p = new CondaEnvironmentPreview();
                    p.Progress.IsProgressDisplayed = false;
                    p.IsEnvFile = result != null;
                    p.IsNoEnvFile = result == null;
                    p.EnvFileContents = result ?? "";
                    CondaPreview = p;
                });
            } catch (OperationCanceledException) {
            }
        }

        private async Task UpdatePreviewAsync(string envName, PackageSpec[] specs, CancellationToken ct) {
            if (specs.Length == 0) {
                Dispatcher.Invoke(() => {
                    CondaPreview = new CondaEnvironmentPreview() {
                        IsNoPackages = true
                    };
                });
                return;
            }

            try {
                ct.ThrowIfCancellationRequested();
                var result = await _condaMgr.PreviewCreateAsync(envName, specs, ct);
                ct.ThrowIfCancellationRequested();
                var preview = result?.Actions?.LinkPackages;
                var msg = result?.Message;
                Dispatcher.Invoke(() => {
                    var p = new CondaEnvironmentPreview();
                    if (preview != null) {
                        foreach (var package in preview.MaybeEnumerate().OrderBy(pkg => pkg.Name)) {
                            p.Packages.Add(new CondaPackageView() {
                                Name = package.Name.Trim(),
                                Version = package.VersionText,
                            });
                        }
                    }

                    if (result == null) {
                        msg = Strings.AddCondaEnvironmentPreviewFailed;
                    }
                    p.HasPreviewError = !string.IsNullOrEmpty(msg);
                    p.ErrorMessage = msg;
                    p.Progress.IsProgressDisplayed = false;
                    p.IsPackages = p.Packages.Count > 0;
                    p.IsNoPackages = !p.IsPackages && !p.HasPreviewError;

                    CondaPreview = p;
                });
            } catch (OperationCanceledException) {
            }
        }

        public override string ToString() {
            return Strings.AddCondaEnvironmentTabHeader;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _updatePreviewTimer.Dispose();
                _updatePreviewCancelTokenSource?.Cancel();
            }

            base.Dispose(disposing);
        }
    }

    sealed class CondaEnvironmentPreview : DependencyObject {
        public CondaEnvironmentPreview() {
            Packages = new ObservableCollection<CondaPackageView>();
            Progress = new ProgressControlViewModel();
            Progress.ProgressMessage = Strings.AddCondaEnvironmentPreviewInProgress;
            Progress.IsProgressDisplayed = false;
        }

        public static readonly DependencyProperty IsNoPackagesProperty =
            DependencyProperty.Register(nameof(IsNoPackages), typeof(bool), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty IsPackagesProperty =
            DependencyProperty.Register(nameof(IsPackages), typeof(bool), typeof(CondaEnvironmentPreview));

        private static readonly DependencyPropertyKey PackagesPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Packages), typeof(ObservableCollection<CondaPackageView>), typeof(CondaEnvironmentPreview), new PropertyMetadata());

        public static readonly DependencyProperty PackagesProperty =
            PackagesPropertyKey.DependencyProperty;

        public static readonly DependencyProperty HasPreviewErrorProperty =
            DependencyProperty.Register(nameof(HasPreviewError), typeof(bool), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty IsEnvFileProperty =
            DependencyProperty.Register(nameof(IsEnvFile), typeof(bool), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty IsNoEnvFileProperty =
            DependencyProperty.Register(nameof(IsNoEnvFile), typeof(bool), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty EnvFileContentsProperty =
            DependencyProperty.Register(nameof(EnvFileContents), typeof(string), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(CondaEnvironmentPreview));

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(ProgressControlViewModel), typeof(CondaEnvironmentPreview));

        public ObservableCollection<CondaPackageView> Packages {
            get { return (ObservableCollection<CondaPackageView>)GetValue(PackagesProperty); }
            private set { SetValue(PackagesPropertyKey, value); }
        }

        public bool IsNoPackages {
            get { return (bool)GetValue(IsNoPackagesProperty); }
            set { SetValue(IsNoPackagesProperty, value); }
        }

        public bool IsPackages {
            get { return (bool)GetValue(IsPackagesProperty); }
            set { SetValue(IsPackagesProperty, value); }
        }

        public bool HasPreviewError {
            get { return (bool)GetValue(HasPreviewErrorProperty); }
            set { SetValue(HasPreviewErrorProperty, value); }
        }

        public bool IsEnvFile {
            get { return (bool)GetValue(IsEnvFileProperty); }
            set { SetValue(IsEnvFileProperty, value); }
        }

        public bool IsNoEnvFile {
            get { return (bool)GetValue(IsNoEnvFileProperty); }
            set { SetValue(IsNoEnvFileProperty, value); }
        }

        public string EnvFileContents {
            get { return (string)GetValue(EnvFileContentsProperty); }
            set { SetValue(EnvFileContentsProperty, value); }
        }

        public string ErrorMessage {
            get { return (string)GetValue(ErrorMessageProperty); }
            set { SetValue(ErrorMessageProperty, value); }
        }

        public ProgressControlViewModel Progress {
            get { return (ProgressControlViewModel)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }
    }

    sealed class CondaPackageView : DependencyObject {
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(nameof(Name), typeof(string), typeof(CondaPackageView));

        public static readonly DependencyProperty VersionProperty =
            DependencyProperty.Register(nameof(Version), typeof(string), typeof(CondaPackageView));

        public string Name {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        public string Version {
            get { return (string)GetValue(VersionProperty); }
            set { SetValue(VersionProperty, value); }
        }
    }
}
