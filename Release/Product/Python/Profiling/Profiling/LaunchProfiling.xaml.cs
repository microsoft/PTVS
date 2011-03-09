using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using EnvDTE;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for LaunchProfiling.xaml
    /// </summary>
    public partial class LaunchProfiling : System.Windows.Window {
        private List<AvailableProject> _availableProjects;
        private List<AvailableInterpreter> _availableInterpreters;
        private ProfilingTarget _target;

        public LaunchProfiling() {
            _target = new ProfilingTarget();
            InitializeComponent();
            /*
            if (availableProjects.Count == 0) {
                _profileProject.IsEnabled = false;
            }*/
        }

        public LaunchProfiling(ProfilingTarget target) {
            _target = target;
            InitializeComponent();

            if (_target.ProjectTarget != null) {
                _profileProject.IsChecked = true;
                for (int i = 0; i < Projects.Count; i++) {
                    var project = Projects[i];
                    if (project.Guid == target.ProjectTarget.TargetProject) {
                        _project.SelectedIndex = i;
                        break;
                    }
                }
            } else if (_target.StandaloneTarget != null) {
                _profileScript.IsChecked = true;
                if (_target.StandaloneTarget.PythonInterpreter != null) {
                    var guid = _target.StandaloneTarget.PythonInterpreter.Id;
                    Version version;
                    if (Version.TryParse(_target.StandaloneTarget.PythonInterpreter.Version, out version)) {
                        for (int i = 0; i < InterpreterFactories.Count; i++) {
                            var fact = InterpreterFactories[i];
                            if (fact.Id == guid && fact.Version == version) {
                                _pythonInterpreter.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                } else {
                    _pythonInterpreter.Text = _target.StandaloneTarget.InterpreterPath ?? "";
                }

                _scriptName.Text = _target.StandaloneTarget.Script ?? "";
                _workingDir.Text = _target.StandaloneTarget.WorkingDirectory ?? "";
                _cmdLineArgs.Text = _target.StandaloneTarget.Arguments ?? "";
            }
        }

        public ProfilingTarget Target {
            get {
                return _target;
            }
        }

        public List<AvailableProject> Projects {
            get {
                if (_availableProjects == null) {
                    var service = (EnvDTE.DTE)(PythonProfilingPackage.GetGlobalService(typeof(EnvDTE.DTE)));

                    List<AvailableProject> availableProjects = new List<AvailableProject>();
                    foreach (EnvDTE.Project project in service.Solution.Projects) {
                        var kind = project.Kind;
                        if (String.Equals(kind, PythonProfilingPackage.PythonProjectGuid, StringComparison.OrdinalIgnoreCase)) {
                            // Python project available for profiling
                            availableProjects.Add(new AvailableProject(project));
                        }
                    }
                    _availableProjects = availableProjects;
                }
                return _availableProjects;
            }
        }

        public List<AvailableInterpreter> InterpreterFactories {
            get {
                if (_availableInterpreters == null) {
                    var service = (IComponentModel)(PythonProfilingPackage.GetGlobalService(typeof(SComponentModel)));

                    var factoryProviders = service.GetExtensions<IPythonInterpreterFactoryProvider>();
                    List<AvailableInterpreter> factories = new List<AvailableInterpreter>();
                    foreach (var factoryProvider in factoryProviders) {
                        foreach (var factory in factoryProvider.GetInterpreterFactories()) {
                            factories.Add(new AvailableInterpreter(factory));
                        }
                    }
                    _availableInterpreters = factories;
                }
                return _availableInterpreters;
            }
        }

        public bool CanLaunch {
            get {
                return _profileProject.IsChecked == true || _profileScript.IsChecked == true;
            }
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }

        private void OkButtonClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            Close();
        }

        private void FindScriptClick(object sender, RoutedEventArgs e) {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            var res = dlg.ShowDialog();
            if (res != null && res.Value) {
                _scriptName.Text = dlg.FileName;
            }
        }

        private void FindWorkingDirectoryClick(object sender, RoutedEventArgs e) {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK) {
                _workingDir.Text = dlg.SelectedPath;
            }
        }

        private void StandaloneScriptChecked(object sender, RoutedEventArgs e) {
            if (_target.StandaloneTarget == null) {
                _target.StandaloneTarget = new StandaloneTarget();
                UpdateInterpreterSetting();
                UpdateScriptSetting();
                UpdateWorkingDirSetting();
                UpdateArgumentsSetting();
            }
            Target.ProjectTarget = null;
        }

        private void ExistingProjectChecked(object sender, RoutedEventArgs e) {
            if (_target.ProjectTarget == null) {
                _target.ProjectTarget = new ProjectTarget();
                if (_project.SelectedIndex != -1) {
                    UpdateProjectSetting();
                }
            }
            _target.StandaloneTarget = null;
        }

        private void ProjectSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (_project.SelectedIndex != -1) {
                UpdateProjectSetting();
            } else {
                _target.ProjectTarget = null;
            }
        }

        private void UpdateProjectSetting() {
            var target = new ProjectTarget();
            target.TargetProject = Projects[_project.SelectedIndex].Guid;
            target.FriendlyName = Projects[_project.SelectedIndex].Name;
            _target.ProjectTarget = target;
        }

        private void InterpreterSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            UpdateInterpreterSetting();
        }

        private void OnInterpreterTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            UpdateInterpreterSetting();
        }

        private void UpdateInterpreterSetting() {
            if (_pythonInterpreter.SelectedIndex != -1) {
                var interpreter = Target.StandaloneTarget.PythonInterpreter = new PythonInterpreter();
                interpreter.Id = _availableInterpreters[_pythonInterpreter.SelectedIndex].Id;
                interpreter.Version = _availableInterpreters[_pythonInterpreter.SelectedIndex].Version.ToString();
                Target.StandaloneTarget.InterpreterPath = null;
            } else {
                Target.StandaloneTarget.PythonInterpreter = null;
                Target.StandaloneTarget.InterpreterPath = _pythonInterpreter.Text;
            }
        }

        private void ScriptTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            UpdateScriptSetting();
        }

        private void UpdateScriptSetting() {
            Target.StandaloneTarget.Script = _scriptName.Text;
        }

        private void WorkingDirChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            UpdateWorkingDirSetting();
        }

        private void UpdateWorkingDirSetting() {
            Target.StandaloneTarget.WorkingDirectory = _workingDir.Text;
        }

        private void ArgsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            UpdateArgumentsSetting();
        }

        private void UpdateArgumentsSetting() {
            Target.StandaloneTarget.Arguments = _cmdLineArgs.Text;
        }
    }
}
