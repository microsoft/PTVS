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
// MERCHANTABLITY OR NON-INFRINGEMENT.
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
using Microsoft.CookiecutterTools.Resources;
using Microsoft.CookiecutterTools.Telemetry;
using Microsoft.VisualStudio.Imaging;
using Newtonsoft.Json;

namespace Microsoft.CookiecutterTools.ViewModel {
    class CookiecutterViewModel : INotifyPropertyChanged {
        private readonly ICookiecutterClient _cutterClient;
        private readonly IGitHubClient _githubClient;
        private readonly IGitClient _gitClient;
        private readonly ICookiecutterTelemetry _telemetry;
        private readonly Redirector _outputWindow;
        private readonly Action<string> _openFolder;

        public static readonly ICommand LoadMore = new RoutedCommand();
        public static readonly ICommand OpenInBrowser = new RoutedCommand();
        public static readonly ICommand OpenInExplorer = new RoutedCommand();
        public static readonly ICommand RunSelection = new RoutedCommand();
        public static readonly ICommand Search = new RoutedCommand();
        public static readonly ICommand CreateFilesCommand = new RoutedCommand();
        public static readonly ICommand HomeCommand = new RoutedCommand();

        private string _searchTerm;
        private string _outputFolderPath;
        private string _openInExplorerFolderPath;
        private string _selectedDescription;
        private string _selectedLocation;

        private OperationStatus _installingStatus;
        private OperationStatus _cloningStatus;
        private OperationStatus _loadingStatus;
        private OperationStatus _creatingStatus;
        private OperationStatus _checkingUpdateStatus;
        private OperationStatus _updatingStatus;

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

        public CookiecutterViewModel(ICookiecutterClient cutter, IGitHubClient githubClient, IGitClient gitClient, ICookiecutterTelemetry telemetry, Redirector outputWindow, ILocalTemplateSource installedTemplateSource, ITemplateSource feedTemplateSource, ITemplateSource gitHubTemplateSource, Action<string> openFolder) {
            _cutterClient = cutter;
            _githubClient = githubClient;
            _gitClient = gitClient;
            _telemetry = telemetry;
            _outputWindow = outputWindow;
            _recommendedSource = feedTemplateSource;
            _installedSource = installedTemplateSource;
            _githubSource = gitHubTemplateSource;
            _openFolder = openFolder;

            Installed = new CategorizedViewModel(Strings.TemplateCategoryInstalled);
            Recommended = new CategorizedViewModel(Strings.TemplateCategoryRecommended);
            GitHub = new CategorizedViewModel(Strings.TemplateCategoryGitHub);
            Custom = new CategorizedViewModel(Strings.TemplateCategoryCustom);
        }

