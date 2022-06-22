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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
using Microsoft.CookiecutterTools.Telemetry;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Telemetry;
using Newtonsoft.Json;

namespace Microsoft.CookiecutterTools.ViewModel {
    class CookiecutterViewModel : INotifyPropertyChanged {
        private readonly ICookiecutterClient _cutterClient;
        private readonly IGitHubClient _githubClient;
        private readonly IGitClient _gitClient;
        private readonly ICookiecutterTelemetry _telemetry;
        private readonly IProjectSystemClient _projectSystemClient;
        private readonly Redirector _outputWindow;
        private readonly Action<string, string> _executeCommand;
        public static readonly ICommand LoadMore = new RoutedCommand();
        public static readonly ICommand OpenInBrowser = new RoutedCommand();
        public static readonly ICommand RunSelection = new RoutedCommand();
        public static readonly ICommand Search = new RoutedCommand();
        public static readonly ICommand CreateFilesCommand = new RoutedCommand();
        public static readonly ICommand HomeCommand = new RoutedCommand();

        private string _searchTerm;
        private string _outputFolderPath;
        private string _projectName;
        private string _selectedDescription;
        private ImageSource _selectedImage;
        private string _selectedLocation;
        private int _checkingUpdatePercentComplete;
        private bool _fixedOutputFolder;
        private ProjectLocation _targetProjectLocation;
        private DteCommand[] _postCommands;
        private bool _hasPostCommands;
        private bool _shouldExecutePostCommands;
        private PostCreateAction _postCreate;
        private bool _hasOpenProjectPostCommand;
        private bool _solutionLoaded;

        private OperationStatus _installingStatus;
        private OperationStatus _cloningStatus;
        private OperationStatus _loadingStatus;
        private OperationStatus _creatingStatus;
        private OperationStatus _checkingUpdateStatus;
        private OperationStatus _updatingStatus;

        private string _statusMessage, _statusHelp;
        private ImageMoniker _statusImage;
        private Visibility _statusVisibility = Visibility.Hidden;
        private Visibility _statusImageVisibility = Visibility.Collapsed;
        private Visibility _spinningStatusImageVisibility = Visibility.Collapsed;
        private Visibility _statusProgressVisibility = Visibility.Hidden;
        private Visibility _statusUpdateProgressVisibility = Visibility.Hidden;

        private TemplateViewModel _selectedTemplate;
        private CancellationTokenSource _templateRefreshCancelTokenSource;
        private CancellationTokenSource _checkUpdatesCancelTokenSource;

        private ITemplateSource _recommendedSource;
        private ILocalTemplateSource _installedSource;
        private ITemplateSource _githubSource;

        private string _templateLocalFolderPath;

        private const string CloneFolderName = ".cookiecutters";
        private const string DefaultConfigFileName = ".cookiecutterrc";
        private const string ConfigEnvironmentVariableName = "COOKIECUTTER_CONFIG";

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<EventArgs> ContextLoaded;
        public event EventHandler<EventArgs> HomeClicked;

        public ObservableCollection<CategorizedViewModel> SearchResults { get; } = new ObservableCollection<CategorizedViewModel>();
        public CategorizedViewModel Installed { get; }
        public CategorizedViewModel Recommended { get; }
        public CategorizedViewModel GitHub { get; }
        public CategorizedViewModel Custom { get; }

        public ObservableCollection<ContextItemViewModel> ContextItems { get; } = new ObservableCollection<ContextItemViewModel>();

        public string UserConfigFilePath { get; set; }

        public string InstalledFolderPath { get; set; } = DefaultInstalledFolderPath;

        public static string DefaultInstalledFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CloneFolderName);

        /// <summary>
        /// Constructor for design view.
        /// </summary>
        public CookiecutterViewModel() {
        }

        public CookiecutterViewModel(ICookiecutterClient cutter, IGitHubClient githubClient, IGitClient gitClient, ICookiecutterTelemetry telemetry, Redirector outputWindow, ILocalTemplateSource installedTemplateSource, ITemplateSource feedTemplateSource, ITemplateSource gitHubTemplateSource, Action<string, string> executeCommand, IProjectSystemClient projectSystemClient) {
            _cutterClient = cutter;
            _githubClient = githubClient;
            _gitClient = gitClient;
            _telemetry = telemetry;
            _outputWindow = outputWindow;
            _recommendedSource = feedTemplateSource;
            _installedSource = installedTemplateSource;
            _githubSource = gitHubTemplateSource;
            _executeCommand = executeCommand;
            _projectSystemClient = projectSystemClient;
            if (_projectSystemClient != null) {
                _projectSystemClient.SolutionOpenChanged += OnSolutionLoadedChanged;
            }
            Installed = new CategorizedViewModel(Strings.TemplateCategoryInstalled);
            Recommended = new CategorizedViewModel(Strings.TemplateCategoryRecommended);
            GitHub = new CategorizedViewModel(Strings.TemplateCategoryGitHub);
            Custom = new CategorizedViewModel(Strings.TemplateCategoryCustom);

            PropertyChanged += UpdateStatus;
        }

        private void OnSolutionLoadedChanged(object sender, EventArgs e) {
            _solutionLoaded = _projectSystemClient.IsSolutionOpen;
            if (!_solutionLoaded && TargetProjectLocation != null) {
                // User initiated cookiecutter explorer with add new item,
                // but they've now closed the solution, so update the state
                // so we no longer add to project.
                TargetProjectLocation = null;

                // If we've already loaded the template, we may have
                // initialized the context items with values based on
                // what was the state of VS at the time. We need to
                // reload the context to get the proper values.
                if (ContextItems.Any()) {
                    // RefreshContextAsync catches all non critical exceptions
                    // and prints them to output window.
                    RefreshContextAsync(SelectedTemplate).HandleAllExceptions(null, GetType()).DoNotWait();
                }

            }
            UpdatePostCreateOption();
        }

