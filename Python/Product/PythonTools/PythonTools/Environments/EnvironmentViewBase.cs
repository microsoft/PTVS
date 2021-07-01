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
    abstract class EnvironmentViewBase : DependencyObject, INotifyDataErrorInfo, IDisposable
    {
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();
        private bool _ignoreSelectedProjectChanged;

        public EnvironmentViewBase(IServiceProvider serviceProvider, ProjectView[] projects, ProjectView selectedProject)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            _ignoreSelectedProjectChanged = true;

            Site = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            RegistryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            OptionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();

            AcceptCaption = Strings.AddEnvironmentAddButton;
            AcceptAutomationName = Strings.AddEnvironmentAddButtonAutomationName;
            IsAcceptEnabled = true;
            Projects = new ObservableCollection<ProjectView>(projects);
            SelectedProject = selectedProject;

            ProjectsView = new ListCollectionView(Projects);
            ProjectsView.MoveCurrentTo(selectedProject);

            _ignoreSelectedProjectChanged = false;
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public static readonly DependencyProperty PageNameProperty =
            DependencyProperty.Register(nameof(PageName), typeof(string), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty AcceptCaptionProperty =
            DependencyProperty.Register(nameof(AcceptCaption), typeof(string), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty AcceptAutomationNameProperty =
            DependencyProperty.Register(nameof(AcceptAutomationName), typeof(string), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty IsAcceptEnabledProperty =
            DependencyProperty.Register(nameof(IsAcceptEnabled), typeof(bool), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty IsAcceptShieldVisibleProperty =
            DependencyProperty.Register(nameof(IsAcceptShieldVisible), typeof(bool), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty SelectedProjectProperty =
            DependencyProperty.Register(nameof(SelectedProject), typeof(ProjectView), typeof(EnvironmentViewBase), new PropertyMetadata(SelectedProject_Changed));

        private static readonly DependencyPropertyKey ProjectsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Projects), typeof(ObservableCollection<ProjectView>), typeof(EnvironmentViewBase), new PropertyMetadata());

        public static readonly DependencyProperty ProjectsProperty =
            ProjectsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty SetAsCurrentProperty =
            DependencyProperty.Register(nameof(SetAsCurrent), typeof(bool), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty SetAsDefaultProperty =
            DependencyProperty.Register(nameof(SetAsDefault), typeof(bool), typeof(EnvironmentViewBase));

        public static readonly DependencyProperty ViewInEnvironmentWindowProperty =
            DependencyProperty.Register(nameof(ViewInEnvironmentWindow), typeof(bool), typeof(EnvironmentViewBase));

        protected IServiceProvider Site { get; }

        protected IInterpreterRegistryService RegistryService { get; }

        protected IInterpreterOptionsService OptionsService { get; }

        public ListCollectionView ProjectsView { get; }

        public string PageName
        {
            get { return (string)GetValue(PageNameProperty); }
            set { SetValue(PageNameProperty, value); }
        }

        public string AcceptCaption
        {
            get { return (string)GetValue(AcceptCaptionProperty); }
            set { SetValue(AcceptCaptionProperty, value); }
        }

        public string AcceptAutomationName
        {
            get { return (string)GetValue(AcceptAutomationNameProperty); }
            set { SetValue(AcceptAutomationNameProperty, value); }
        }

        public bool IsAcceptEnabled
        {
            get { return (bool)GetValue(IsAcceptEnabledProperty); }
            set { SetValue(IsAcceptEnabledProperty, value); }
        }

        public bool IsAcceptShieldVisible
        {
            get { return (bool)GetValue(IsAcceptShieldVisibleProperty); }
            set { SetValue(IsAcceptShieldVisibleProperty, value); }
        }

        public ProjectView SelectedProject
        {
            get { return (ProjectView)GetValue(SelectedProjectProperty); }
            set { SetValue(SelectedProjectProperty, value); }
        }

        public ObservableCollection<ProjectView> Projects
        {
            get { return (ObservableCollection<ProjectView>)GetValue(ProjectsProperty); }
            private set { SetValue(ProjectsPropertyKey, value); }
        }

        public bool SetAsCurrent
        {
            get { return (bool)GetValue(SetAsCurrentProperty); }
            set { SetValue(SetAsCurrentProperty, value); }
        }

        public bool SetAsDefault
        {
            get { return (bool)GetValue(SetAsDefaultProperty); }
            set { SetValue(SetAsDefaultProperty, value); }
        }

        public bool ViewInEnvironmentWindow
        {
            get { return (bool)GetValue(ViewInEnvironmentWindowProperty); }
            set { SetValue(ViewInEnvironmentWindowProperty, value); }
        }

        public bool HasErrors => _errors.Any(kv => kv.Value.Count > 0);

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            if (_errors.TryGetValue(propertyName, out List<string> val))
            {
                return val;
            }

            return null;
        }

        protected void SetError(string propertyName, string msg, bool notify = true)
        {
            _errors[propertyName] = new List<string>(Enumerable.Repeat(msg, 1));
            if (notify)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        protected void ClearErrors(string propertyName, bool notify = true)
        {
            _errors.Remove(propertyName);
            if (notify)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        protected void AddError(string propertyName, string msg, bool notify = true)
        {
            List<string> errors;
            if (!_errors.TryGetValue(propertyName, out errors))
            {
                errors = new List<string>();
                _errors[propertyName] = errors;
            }
            errors.Add(msg);
            if (notify)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        private static void SelectedProject_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as EnvironmentViewBase;
            if (view != null && !view._ignoreSelectedProjectChanged)
            {
                view.ResetProjectDependentProperties();
            }
        }

        public abstract Task ApplyAsync();

        public virtual IEnumerable<string> GetAllErrors()
        {
            foreach (var kv in _errors)
            {
                foreach (var error in kv.Value)
                {
                    yield return error;
                }
            }
        }

        protected virtual void ResetProjectDependentProperties()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
