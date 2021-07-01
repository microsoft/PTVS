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

namespace Microsoft.PythonTools.Environments
{
    sealed class AddExistingEnvironmentView : EnvironmentViewBase
    {
        private readonly IPythonToolsLogger _logger;
        private InterpreterView[] _allGlobalInterpreters;

        public AddExistingEnvironmentView(
            IServiceProvider serviceProvider,
            ProjectView[] projects,
            ProjectView selectedProject
        ) : base(serviceProvider, projects, selectedProject)
        {
            _logger = Site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
            _allGlobalInterpreters = InterpreterView.GetInterpreters(Site, null).ToArray();
            PageName = Strings.AddExistingEnvironmentTabHeader;
            ResetProjectDependentProperties();
        }

        public static readonly InterpreterView CustomInterpreter =
            new InterpreterView(string.Empty, Strings.AddExistingEnvironmentCustomName, Strings.AddExistingEnvironmentCustomPath, string.Empty, string.Empty, null);

        public static IList<string> ArchitectureNames { get; } = new[] {
            InterpreterArchitecture.x86.ToString(),
            InterpreterArchitecture.x64.ToString()
        };

        public static IList<string> VersionNames { get; } = new[] {
            "2.5",
            "2.6",
            "2.7",
            "3.0",
            "3.1",
            "3.2",
            "3.3",
            "3.4",
            "3.5",
            "3.6",
            "3.7",
            "3.8",
        };