        protected void OnPropertyChange([CallerMemberName] string name = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string SearchTerm {
            get {
                return _searchTerm;
            }

            set {
                if (value != _searchTerm) {
                    _searchTerm = value;
                    OnPropertyChange();
                }
            }
        }

        public string OutputFolderPath {
            get {
                return _outputFolderPath;
            }

            set {
                if (value != _outputFolderPath) {
                    _outputFolderPath = value;
                    OnPropertyChange();
                }
            }
        }

        public string ProjectName {
            get {
                return _projectName;
            }

            set {
                if (value != _projectName) {
                    _projectName = value;
                    OnPropertyChange();
                    OnPropertyChange(nameof(IsFromProjectWizard));
                }
            }
        }

        public bool IsFromProjectWizard => !string.IsNullOrEmpty(ProjectName);

        public string SelectedDescription {
            get {
                return _selectedDescription;
            }

            set {
                if (value != _selectedDescription) {
                    _selectedDescription = value;
                    OnPropertyChange();
                }
            }
        }

        public ImageSource SelectedImage {
            get {
                return _selectedImage;
            }

            set {
                _selectedImage = value;
                OnPropertyChange();
            }
        }

        public string SelectedLocation {
            get {
                return _selectedLocation;
            }

            set {
                if (value != _selectedLocation) {
                    _selectedLocation = value;
                    OnPropertyChange();
                }
            }
        }

        public string StatusMessage => _statusMessage;
        public string StatusHelp => _statusHelp;
        public ImageMoniker StatusImage => _statusImage;
        public Visibility StatusVisibility => _statusVisibility;
        public Visibility StatusImageVisibility => _statusImageVisibility;
        public Visibility SpinningStatusImageVisibility => _spinningStatusImageVisibility;
        public Visibility StatusProgressVisibility => _statusProgressVisibility;
        public Visibility StatusUpdateProgressVisibility => _statusUpdateProgressVisibility;

        private void SetStatus(
            string message,
            string help = null,
            ImageMoniker? image = null,
            bool? visible = null,
            bool progressVisible = false,
            bool updateProgressVisible = false
        ) {
            _statusMessage = message ?? string.Empty;
            _statusHelp = help ?? string.Empty;
            _statusImage = image ?? default(ImageMoniker);
            _statusImageVisibility = image.HasValue && !progressVisible ? Visibility.Visible : Visibility.Collapsed;
            _spinningStatusImageVisibility = image.HasValue && progressVisible ? Visibility.Visible : Visibility.Collapsed;
            bool v = (visible ?? !string.IsNullOrEmpty(message));
            _statusVisibility = v ? Visibility.Visible : Visibility.Hidden;
            _statusProgressVisibility = progressVisible ? Visibility.Visible : Visibility.Hidden;
            _statusUpdateProgressVisibility = updateProgressVisible ? Visibility.Visible : Visibility.Hidden;

            if (!v) {
                OnPropertyChange(nameof(StatusVisibility));
            }
            OnPropertyChange(nameof(StatusMessage));
            OnPropertyChange(nameof(StatusHelp));
            OnPropertyChange(nameof(StatusImage));
            OnPropertyChange(nameof(StatusImageVisibility));
            OnPropertyChange(nameof(SpinningStatusImageVisibility));
            OnPropertyChange(nameof(StatusProgressVisibility));
            OnPropertyChange(nameof(StatusUpdateProgressVisibility));
            if (v) {
                OnPropertyChange(nameof(StatusVisibility));
            }
        }

        private void UpdateStatus(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(InstallingStatus):
                case nameof(CloningStatus):
                case nameof(LoadingStatus):
                //case nameof(CreatingStatus):
                case nameof(CheckingUpdateStatus):
                case nameof(UpdatingStatus):
                    break;
                default:
                    return;
            }

            OnPropertyChange(nameof(IsBusy));

            if (InstallingStatus == OperationStatus.InProgress) {
                SetStatus(
                    Strings.SearchPage_PreparingFirstTime,
                    Strings.SearchPage_PreparingFirstTimeTooltip,
                    KnownMonikers.Loading,
                    progressVisible: true
                );
            } else if (CloningStatus == OperationStatus.InProgress) {
                SetStatus(Strings.SearchPage_CloningRepository, progressVisible: true);
            } else if (LoadingStatus == OperationStatus.InProgress) {
                SetStatus(Strings.SearchPage_LoadingTemplate, progressVisible: true);
            } else if (CheckingUpdateStatus == OperationStatus.InProgress) {
                SetStatus(Strings.SearchPage_CheckingUpdates, updateProgressVisible: true);
            } else if (UpdatingStatus == OperationStatus.InProgress) {
                SetStatus(Strings.SearchPage_UpdatingTemplate, progressVisible: true);
            } else if (InstallingStatus == OperationStatus.Failed) {
                SetStatus(
                    Strings.SearchPage_ErrorInstallingCookiecutter,
                    image: KnownMonikers.StatusError
                );
            } else if (CloningStatus == OperationStatus.Failed) {
                SetStatus(
                    Strings.SearchPage_ErrorCloningRepository,
                    image: KnownMonikers.StatusError
                );
            } else if (LoadingStatus == OperationStatus.Failed) {
                SetStatus(
                    Strings.SearchPage_ErrorLoadingTemplate,
                    image: KnownMonikers.StatusError
                );
            } else if (CheckingUpdateStatus == OperationStatus.Failed) {
                SetStatus(
                    Strings.SearchPage_ErrorCheckingUpdates,
                    image: KnownMonikers.StatusError
                );
            } else if (UpdatingStatus == OperationStatus.Failed) {
                SetStatus(
                    Strings.SearchPage_UpdatingTemplateError,
                    image: KnownMonikers.StatusError
                );
            } else if (CheckingUpdateStatus == OperationStatus.Succeeded) {
                SetStatus(
                    Strings.SearchPage_CheckingUpdatesCompleted,
                    image: KnownMonikers.StatusOK
                );
            } else if (UpdatingStatus == OperationStatus.Succeeded) {
                SetStatus(
                    Strings.SearchPage_UpdatingTemplateCompleted,
                    image: KnownMonikers.StatusOK
                );
            } else {
                SetStatus(null);
            }
        }