        public string SearchTerm {
            get {
                return _searchTerm;
            }

            set {
                if (value != _searchTerm) {
                    _searchTerm = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchTerm)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputFolderPath)));
                }
            }
        }

        public string OpenInExplorerFolderPath {
            get {
                return _openInExplorerFolderPath;
            }

            set {
                if (value != _openInExplorerFolderPath) {
                    _openInExplorerFolderPath = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenInExplorerFolderPath)));
                }
            }
        }

        public string SelectedDescription {
            get {
                return _selectedDescription;
            }

            set {
                if (value != _selectedDescription) {
                    _selectedDescription = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDescription)));
                }
            }
        }

        public string SelectedLocation {
            get {
                return _selectedLocation;
            }

            set {
                if (value != _selectedLocation) {
                    _selectedLocation = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLocation)));
                }
            }
        }

        public OperationStatus InstallingStatus {
            get {
                return _installingStatus;
            }

            set {
                if (value != _installingStatus) {
                    _installingStatus = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallingStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloningStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadingStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CreatingStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckingUpdateStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdatingStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
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

        public TemplateViewModel SelectedTemplate {
            get {
                return _selectedTemplate;
            }

            set {
                if (value != _selectedTemplate) {
                    _selectedTemplate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTemplate)));
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

        public async Task SearchAsync() {
            _templateRefreshCancelTokenSource?.Cancel();
            _templateRefreshCancelTokenSource = new CancellationTokenSource();
            try {
                ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.Load);

                await RefreshTemplatesAsync(SearchTerm, _templateRefreshCancelTokenSource.Token);
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
                var searchTermTemplate = new TemplateViewModel();
                searchTermTemplate.IsSearchTerm = true;

                if (searchTerm.StartsWith("http")) {
                    searchTermTemplate.DisplayName = searchTerm;
                    searchTermTemplate.RemoteUrl = searchTerm;
                    searchTermTemplate.Image = ImageMonikers.CookiecutterTemplate;
                    searchTermTemplate.UpdateableImage = ImageMonikers.CookiecutterTemplate;
                    Custom.Templates.Add(searchTermTemplate);
                    SearchResults.Add(Custom);
                    return;
                } else if (Directory.Exists(searchTerm)) {
                    searchTermTemplate.DisplayName = searchTerm;
                    searchTermTemplate.ClonedPath = searchTerm;
                    searchTermTemplate.Image = ImageMonikers.CookiecutterTemplateOK;
                    searchTermTemplate.UpdateableImage = ImageMonikers.CookiecutterTemplateUpdate;
                    Custom.Templates.Add(searchTermTemplate);
                    SearchResults.Add(Custom);
                    return;
                }
            }

            SearchResults.Add(Installed);
            SearchResults.Add(Recommended);
            SearchResults.Add(GitHub);

            var recommendedTask = AddFromSource(_recommendedSource, searchTerm, KnownMonikers.RecommendedTest, Recommended, ct);
            var installedTask = AddFromSource(_installedSource, searchTerm, KnownMonikers.TestSuite, Installed, ct);
            var githubTask = AddFromSource(_githubSource, searchTerm, KnownMonikers.GitNoColor, GitHub, ct);

            await Task.WhenAll(recommendedTask, installedTask, githubTask);
        }

        public async Task DeleteTemplateAsync(TemplateViewModel template) {
            try {
                string remote = template.RemoteUrl;

                _outputWindow.ShowAndActivate();
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
                        await AddFromSource(_installedSource, SearchTerm, ImageMonikers.CookiecutterTemplateOK, Installed, CancellationToken.None);
                    } catch (OperationCanceledException) {
                    }

                    _templateLocalFolderPath = selection.ClonedPath;

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

                await RefreshContextAsync(selection);
            }
        }

        public async Task CheckForUpdatesAsync() {
            ResetStatus();

            try {
                _checkUpdatesCancelTokenSource?.Cancel();
                _checkUpdatesCancelTokenSource = new CancellationTokenSource();

                CheckingUpdateStatus = OperationStatus.InProgress;

                _outputWindow.WriteLine(Strings.CheckingForAllUpdatesStarted);

                bool anyError = false;
                var templatesResult = await _installedSource.GetTemplatesAsync(null, null, CancellationToken.None);
                foreach (var template in templatesResult.Templates) {
                    _checkUpdatesCancelTokenSource.Token.ThrowIfCancellationRequested();

                    try {
                        _outputWindow.WriteLine(Strings.CheckingTemplateUpdateStarted.FormatUI(template.Name, template.RemoteUrl));

                        var available = await _installedSource.CheckForUpdateAsync(template.RemoteUrl);

                        if (available.HasValue) {
                            _outputWindow.WriteLine(available.Value ? Strings.CheckingTemplateUpdateFound : Strings.CheckingTemplateUpdateNotFound);
                        } else {
                            _outputWindow.WriteLine(Strings.CheckingTemplateUpdateInconclusive);
                        }

                        var installed = Installed.Templates.OfType<TemplateViewModel>().SingleOrDefault(vm => vm.RemoteUrl == template.RemoteUrl);
                        if (installed != null) {
                            installed.IsUpdateAvailable = available == true;
                        }
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        if (!anyError) {
                            _outputWindow.ShowAndActivate();
                        }

                        anyError = true;

                        _outputWindow.WriteErrorLine(ex.Message);
                        _outputWindow.WriteLine(Strings.CheckingTemplateUpdateError);
                    }
                }

                CheckingUpdateStatus = anyError ? OperationStatus.Failed : OperationStatus.Succeeded;

                _outputWindow.WriteLine(anyError ? Strings.CheckingForAllUpdatesFailed : Strings.CheckingForAllUpdatesSuccess);

                ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.CheckUpdate, (!anyError).ToString());
            } catch (OperationCanceledException) {
                CheckingUpdateStatus = OperationStatus.Canceled;
                _outputWindow.WriteLine(Strings.CheckingForAllUpdatesCanceled);
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

        public void Reset() {
            ContextItems.Clear();

            ResetStatus();

            _templateLocalFolderPath = null;

            HomeClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ResetStatus() {
            CloningStatus = OperationStatus.NotStarted;
            LoadingStatus = OperationStatus.NotStarted;
            CreatingStatus = OperationStatus.NotStarted;
            CheckingUpdateStatus = OperationStatus.NotStarted;
            UpdatingStatus = OperationStatus.NotStarted;
        }

        public async Task CreateFilesAsync() {
            var selection = SelectedTemplate;
            Debug.Assert(selection != null);
            if (selection == null) {
                throw new InvalidOperationException("CreateFilesAsync called with null SelectedTemplate");
            }

            ResetStatus();

            CreatingStatus = OperationStatus.InProgress;
            OpenInExplorerFolderPath = null;

            try {
                var contextFilePath = Path.GetTempFileName();
                SaveUserInput(contextFilePath);

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(Strings.RunningTemplateStarted.FormatUI(selection.DisplayName));

                await _cutterClient.GenerateProjectAsync(_templateLocalFolderPath, UserConfigFilePath, contextFilePath, OutputFolderPath);

                try {
                    File.Delete(contextFilePath);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }

                OpenInExplorerFolderPath = OutputFolderPath;

                Reset();

                CreatingStatus = OperationStatus.Succeeded;

                _outputWindow.WriteLine(Strings.RunningTemplateSuccess.FormatUI(selection.DisplayName, OutputFolderPath));

                ReportTemplateEvent(CookiecutterTelemetry.TelemetryArea.Template, CookiecutterTelemetry.TemplateEvents.Run, selection);
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

        public void NavigateToHelp() {
            Process.Start(UrlConstants.HelpUrl)?.Dispose();
        }

        public void OpenFolderInExplorer(string path) {
            _openFolder(path);
        }

        public async Task SelectTemplate(TemplateViewModel template) {
            SelectedTemplate = template;
            await RefreshSelectedDescriptionAsync(template);
        }

        public async Task LoadMoreTemplates(string continuationToken) {
            var last = GitHub.Templates.LastOrDefault();
            if (last is ContinuationViewModel) {
                _templateRefreshCancelTokenSource?.Cancel();
                _templateRefreshCancelTokenSource = new CancellationTokenSource();
                try {
                    GitHub.Templates.Remove(last);
                    ReportEvent(CookiecutterTelemetry.TelemetryArea.Search, CookiecutterTelemetry.SearchEvents.More);
                    await AddFromSource(_githubSource, null, KnownMonikers.GitNoColor, GitHub, _templateRefreshCancelTokenSource.Token, continuationToken);
                } catch (OperationCanceledException) {
                }
            }
        }

        private async Task<bool> EnsureCookiecutterIsInstalled() {
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

        private async Task AddFromSource(ITemplateSource source, string searchTerm, VisualStudio.Imaging.Interop.ImageMoniker image, CategorizedViewModel parent, CancellationToken ct, string continuationToken = null) {
            var loading = new LoadingViewModel();
            parent.Templates.Add(loading);

            try {
                var result = await source.GetTemplatesAsync(searchTerm, continuationToken, ct);
                foreach (var t in result.Templates) {
                    ct.ThrowIfCancellationRequested();

                    var vm = new TemplateViewModel();
                    vm.DisplayName = t.Name;
                    vm.Description = t.Description;
                    vm.RemoteUrl = t.RemoteUrl;
                    vm.ClonedPath = t.LocalFolderPath;
                    vm.IsUpdateAvailable = t.UpdateAvailable == true;
                    vm.Image = image;
                    parent.Templates.Add(vm);
                }

                ct.ThrowIfCancellationRequested();

                if (result.ContinuationToken != null) {
                    parent.Templates.Add(new ContinuationViewModel(result.ContinuationToken));
                }
            } catch (TemplateEnumerationException ex) {
                var template = new ErrorViewModel() {
                    ErrorDescription = ex.Message,
                    ErrorDetails = ex.InnerException?.Message,
                };
                parent.Templates.Add(template);
            } finally {
                parent.Templates.Remove(loading);
            }
        }

        private async Task RefreshContextAsync(TemplateViewModel selection) {
            if (!await EnsureCookiecutterIsInstalled()) {
                return;
            }

            try {
                LoadingStatus = OperationStatus.InProgress;

                _outputWindow.ShowAndActivate();
                _outputWindow.WriteLine(Strings.LoadingTemplateStarted.FormatUI(selection.DisplayName));

                var result = await _cutterClient.LoadContextAsync(selection.ClonedPath, UserConfigFilePath);

                ContextItems.Clear();
                foreach (var item in result) {
                    ContextItems.Add(new ContextItemViewModel(item.Name, item.Selector, item.Description, item.DefaultValue, item.Values));
                }

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

        private async Task RefreshSelectedDescriptionAsync(TemplateViewModel selection) {
            if (selection == null) {
                SelectedDescription = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(selection.Description)) {
                await InitializeDescription(selection);
            }

            SelectedDescription = selection.Description ?? string.Empty;
        }

        private async Task InitializeDescription(TemplateViewModel selection) {
            if (!string.IsNullOrEmpty(selection.RemoteUrl)) {
                try {
                    var repo = await _githubClient.GetDescription(selection.RepositoryOwner, selection.RepositoryName);
                    selection.Description = repo.Description;
                } catch (WebException) {
                }
            } else {
                selection.Description = string.Empty;
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

                var repoUrl = selection.RemoteUrl?.ToLowerInvariant();
                var repoFullName = selection.RepositoryFullName?.ToLowerInvariant();
                var repoOwner = selection.RepositoryOwner?.ToLowerInvariant();
                var repoName = selection.RepositoryName?.ToLowerInvariant();

                var obj = new {
                    Success = error == null,
                    RepoUrl = repoUrl?.GetSha512(),
                    RepoFullName = repoFullName?.GetSha512(),
                    RepoOwner = repoOwner?.GetSha512(),
                    RepoName = repoName?.GetSha512(),
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