        public static readonly DependencyProperty InterpretersProperty =
            DependencyProperty.Register(nameof(Interpreters), typeof(ObservableCollection<InterpreterView>), typeof(AddExistingEnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty SelectedInterpreterProperty =
            DependencyProperty.Register(nameof(SelectedInterpreter), typeof(InterpreterView), typeof(AddExistingEnvironmentView), new PropertyMetadata(SelectedInterpreter_Changed));

        public static readonly DependencyProperty IsCustomPrefixPathValidProperty =
            DependencyProperty.Register(nameof(IsCustomPrefixPathValid), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCustomInterpreterProperty =
            DependencyProperty.Register(nameof(IsCustomInterpreter), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCustomNotVirtualEnvProperty =
            DependencyProperty.Register(nameof(IsCustomNotVirtualEnv), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCustomVirtualEnvProperty =
            DependencyProperty.Register(nameof(IsCustomVirtualEnv), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty PrefixPathProperty =
            DependencyProperty.Register(nameof(PrefixPath), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", PrefixPath_Changed));

        public static readonly DependencyProperty IsAutoDetectRunningProperty =
            DependencyProperty.Register(nameof(IsAutoDetectRunning), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty RegisterCustomEnvProperty =
            DependencyProperty.Register(nameof(RegisterCustomEnv), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsRegisterCustomEnvEnabledProperty =
            DependencyProperty.Register(nameof(IsRegisterCustomEnvEnabled), typeof(bool), typeof(AddExistingEnvironmentView), new PropertyMetadata(true));

        public static readonly DependencyProperty InterpreterPathProperty =
            DependencyProperty.Register(nameof(InterpreterPath), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", InterpreterPath_Changed));

        public static readonly DependencyProperty WindowsInterpreterPathProperty =
            DependencyProperty.Register(nameof(WindowsInterpreterPath), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", WindowsInterpreterPath_Changed));

        public static readonly DependencyProperty VersionNameProperty =
            DependencyProperty.Register(nameof(VersionName), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", VersionName_Changed));

        public static readonly DependencyProperty ArchitectureNameProperty =
            DependencyProperty.Register(nameof(ArchitectureName), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", ArchitectureName_Changed));

        public static readonly DependencyProperty PathEnvironmentVariableProperty =
            DependencyProperty.Register(nameof(PathEnvironmentVariable), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata(""));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(AddExistingEnvironmentView), new PropertyMetadata("", Description_Changed));

        public ObservableCollection<InterpreterView> Interpreters
        {
            get { return (ObservableCollection<InterpreterView>)GetValue(InterpretersProperty); }
            private set { SetValue(InterpretersProperty, value); }
        }

        public InterpreterView SelectedInterpreter
        {
            get { return (InterpreterView)GetValue(SelectedInterpreterProperty); }
            set { SetValue(SelectedInterpreterProperty, value); }
        }

        public bool IsCustomPrefixPathValid
        {
            get { return (bool)GetValue(IsCustomPrefixPathValidProperty); }
            set { SetValue(IsCustomPrefixPathValidProperty, value); }
        }

        public bool IsCustomInterpreter
        {
            get { return (bool)GetValue(IsCustomInterpreterProperty); }
            set { SetValue(IsCustomInterpreterProperty, value); }
        }

        public bool IsCustomNotVirtualEnv
        {
            get { return (bool)GetValue(IsCustomNotVirtualEnvProperty); }
            set { SetValue(IsCustomNotVirtualEnvProperty, value); }
        }

        public bool IsCustomVirtualEnv
        {
            get { return (bool)GetValue(IsCustomVirtualEnvProperty); }
            set { SetValue(IsCustomVirtualEnvProperty, value); }
        }

        public string PrefixPath
        {
            get { return (string)GetValue(PrefixPathProperty); }
            set { SetValue(PrefixPathProperty, value); }
        }

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public bool IsAutoDetectRunning
        {
            get { return (bool)GetValue(IsAutoDetectRunningProperty); }
            set { SetValue(IsAutoDetectRunningProperty, value); }
        }

        public bool RegisterCustomEnv
        {
            get { return (bool)GetValue(RegisterCustomEnvProperty); }
            set { SetValue(RegisterCustomEnvProperty, value); }
        }

        public bool IsRegisterCustomEnvEnabled
        {
            get { return (bool)GetValue(IsRegisterCustomEnvEnabledProperty); }
            set { SetValue(IsRegisterCustomEnvEnabledProperty, value); }
        }

        public string InterpreterPath
        {
            get { return (string)GetValue(InterpreterPathProperty); }
            set { SetValue(InterpreterPathProperty, value); }
        }

        public string WindowsInterpreterPath
        {
            get { return (string)GetValue(WindowsInterpreterPathProperty); }
            set { SetValue(WindowsInterpreterPathProperty, value); }
        }

        public string VersionName
        {
            get { return (string)GetValue(VersionNameProperty); }
            set { SetValue(VersionNameProperty, value); }
        }

        public string ArchitectureName
        {
            get { return (string)GetValue(ArchitectureNameProperty); }
            set { SetValue(ArchitectureNameProperty, value); }
        }

        public string PathEnvironmentVariable
        {
            get { return (string)GetValue(PathEnvironmentVariableProperty); }
            set { SetValue(PathEnvironmentVariableProperty, value); }
        }

        private static void PrefixPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentPrefixPathChanged();
        }

        private static void Description_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentDetailsChanged();
        }

        private static void InterpreterPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentDetailsChanged();
        }

        private static void WindowsInterpreterPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentDetailsChanged();
        }

        private static void VersionName_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentDetailsChanged();
        }

        private static void ArchitectureName_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).CustomEnvironmentDetailsChanged();
        }

        private static void SelectedInterpreter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AddExistingEnvironmentView)d).SelectedInterpreterChanged();
        }

        private void SelectedInterpreterChanged()
        {
            IsCustomInterpreter = SelectedInterpreter == CustomInterpreter;
            PrefixPath = string.Empty;
            ValidateCustomData();
        }

        private void CustomEnvironmentDetailsChanged()
        {
            ValidateCustomData();
        }

        private void CustomEnvironmentPrefixPathChanged()
        {
            if (IsCustomInterpreter)
            {
                if (Directory.Exists(PrefixPath))
                {
                    IsCustomPrefixPathValid = true;

                    var config = VirtualEnv.FindInterpreterConfiguration(null, PrefixPath, RegistryService);
                    if (config != null && File.Exists(config.InterpreterPath))
                    {
                        var baseInterp = _allGlobalInterpreters.FirstOrDefault(v => v.Id == config.Id);

                        IsRegisterCustomEnvEnabled = SelectedProject != null;
                        RegisterCustomEnv = SelectedProject == null;
                        IsCustomVirtualEnv = baseInterp != null;
                        IsCustomNotVirtualEnv = baseInterp == null;

                        SetCustomVariables(config);
                    }
                    else
                    {
                        IsRegisterCustomEnvEnabled = SelectedProject != null;
                        RegisterCustomEnv = true;
                        IsCustomNotVirtualEnv = true;
                        IsCustomVirtualEnv = false;

                        ClearCustomVariables();

                        AutoDetectFromCustomPrefixPathAsync().DoNotWait();
                    }

                    ValidateCustomData();
                }
                else
                {
                    IsRegisterCustomEnvEnabled = false;
                    RegisterCustomEnv = false;
                    IsCustomPrefixPathValid = false;
                    IsCustomNotVirtualEnv = false;
                    IsCustomVirtualEnv = false;

                    // For now, we enable but prompt when they click accept
                    //IsAcceptEnabled = false;
                    IsAcceptEnabled = true;

                    ClearCustomVariables();
                }
            }
            else
            {
                IsRegisterCustomEnvEnabled = false;
                RegisterCustomEnv = false;
                IsCustomPrefixPathValid = false;
                IsCustomNotVirtualEnv = false;
                IsCustomVirtualEnv = false;
                IsAcceptEnabled = true;

                ClearCustomVariables();
            }
        }

        private void ValidateCustomData()
        {
            // For now, we enable but prompt when they click accept
            //IsAcceptEnabled = IsValidCustomData();
            IsAcceptEnabled = !IsAutoDetectRunning;

            if (IsCustomInterpreter)
            {
                if (!Directory.Exists(PrefixPath))
                {
                    SetError(nameof(PrefixPath), Strings.AddExistingEnvironmentPrefixPathFolderNotFound.FormatUI(PrefixPath ?? string.Empty));
                }
                else
                {
                    ClearErrors(nameof(PrefixPath));
                }

                if (!File.Exists(InterpreterPath))
                {
                    SetError(nameof(InterpreterPath), Strings.AddExistingEnvironmentInterpreterPathNotFound.FormatUI(InterpreterPath ?? string.Empty));
                }
                else
                {
                    ClearErrors(nameof(InterpreterPath));
                }

                if (!string.IsNullOrEmpty(WindowsInterpreterPath) && !File.Exists(WindowsInterpreterPath))
                {
                    SetError(nameof(WindowsInterpreterPath), Strings.AddExistingEnvironmentWindowsInterpreterPathNotFound.FormatUI(WindowsInterpreterPath ?? string.Empty));
                }
                else
                {
                    ClearErrors(nameof(WindowsInterpreterPath));
                }

                if (string.IsNullOrEmpty(Description))
                {
                    SetError(nameof(Description), Strings.AddEnvironmentDescriptionEmpty);
                }
                else if (Description.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    SetError(nameof(Description), Strings.AddEnvironmentDescriptionInvalid);
                }
                else
                {
                    ClearErrors(nameof(Description));
                }

                if (string.IsNullOrEmpty(ArchitectureName))
                {
                    SetError(nameof(ArchitectureName), Strings.AddExistingEnvironmentArchictureEmpty);
                }
                else
                {
                    ClearErrors(nameof(ArchitectureName));
                }

                if (string.IsNullOrEmpty(VersionName))
                {
                    SetError(nameof(VersionName), Strings.AddExistingEnvironmentVersionEmpty);
                }
                else
                {
                    ClearErrors(nameof(VersionName));
                }
            }
            else
            {
                ClearErrors(nameof(PrefixPath));
                ClearErrors(nameof(InterpreterPath));
                ClearErrors(nameof(WindowsInterpreterPath));
                ClearErrors(nameof(Description));
                ClearErrors(nameof(ArchitectureName));
                ClearErrors(nameof(VersionName));
            }
        }

        private bool IsValidCustomData()
        {
            return !string.IsNullOrEmpty(Description) &&
                Directory.Exists(PrefixPath) &&
                File.Exists(InterpreterPath) &&
                (string.IsNullOrEmpty(WindowsInterpreterPath) || File.Exists(WindowsInterpreterPath)) &&
                !string.IsNullOrEmpty(ArchitectureName) &&
                !string.IsNullOrEmpty(VersionName);
        }

        private void SetCustomVariables(InterpreterConfiguration config)
        {
            Description = config.Description;
            InterpreterPath = config.InterpreterPath;
            WindowsInterpreterPath = config.GetWindowsInterpreterPath();
            ArchitectureName = config.ArchitectureString;
            VersionName = config.Version.ToString();
            PathEnvironmentVariable = config.PathEnvironmentVariable;
        }

        private void ClearCustomVariables()
        {
            Description = string.Empty;
            InterpreterPath = string.Empty;
            WindowsInterpreterPath = string.Empty;
            ArchitectureName = string.Empty;
            VersionName = string.Empty;
            PathEnvironmentVariable = string.Empty;
        }

        private async Task AutoDetectFromCustomPrefixPathAsync()
        {
            // TODO: cancel previous auto detect if one is running (use same _working as in virtual env view)
            if (IsAutoDetectRunning)
            {
                return;
            }

            IsAutoDetectRunning = true;

            try
            {
                await AutoDetectAsync(this);
            }
            finally
            {
                IsAutoDetectRunning = false;
            }

            ValidateCustomData();
        }

        private async Task<AddExistingEnvironmentView> AutoDetectAsync(AddExistingEnvironmentView view)
        {
            if (!Directory.Exists(view.PrefixPath))
            {
                // If view.PrefixPath is not valid by this point, we can't find anything
                // else, so abort without changes.
                return view;
            }

            if (string.IsNullOrEmpty(view.Description))
            {
                view.Description = PathUtils.GetFileOrDirectoryName(view.PrefixPath);
            }

            if (!File.Exists(view.InterpreterPath))
            {
                view.InterpreterPath = PathUtils.FindFile(
                    view.PrefixPath,
                    CPythonInterpreterFactoryConstants.ConsoleExecutable,
                    firstCheck: new[] { "scripts" }
                );
            }

            if (!File.Exists(view.WindowsInterpreterPath))
            {
                view.WindowsInterpreterPath = PathUtils.FindFile(
                    view.PrefixPath,
                    CPythonInterpreterFactoryConstants.WindowsExecutable,
                    firstCheck: new[] { "scripts" }
                );
            }

            if (File.Exists(view.InterpreterPath))
            {
                using (var output = ProcessOutput.RunHiddenAndCapture(
                    view.InterpreterPath, "-c", "import sys; print('%s.%s' % (sys.version_info[0], sys.version_info[1])); print(sys.platform)"
                ))
                {
                    var exitCode = await output;
                    if (exitCode == 0)
                    {
                        view.VersionName = output.StandardOutputLines.FirstOrDefault() ?? view.VersionName;
                    }
                }

                var arch = CPythonInterpreterFactoryProvider.ArchitectureFromExe(view.InterpreterPath);
                if (arch != InterpreterArchitecture.Unknown)
                {
                    view.ArchitectureName = arch.ToString();
                }

                if (string.IsNullOrEmpty(view.PathEnvironmentVariable))
                {
                    view.PathEnvironmentVariable = "PYTHONPATH";
                }
            }

            return view;
        }

        protected override void ResetProjectDependentProperties()
        {
            // When there's no project, the only action that is viable is to register a custom environment.
            IEnumerable<InterpreterView> available = SelectedProject != null
                ? InterpreterView.GetInterpreters(Site, SelectedProject?.Node).Where(view => SelectedProject.InterpreterIds.IndexOf(view.Id) < 0)
                : Enumerable.Empty<InterpreterView>();

            var interpreters = new ObservableCollection<InterpreterView>(available);
            interpreters.Insert(0, CustomInterpreter);

            Interpreters = interpreters;

            if (Interpreters.Count > 1)
            {
                SelectedInterpreter = (InterpreterView)Interpreters[1];
            }
            else
            {
                SelectedInterpreter = CustomInterpreter;
            }
        }

        public async override Task ApplyAsync()
        {
            bool failed = false;
            if (IsCustomInterpreter)
            {
                try
                {
                    await ApplyCustomAsync();
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    failed = true;
                    throw;
                }
                finally
                {
                    _logger?.LogEvent(PythonLogEvent.AddExistingEnv, new AddExistingEnvInfo()
                    {
                        Failed = failed,
                        LanguageVersion = VersionName,
                        Architecture = ArchitectureName,
                        Custom = true,
                        Global = RegisterCustomEnv,
                    });
                }
            }
            else
            {
                try
                {
                    await ApplyExistingAsync();
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    failed = true;
                    throw;
                }
                finally
                {
                    _logger?.LogEvent(PythonLogEvent.AddExistingEnv, new AddExistingEnvInfo()
                    {
                        Failed = failed,
                        LanguageVersion = SelectedInterpreter.LanguageVersion,
                        Architecture = SelectedInterpreter.Architecture,
                    });
                }
            }
        }

        private async Task ApplyCustomAsync()
        {
            IPythonInterpreterFactory factory = null;

            if (RegisterCustomEnv)
            {
                Version version;
                if (!Version.TryParse(VersionName, out version))
                {
                    version = null;
                }

                factory = await CustomEnv.CreateCustomEnv(
                    RegistryService,
                    OptionsService,
                    PrefixPath,
                    InterpreterPath,
                    WindowsInterpreterPath,
                    PathEnvironmentVariable,
                    InterpreterArchitecture.TryParse(ArchitectureName ?? ""),
                    version,
                    Description
                );
            }
            else
            {
                Debug.Assert(SelectedProject != null, "Project is null, UI should not have allowed this");
                if (SelectedProject != null)
                {
                    Version version;
                    if (!Version.TryParse(VersionName, out version))
                    {
                        version = null;
                    }

                    if (SelectedProject.Node != null)
                    {
                        factory = SelectedProject.Node.AddMSBuildEnvironment(
                            RegistryService,
                            PrefixPath,
                            InterpreterPath,
                            WindowsInterpreterPath,
                            PathEnvironmentVariable,
                            version,
                            InterpreterArchitecture.TryParse(ArchitectureName ?? ""),
                            Description
                        );
                    }
                    else if (SelectedProject.Workspace != null)
                    {
                        await SelectedProject.Workspace.SetInterpreterAsync(InterpreterPath);
                    }
                }
            }

            if (factory != null)
            {
                if (SelectedProject != null)
                {
                    if (SelectedProject.Node != null)
                    {
                        SelectedProject.Node.AddInterpreter(factory.Configuration.Id);
                        if (SetAsCurrent)
                        {
                            SelectedProject.Node.SetInterpreterFactory(factory);
                        }
                    }
                    else if (SelectedProject.Workspace != null)
                    {
                        await SelectedProject.Workspace.SetInterpreterFactoryAsync(factory);
                    }
                }

                if (SetAsDefault)
                {
                    OptionsService.DefaultInterpreter = factory;
                }
            }
        }

        private async Task ApplyExistingAsync()
        {
            if (SelectedProject.Node != null)
            {
                var ids = SelectedProject.InterpreterIds.Union(new string[] { SelectedInterpreter.Id }).ToArray();
                SelectedProject?.Node.ChangeInterpreters(ids);
            }
            else if (SelectedProject.Workspace != null)
            {
                var factory = RegistryService.FindInterpreter(SelectedInterpreter.Id);
                if (factory != null)
                {
                    await SelectedProject.Workspace.SetInterpreterFactoryAsync(factory);
                }
            }
        }

        public override string ToString()
        {
            return Strings.AddExistingEnvironmentTabHeader;
        }
    }
}