        public OperationStatus InstallingStatus {
            get {
                return _installingStatus;
            }

            set {
                if (value != _installingStatus) {
                    _installingStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public OperationStatus CloningStatus {
            get {
                return _cloningStatus;
            }

            set {
                if (value != _cloningStatus) {
                    _cloningStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public OperationStatus LoadingStatus {
            get {
                return _loadingStatus;
            }

            set {
                if (value != _loadingStatus) {
                    _loadingStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public OperationStatus CreatingStatus {
            get {
                return _creatingStatus;
            }

            set {
                if (value != _creatingStatus) {
                    _creatingStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public OperationStatus CheckingUpdateStatus {
            get {
                return _checkingUpdateStatus;
            }

            set {
                if (value != _checkingUpdateStatus) {
                    _checkingUpdateStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public OperationStatus UpdatingStatus {
            get {
                return _updatingStatus;
            }

            set {
                if (value != _updatingStatus) {
                    _updatingStatus = value;
                    OnPropertyChange();
                }
            }
        }

        public bool IsBusy {
            get {
                return InstallingStatus == OperationStatus.InProgress ||
                    CloningStatus == OperationStatus.InProgress ||
                    LoadingStatus == OperationStatus.InProgress ||
                    CreatingStatus == OperationStatus.InProgress ||
                    UpdatingStatus == OperationStatus.InProgress;
            }
        }

        public int CheckingUpdatePercentComplete {
            get {
                return _checkingUpdatePercentComplete;
            }

            set {
                if (value != _checkingUpdatePercentComplete) {
                    _checkingUpdatePercentComplete = value;
                    OnPropertyChange();
                }
            }
        }

        public bool FixedOutputFolder {
            get {
                return _fixedOutputFolder;
            }
            set {
                if (value != _fixedOutputFolder) {
                    _fixedOutputFolder = value;
                    OnPropertyChange();
                }
            }
        }

        public bool HasPostCommands {
            get {
                return _hasPostCommands;
            }

            set {
                if (value != _hasPostCommands) {
                    _hasPostCommands = value;
                    OnPropertyChange();
                }
            }
        }

        public bool ShouldExecutePostCommands {
            get {
                return _shouldExecutePostCommands;
            }

            set {
                if (value != _shouldExecutePostCommands) {
                    _shouldExecutePostCommands = value;
                    OnPropertyChange();
                }
            }
        }

        private void UpdatePostCreateOption() {
            if (AddingToExistingProject) {
                PostCreate = PostCreateAction.AddToProject;
            } else {
                if (_hasOpenProjectPostCommand) {
                    if (_solutionLoaded) {
                        PostCreate = PostCreateAction.AddToSolution;
                    } else {
                        PostCreate = PostCreateAction.OpenProject;
                    }
                } else {
                    PostCreate = PostCreateAction.OpenFolder;
                }
            }
        }
        public PostCreateAction PostCreate {
            get {
                return _postCreate;
            }

            set {
                if (value != _postCreate) {
                    _postCreate = value;
                    OnPropertyChange();
                }
            }
        }

        public ProjectLocation TargetProjectLocation {
            get {
                return _targetProjectLocation;
            }

            set {
                if (value != _targetProjectLocation) {
                    _targetProjectLocation = value;
                    OnPropertyChange();
                    OnPropertyChange(nameof(AddingToExistingProject));
                }
            }
        }

        public bool AddingToExistingProject {
            get {
                return _targetProjectLocation != null;
            }
        }

        public TemplateViewModel SelectedTemplate {
            get {
                return _selectedTemplate;
            }

            set {
                if (value != _selectedTemplate) {
                    _selectedTemplate = value;
                    OnPropertyChange();
                }
            }
        }

        public bool CanLoadSelectedTemplate {
            get {
                return SelectedTemplate != null && !IsBusy;
            }
        }

        public bool CanRunSelectedTemplate {
            get {
                return SelectedTemplate != null && !IsBusy;
            }
        }

        public bool CanDeleteSelectedTemplate {
            get {
                return Directory.Exists(SelectedTemplate?.ClonedPath) && !IsBusy;
            }
        }

        public bool CanUpdateSelectedTemplate {
            get {
                return SelectedTemplate != null && SelectedTemplate.IsUpdateAvailable && !IsBusy;
            }
        }

        public bool CanNavigateToGitHub {
            get {
                return !string.IsNullOrEmpty(SelectedTemplate?.GitHubHomeUrl);
            }
        }

        public bool CanNavigateToOwner {
            get {
                return !string.IsNullOrEmpty(SelectedTemplate?.OwnerUrl);
            }
        }

        public bool CanCheckForUpdates {
            get {
                return !IsBusy && CheckingUpdateStatus != OperationStatus.InProgress;
            }
        }

        public bool IsOutputFolderEmpty() {
            if (Directory.Exists(OutputFolderPath)) {
                var files = Directory.EnumerateFileSystemEntries(OutputFolderPath);
                return files.Count() == 0;
            }

            return true;
        }

        public static string GetUserConfigPath() {
            var userConfigFilePath = Environment.GetEnvironmentVariable(ConfigEnvironmentVariableName);
            if (string.IsNullOrEmpty(userConfigFilePath)) {
                userConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultConfigFileName);
            }

            return userConfigFilePath;
        }

        public async Task SearchAsync(string autoSelectTemplateUri = null) {
            // Clear errors when a new search is performed
            ResetStatus(resetUpdateStatus: false);

            _templateRefreshCancelTokenSource?.Cancel();
            _templateRefreshCancelTokenSource = new CancellationTokenSource();
            try {
                ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.Load);

                await RefreshTemplatesAsync(SearchTerm, _templateRefreshCancelTokenSource.Token);

                if (!string.IsNullOrEmpty(autoSelectTemplateUri)) {
                    var firstTemplate = SearchResults
                        .SelectMany(cat => cat.Templates)
                        .OfType<TemplateViewModel>()
                        .Where(tmp => string.Compare(tmp.RemoteUrl, autoSelectTemplateUri, StringComparison.InvariantCultureIgnoreCase) == 0)
                        .FirstOrDefault();
                    if (firstTemplate != null) {
                        firstTemplate.IsSelected = true;
                    } else {
                        // We were asked to select a template which wasn't part of the search results
                        // so add it to the custom section and select it
                        var addedCustomTemplate = AddToCustomTemplates(autoSelectTemplateUri);
                        addedCustomTemplate.IsSelected = true;
                        if (!SearchResults.Contains(Custom)) {
                            SearchResults.Insert(0, Custom);
                        }
                    }
                }
            } catch (OperationCanceledException) {
            }
        }

        private async Task RefreshTemplatesAsync(string searchTerm, CancellationToken ct) {
            Custom.Templates.Clear();
            Recommended.Templates.Clear();
            GitHub.Templates.Clear();
            Installed.Templates.Clear();

            SearchResults.Clear();

            if (!string.IsNullOrEmpty(searchTerm)) {
                if (AddToCustomTemplates(searchTerm) != null) {
                    SearchResults.Add(Custom);
                }

                // If it's a location on local disk or http, then we're done
                if (SearchResults.Any()) {
                    // Ensure that there's a selection, to avoid focus issues
                    // when tabbing to the search results.
                    if (!AnySelection()) {
                        SelectFirstTemplateOrCategory();
                    }
                    return;
                }
            }

            SearchResults.Add(Installed);
            SearchResults.Add(Recommended);
            SearchResults.Add(GitHub);

            // Ensure that there's a selection, to avoid focus issues
            // when tabbing to the search results.
            if (!AnySelection()) {
                SelectFirstTemplateOrCategory();
            }

            var recommendedTask = AddFromSourceAsync(_recommendedSource, searchTerm, Recommended, false, ct);
            var installedTask = AddFromSourceAsync(_installedSource, searchTerm, Installed, false, ct);
            var githubTask = AddFromSourceAsync(_githubSource, searchTerm, GitHub, false, ct);

            await Task.WhenAll(recommendedTask, installedTask, githubTask);
        }

        private TemplateViewModel AddToCustomTemplates(string possibleUri) {
            var searchTermTemplate = new TemplateViewModel();
            searchTermTemplate.IsSearchTerm = true;

            if (possibleUri.StartsWithOrdinal("http")) {
                searchTermTemplate.DisplayName = possibleUri;
                searchTermTemplate.RemoteUrl = possibleUri;
                searchTermTemplate.Category = Custom.DisplayName;
                Custom.Templates.Add(searchTermTemplate);
                return searchTermTemplate;
            } else if (Directory.Exists(possibleUri)) {
                searchTermTemplate.DisplayName = possibleUri;
                searchTermTemplate.ClonedPath = possibleUri;
                searchTermTemplate.Category = Custom.DisplayName;
                Custom.Templates.Add(searchTermTemplate);
                return searchTermTemplate;
            }

            return null;
        }

        private bool AnySelection() {
            return SearchResults.Any(cat => cat.IsSelected) ||
                SearchResults.SelectMany(cat => cat.Templates).Any(tmp => tmp.IsSelected);
        }

        private void SelectFirstTemplateOrCategory() {
            var firstTemplate = SearchResults.SelectMany(cat => cat.Templates).FirstOrDefault();
            if (firstTemplate != null) {
                firstTemplate.IsSelected = true;
            } else {
                var firstCategory = SearchResults.FirstOrDefault();
                if (firstCategory != null) {
                    firstCategory.IsSelected = true;
                }
            }
        }

        public async Task DeleteTemplateAsync(TemplateViewModel template) {
            try {
                string remote = template.RemoteUrl;

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(String.Empty);
                _outputWindow.WriteLine(Strings.DeletingTemplateStarted.FormatUI(template.ClonedPath));

                await _installedSource.DeleteTemplateAsync(template.ClonedPath);

                _outputWindow.WriteLine(Strings.DeletingTemplateSuccess.FormatUI(template.ClonedPath));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Delete, template);

                if (!string.IsNullOrEmpty(remote)) {
                    var t = Installed.Templates.SingleOrDefault(current => (current as TemplateViewModel)?.RemoteUrl == remote) as TemplateViewModel;
                    if (t != null) {
                        Installed.Templates.Remove(t);
                    }

                    t = Recommended.Templates.SingleOrDefault(current => (current as TemplateViewModel)?.RemoteUrl == remote) as TemplateViewModel;
                    if (t != null) {
                        t.ClonedPath = string.Empty;
                    }

                    t = GitHub.Templates.SingleOrDefault(current => (current as TemplateViewModel)?.RemoteUrl == remote) as TemplateViewModel;
                    if (t != null) {
                        t.ClonedPath = string.Empty;
                    }
                } else {
                    if (Installed.Templates.Contains(template)) {
                        Installed.Templates.Remove(template);
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                _outputWindow.WriteErrorLine(ex.Message);
                _outputWindow.WriteLine(Strings.DeletingTemplateFailed.FormatUI(template.ClonedPath));
                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Delete, template, ex);
            }
        }

        public bool IsCloneNeeded(TemplateViewModel template) {
            // TODO: every search clears the ClonedPath for the online templates, so this will trigger cloning more often than we desire

            // If it's from online (recommended or github) and hasn't been cloned then we need to clone it
            return !string.IsNullOrEmpty(template.RemoteUrl) && string.IsNullOrEmpty(template.ClonedPath);
        }

        public bool IsCloneCollision(TemplateViewModel template, out TemplateViewModel collidingTemplate) {
            // If an installed template has the same repository name, we'll have a collision,
            // unless the installed template is a perfect match, ie. same repo owner.
            var result = Installed.Templates.OfType<TemplateViewModel>().Where(t => t.RepositoryName == template.RepositoryName && t.RepositoryFullName != template.RepositoryFullName).ToArray();
            if (result.Length > 0) {
                collidingTemplate = result.First();
                return true;
            } else {
                collidingTemplate = null;
                return false;
            }
        }

        public async Task LoadTemplateAsync() {
            var selection = SelectedTemplate;
            Debug.Assert(selection != null);
            if (selection == null) {
                throw new InvalidOperationException("LoadTemplateAsync called with null SelectedTemplate");
            }

            ResetStatus();

            _checkUpdatesCancelTokenSource?.Cancel();

            if (IsCloneNeeded(selection)) {
                CloningStatus = OperationStatus.InProgress;

                try {
                    _outputWindow.ShowAndActivate();
                    _outputWindow.WriteLine(String.Empty);
                    _outputWindow.WriteLine(Strings.CloningTemplateStarted.FormatUI(selection.DisplayName));

                    Directory.CreateDirectory(InstalledFolderPath);

                    selection.ClonedPath = await _gitClient.CloneAsync(selection.RemoteUrl, InstalledFolderPath);

                    CloningStatus = OperationStatus.Succeeded;

                    _outputWindow.WriteLine(Strings.CloningTemplateSuccess.FormatUI(selection.DisplayName, selection.ClonedPath));

                    ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Clone, selection);

                    await _installedSource.AddTemplateAsync(selection.ClonedPath);

                    _templateRefreshCancelTokenSource?.Cancel();
                    _templateRefreshCancelTokenSource = new CancellationTokenSource();
                    try {
                        Installed.Templates.Clear();
                        await AddFromSourceAsync(_installedSource, SearchTerm, Installed, false, CancellationToken.None);
                    } catch (OperationCanceledException) {
                    }

                    _templateLocalFolderPath = selection.ClonedPath;

                    await SetDefaultOutputFolderAsync(_templateLocalFolderPath);
                    await RefreshContextAsync(selection);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    CloningStatus = OperationStatus.Failed;

                    _outputWindow.WriteErrorLine(ex.Message);
                    _outputWindow.WriteLine(Strings.CloningTemplateFailed.FormatUI(selection.DisplayName));

                    ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Clone, selection, ex);
                }
            } else {
                Debug.Assert(!string.IsNullOrEmpty(selection.ClonedPath));
                _templateLocalFolderPath = selection.ClonedPath;
                await SetDefaultOutputFolderAsync(_templateLocalFolderPath);
                await RefreshContextAsync(selection);
            }
        }

        public async Task CheckForUpdatesAsync() {
            CheckingUpdateStatus = OperationStatus.NotStarted;

            bool anyError = false;
            try {
                _checkUpdatesCancelTokenSource?.Cancel();
                _checkUpdatesCancelTokenSource = new CancellationTokenSource();

                CheckingUpdateStatus = OperationStatus.InProgress;
                CheckingUpdatePercentComplete = 0;

#if DEBUG || VERBOSE_UPDATES
                _outputWindow.WriteLine(String.Empty);
                _outputWindow.WriteLine(Strings.CheckingForAllUpdatesStarted);
#endif

                var templatesResult = await _installedSource.GetTemplatesAsync(null, null, CancellationToken.None);
                for (int i = 0; i < templatesResult.Templates.Count; i++) {
                    CheckingUpdatePercentComplete = (int)((i / (double)templatesResult.Templates.Count) * 100);
                    var template = templatesResult.Templates[i];

                    _checkUpdatesCancelTokenSource.Token.ThrowIfCancellationRequested();

                    try {
#if DEBUG || VERBOSE_UPDATES
                        _outputWindow.WriteLine(Strings.CheckingTemplateUpdateStarted.FormatUI(template.Name, template.RemoteUrl));
#endif

                        var available = await _installedSource.CheckForUpdateAsync(template.RemoteUrl);

                        if (available == null) {
                            _outputWindow.WriteLine(Strings.CheckingTemplateUpdateInconclusive);
#if DEBUG || VERBOSE_UPDATES
                        } else if (available == true) {
                            _outputWindow.WriteLine(Strings.CheckingTemplateUpdateFound);
                        } else if (available == false) {
                            _outputWindow.WriteLine(Strings.CheckingTemplateUpdateNotFound);
#endif
                        }

                        var installed = Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(vm => vm.RemoteUrl == template.RemoteUrl);
                        if (installed != null) {
                            installed.IsUpdateAvailable = available == true;
                        }
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        if (!anyError) {
                            _outputWindow.ShowAndActivate();
#if DEBUG || VERBOSE_UPDATES
                            _outputWindow.WriteLine(String.Empty);
                            _outputWindow.WriteLine(Strings.CheckingForAllUpdatesStarted);
#endif
                        }

                        anyError = true;

                        _outputWindow.WriteLine(Strings.CheckingTemplateUpdateStarted.FormatUI(template.Name, template.RemoteUrl));
                        _outputWindow.WriteErrorLine(ex.Message);

                        var pex = ex as ProcessException;
                        if (pex != null) {
                            _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, pex.Result.StandardErrorLines ?? new string[0]));
                        }

                        _outputWindow.WriteLine(Strings.CheckingTemplateUpdateError);
                    }
                }

                CheckingUpdateStatus = anyError ? OperationStatus.Failed : OperationStatus.Succeeded;
                CheckingUpdatePercentComplete = 100;

                if (anyError) {
                    _outputWindow.WriteLine(Strings.CheckingForAllUpdatesFailed);
#if DEBUG || VERBOSE_UPDATES
                } else {
                    _outputWindow.WriteLine(Strings.CheckingForAllUpdatesSuccess);
#endif
                }

                ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.CheckUpdate, (!anyError).ToString());
            } catch (OperationCanceledException) {
                CheckingUpdateStatus = OperationStatus.Canceled;
#if DEBUG || VERBOSE_UPDATES
                _outputWindow.WriteLine(Strings.CheckingForAllUpdatesCanceled);
#else
                if (anyError) {
                    _outputWindow.WriteLine(Strings.CheckingForAllUpdatesCanceled);
                }
#endif
            }
        }

        public async Task UpdateTemplateAsync() {
            var selection = SelectedTemplate;
            Debug.Assert(selection != null);
            if (selection == null) {
                throw new InvalidOperationException("UpdateTemplateAsync called with null SelectedTemplate");
            }

            ResetStatus();

            try {
                UpdatingStatus = OperationStatus.InProgress;

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(String.Empty);
                _outputWindow.WriteLine(Strings.UpdatingTemplateStarted.FormatUI(selection.DisplayName));

                await _installedSource.UpdateTemplateAsync(selection.ClonedPath);
                selection.IsUpdateAvailable = false;

                UpdatingStatus = OperationStatus.Succeeded;

                _outputWindow.WriteLine(Strings.UpdatingTemplateSuccess.FormatUI(selection.DisplayName, selection.ClonedPath));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Update, selection);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                UpdatingStatus = OperationStatus.Failed;

                _outputWindow.WriteErrorLine(ex.Message);
                _outputWindow.WriteLine(Strings.UpdatingTemplateFailed.FormatUI(selection.DisplayName));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Update, selection, ex);
            }
        }

        private void ResetStatus(bool resetUpdateStatus = true) {
            CloningStatus = OperationStatus.NotStarted;
            LoadingStatus = OperationStatus.NotStarted;
            CreatingStatus = OperationStatus.NotStarted;
            if (resetUpdateStatus) {
                UpdatingStatus = OperationStatus.NotStarted;
                CheckingUpdateStatus = OperationStatus.NotStarted;
            }
        }

        public void Home() {
            HomeClicked?.Invoke(this, EventArgs.Empty);
        }

        public async Task CreateFilesAsync() {
            var selection = SelectedTemplate;
            Debug.Assert(selection != null);
            if (selection == null) {
                throw new InvalidOperationException("CreateFilesAsync called with null SelectedTemplate");
            }

            ResetStatus();

            CreatingStatus = OperationStatus.InProgress;

            try {
                var contextFilePath = Path.GetTempFileName();
                SaveUserInput(contextFilePath);

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(String.Empty);
                _outputWindow.WriteLine(Strings.RunningTemplateStarted.FormatUI(selection.DisplayName));

                var operationResult = await _cutterClient.CreateFilesAsync(_templateLocalFolderPath, UserConfigFilePath, contextFilePath, OutputFolderPath);

                if (operationResult.FilesReplaced.Length > 0) {
                    _outputWindow.WriteLine(Strings.ReplacedFilesHeader);
                    foreach (var replacedfile in operationResult.FilesReplaced) {
                        _outputWindow.WriteLine(Strings.ReplacedFile.FormatUI(replacedfile.OriginalFilePath, replacedfile.BackupFilePath));
                    }
                }

                var renderedContext = await _cutterClient.LoadRenderedContextAsync(_templateLocalFolderPath, UserConfigFilePath, contextFilePath, OutputFolderPath);
                _postCommands = renderedContext.Commands.ToArray();

                try {
                    File.Delete(contextFilePath);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }

                if (TargetProjectLocation != null) {
                    try {
                        var location = new ProjectLocation() {
                            FolderPath = OutputFolderPath,
                            ProjectUniqueName = TargetProjectLocation.ProjectUniqueName,
                        };
                        _projectSystemClient.AddToProject(location, operationResult);

                        RunPostCommands();

                        ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.AddToProject, selection);
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        _outputWindow.WriteErrorLine(Strings.AddToProjectError.FormatUI(ex.Message));

                        ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.AddToProject, selection, ex);
                    }
                }

                _outputWindow.WriteLine(Strings.RunningTemplateSuccess.FormatUI(selection.DisplayName, OutputFolderPath));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Run, selection);

                ContextItems.Clear();
                ResetStatus();
                _templateLocalFolderPath = null;
                CreatingStatus = OperationStatus.Succeeded;

                OpenFolderInExplorer(OutputFolderPath);
                Home();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                CreatingStatus = OperationStatus.Failed;

                _outputWindow.WriteErrorLine(ex.Message);
                _outputWindow.WriteLine(Strings.RunningTemplateFailed.FormatUI(selection.DisplayName));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Run, selection, ex);
            }
        }

        public void NavigateToGitHubHome() {
            var url = SelectedTemplate?.GitHubHomeUrl;
            if (url != null) {
                Process.Start(url)?.Dispose();
            }
        }

        public void NavigateToGitHubIssues() {
            var url = SelectedTemplate?.GitHubIssuesUrl;
            if (url != null) {
                Process.Start(url)?.Dispose();
            }
        }

        public void NavigateToGitHubWiki() {
            var url = SelectedTemplate?.GitHubWikiUrl;
            if (url != null) {
                Process.Start(url)?.Dispose();
            }
        }

        public void NavigateToOwner() {
            var url = SelectedTemplate?.OwnerUrl;
            if (url != null) {
                Process.Start(url)?.Dispose();
            }
        }

        public void NavigateToHelp() {
            Process.Start(UrlConstants.HelpUrl)?.Dispose();
        }

        private void RunPostCommands() {
            if (_postCommands == null || !ShouldExecutePostCommands) {
                return;
            }

            foreach (var cmd in _postCommands) {
                if (cmd.Name == "File.OpenProject") {
                    continue;
                }

                _executeCommand?.Invoke(cmd.Name, cmd.Args);
            }
        }

        private void OpenFolderInExplorer(string outputFolderPath) {
            try {
                // Rather than hard code detection of project files,
                // let the template specify which project/solution file
                // it wants to open.
                var openProjCmd = _postCommands?.FirstOrDefault(cmd => cmd.Name == "File.OpenProject");
                if (openProjCmd != null) {
                    if (_solutionLoaded) {
                        _projectSystemClient.AddToSolution(openProjCmd.Args.Trim('"'));
                    } else {
                        _executeCommand(openProjCmd.Name, openProjCmd.Args);
                    }
                } else {
                    _executeCommand("File.OpenFolder", outputFolderPath);
                    _executeCommand("View.SolutionExplorer", null);
                }

                RunPostCommands();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                _outputWindow.WriteErrorLine(ex.Message);
            }
        }

        public async Task SelectTemplateAsync(TemplateViewModel template) {
            SelectedTemplate = template;
            await RefreshSelectedDescriptionAsync(template);
        }

        public async Task LoadMoreTemplatesAsync(string continuationToken) {
            var last = GitHub.Templates.LastOrDefault();
            if (last is ContinuationViewModel) {
                _templateRefreshCancelTokenSource?.Cancel();
                _templateRefreshCancelTokenSource = new CancellationTokenSource();
                try {
                    GitHub.Templates.Remove(last);
                    ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.More);
                    await AddFromSourceAsync(_githubSource, null, GitHub, true, _templateRefreshCancelTokenSource.Token, continuationToken);
                } catch (OperationCanceledException) {
                }
            }
        }

        private async Task<bool> EnsureCookiecutterIsInstalledAsync() {
            if (await _cutterClient.IsCookiecutterInstalled()) {
                return true;
            }

            ResetStatus();

            InstallingStatus = OperationStatus.InProgress;

            try {
                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(Strings.InstallingCookiecutterStarted);

                await _cutterClient.CreateCookiecutterEnv();
                await _cutterClient.InstallPackage();

                _outputWindow.WriteLine(Strings.InstallingCookiecutterSuccess);

                InstallingStatus = OperationStatus.Succeeded;

                ReportEvent(CookiecutterTelemetry.TelemetryArea.Prereqs, CookiecutterTelemetry.PrereqsEvents.Install, true.ToString());
                return true;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                InstallingStatus = OperationStatus.Failed;

                _outputWindow.WriteErrorLine(ex.Message);
                _outputWindow.WriteLine(Strings.InstallingCookiecutterFailed);

                ReportEvent(CookiecutterTelemetry.TelemetryArea.Prereqs, CookiecutterTelemetry.PrereqsEvents.Install, false.ToString());
                return false;
            }
        }

        private async Task AddFromSourceAsync(
            ITemplateSource source,
            string searchTerm,
            CategorizedViewModel parent,
            bool alterSelection,
            CancellationToken ct,
            string continuationToken = null,
            VisualStudio.Imaging.Interop.ImageMoniker? updateableImage = null
        ) {
            var loading = new LoadingViewModel();
            parent.Templates.Add(loading);
            if (alterSelection) {
                loading.IsSelected = true;
            }

            try {
                var result = await source.GetTemplatesAsync(searchTerm, continuationToken, ct);
                foreach (var t in result.Templates) {
                    ct.ThrowIfCancellationRequested();

                    var vm = new TemplateViewModel();
                    vm.DisplayName = t.Name;
                    vm.Description = t.Description;
                    vm.AvatarUrl = t.AvatarUrl;
                    vm.OwnerUrl = t.OwnerUrl;
                    vm.RemoteUrl = t.RemoteUrl;
                    vm.ClonedPath = t.LocalFolderPath;
                    vm.Category = parent.DisplayName;
                    vm.IsUpdateAvailable = t.UpdateAvailable == true;
                    parent.Templates.Add(vm);
                }

                ct.ThrowIfCancellationRequested();

                if (result.ContinuationToken != null) {
                    var loadMore = new ContinuationViewModel(result.ContinuationToken);
                    parent.Templates.Add(loadMore);
                }
            } catch (TemplateEnumerationException ex) {
                var template = new ErrorViewModel() {
                    ErrorDescription = ex.Message,
                    ErrorDetails = ex.InnerException?.Message,
                };
                parent.Templates.Add(template);
            } finally {
                // Check if the loading item is still selected before we remove it, the user
                // may have selected something else while we were loading results.
                bool loadingStillSelected = loading.IsSelected;
                parent.Templates.Remove(loading);
                if (alterSelection && loadingStillSelected) {
                    // Loading was still selected, so select something else.
                    var newLast = parent.Templates.LastOrDefault() as TreeItemViewModel;
                    if (newLast != null) {
                        newLast.IsSelected = true;
                    }
                }
            }
        }

        private async Task SetDefaultOutputFolderAsync(string localTemplatePath) {
            if (FixedOutputFolder) {
                return;
            }

            OutputFolderPath = await _cutterClient.GetDefaultOutputFolderAsync(PathUtils.GetFileOrDirectoryName(_templateLocalFolderPath));
            Debug.Assert(!Directory.Exists(OutputFolderPath) && !File.Exists(PathUtils.TrimEndSeparator(OutputFolderPath)));
        }

        private async Task RefreshContextAsync(TemplateViewModel selection) {
            if (!await EnsureCookiecutterIsInstalledAsync()) {
                return;
            }

            try {
                LoadingStatus = OperationStatus.InProgress;

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(Strings.LoadingTemplateStarted.FormatUI(selection.DisplayName));

                var unrenderedContext = await _cutterClient.LoadUnrenderedContextAsync(selection.ClonedPath, UserConfigFilePath);

                InitializeContextItems(unrenderedContext);

                LoadingStatus = OperationStatus.Succeeded;

                _outputWindow.WriteLine(Strings.LoadingTemplateSuccess.FormatUI(selection.DisplayName));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Load, selection);

                // Go to the context page
                ContextLoaded?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                LoadingStatus = OperationStatus.Failed;

                _outputWindow.WriteErrorLine(ex.Message);
                _outputWindow.WriteLine(Strings.LoadingTemplateFailed.FormatUI(selection.DisplayName));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Load, selection, ex);
            }
        }

        internal void InitializeContextItems(TemplateContext unrenderedContext) {
            var sourceValues = new Dictionary<string, string>() {
                    { KnownValueSources.ProjectName, ProjectName ?? string.Empty },
                    { KnownValueSources.IsNewProject, TargetProjectLocation == null ? "y" : "n" },
                    { KnownValueSources.IsNewItem, TargetProjectLocation != null ? "y" : "n" },
                    { KnownValueSources.IsFromProjectWizard, IsFromProjectWizard ? "y" : "n" },
                };

            ContextItems.Clear();
            foreach (var item in unrenderedContext.Items) {
                var itemVM = new ContextItemViewModel(item.Name, item.Selector, item.Label, item.Description, item.Url, item.DefaultValue, item.Visible, item.Values);

                if (!string.IsNullOrEmpty(item.ValueSource)) {
                    if (sourceValues.TryGetValue(item.ValueSource, out string sourceValue)) {
                        itemVM.Val = sourceValue;
                    } else {
                        _outputWindow.WriteLine(Strings.LoadTemplateValueSourceNotSupportedWarning.FormatUI(item.Name, item.ValueSource));
                    }
                }

                ContextItems.Add(itemVM);
            }

            HasPostCommands = unrenderedContext.Commands.Count > 0;
            _hasOpenProjectPostCommand = unrenderedContext.Commands.Any(cmd => cmd.Name == "File.OpenProject");
            _solutionLoaded = _projectSystemClient.IsSolutionOpen;
            ShouldExecutePostCommands = HasPostCommands;
            UpdatePostCreateOption();
        }

        private async Task RefreshSelectedDescriptionAsync(TemplateViewModel selection) {
            if (selection == null) {
                SelectedDescription = string.Empty;
                SelectedImage = null;
                return;
            }

            if (!selection.HasDetails) {
                await InitializeDetailsAsync(selection);
            }

            SelectedDescription = selection.Description ?? string.Empty;
            try {
                // Create an ImageSource because binding to that feels significantly faster than binding to the image url
                SelectedImage = !string.IsNullOrEmpty(selection.AvatarUrl) ? new BitmapImage(new Uri(selection.AvatarUrl)) : null;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                SelectedImage = null;
            }
        }

        private async Task InitializeDetailsAsync(TemplateViewModel selection) {
            if (!string.IsNullOrEmpty(selection.RemoteUrl)) {
                try {
                    var repo = await _githubClient.GetRepositoryDetails(selection.RepositoryOwner, selection.RepositoryName);
                    selection.Description = repo.Description;
                    selection.AvatarUrl = repo.Owner.AvatarUrl;
                    selection.OwnerUrl = repo.Owner.HtmlUrl;
                } catch (WebException) {
                }
            } else {
                selection.Description = string.Empty;
                selection.AvatarUrl = string.Empty;
                selection.OwnerUrl = string.Empty;
            }
        }

        internal void SaveUserInput(string filePath) {
            var values = GetUserInput();
            var text = JsonConvert.SerializeObject(values, Formatting.Indented);
            File.WriteAllText(filePath, text);
        }

        internal Dictionary<string, string> GetUserInput() {
            var obj = new Dictionary<string, string>();
            foreach (var p in ContextItems) {
                if (!string.IsNullOrWhiteSpace(p.Val)) {
                    obj.Add(p.Name, p.Val);
                }
            }
            return obj;
        }

        private void ReportTemplateEvent(string area, string eventName, TemplateViewModel selection, Exception error = null) {
            try {
                if (!_telemetry.TelemetryService.IsEnabled) {
                    return;
                }

                var repoUrl = selection.RemoteUrl?.ToLowerInvariant() ?? string.Empty;
                var repoFullName = selection.RepositoryFullName?.ToLowerInvariant() ?? string.Empty;
                var repoOwner = selection.RepositoryOwner?.ToLowerInvariant() ?? string.Empty;
                var repoName = selection.RepositoryName?.ToLowerInvariant() ?? string.Empty;
                var projKind = TargetProjectLocation?.ProjectKind ?? string.Empty;

                var obj = new {
                    Success = error == null,
                    RepoUrl = new TelemetryPiiProperty(repoUrl),
                    RepoFullName = new TelemetryPiiProperty(repoFullName),
                    RepoOwner = new TelemetryPiiProperty(repoOwner),
                    RepoName = new TelemetryPiiProperty(repoName),
                    ProjectKind = projKind,
                };
                ReportEvent(area, eventName, obj);
            } catch (Exception ex) {
                Debug.Fail($"Error reporting event.\n{ex.Message}");
            }
        }

        private void ReportEvent(string area, string eventName, object parameters = null) {
            try {
                _telemetry.TelemetryService.ReportEvent(area, eventName, parameters);
            } catch (Exception ex) {
                Debug.Fail($"Error reporting event.\n{ex.Message}");
            }
        }
    }
}
