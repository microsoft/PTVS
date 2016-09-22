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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
using Microsoft.VisualStudio.Imaging;
using Newtonsoft.Json;

namespace Microsoft.CookiecutterTools.ViewModel {
    class CookiecutterViewModel : INotifyPropertyChanged {
        private readonly ICookiecutterClient _cutterClient;
        private readonly IGitHubClient _githubClient;
        private readonly IGitClient _gitClient;
        private readonly Redirector _outputWindow;
        private readonly Action<string> _openFolder;

        public static readonly ICommand LoadMore = new RoutedCommand();
        public static readonly ICommand OpenInBrowser = new RoutedCommand();
        public static readonly ICommand RunSelection = new RoutedCommand();
        public static readonly ICommand Search = new RoutedCommand();
        public static readonly ICommand CreateFilesCommand = new RoutedCommand();
        public static readonly ICommand HomeCommand = new RoutedCommand();

        private string _searchTerm;
        private string _outputFolderPath;
        private string _selectedDescription;
        private string _selectedLocation;
        private bool _isInstalling;
        private bool _isInstallingSuccess;
        private bool _isInstallingError;
        private bool _isCloning;
        private bool _isCloningSuccess;
        private bool _isCloningError;
        private bool _isLoading;
        private bool _isLoadingSuccess;
        private bool _isLoadingError;
        private bool _isCreating;
        private bool _isCreatingSuccess;
        private bool _isCreatingError;
        private TemplateViewModel _selectedTemplate;
        private CancellationTokenSource _templateRefreshCancelTokenSource;

        private ITemplateSource _recommendedSource;
        private ITemplateSource _installedSource;
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

        public CookiecutterViewModel(ICookiecutterClient cutter, IGitHubClient githubClient, IGitClient gitClient, Redirector outputWindow, ITemplateSource installedTemplateSource, ITemplateSource feedTemplateSource, ITemplateSource gitHubTemplateSource, Action<string> openFolder) {
            _cutterClient = cutter;
            _githubClient = githubClient;
            _gitClient = gitClient;
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

        public bool IsInstalling {
            get {
                return _isInstalling;
            }

            set {
                if (value != _isInstalling) {
                    _isInstalling = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalling)));
                }
            }
        }

        public bool IsInstallingSuccess {
            get {
                return _isInstallingSuccess;
            }

            set {
                if (value != _isInstallingSuccess) {
                    _isInstallingSuccess = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstallingSuccess)));
                }
            }
        }

        public bool IsInstallingError {
            get {
                return _isInstallingError;
            }

            set {
                if (value != _isInstallingError) {
                    _isInstallingError = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstallingError)));
                }
            }
        }

        public bool IsCloning {
            get {
                return _isCloning;
            }

            set {
                if (value != _isCloning) {
                    _isCloning = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCloning)));
                }
            }
        }

        public bool IsCloningSuccess {
            get {
                return _isCloningSuccess;
            }

            set {
                if (value != _isCloningSuccess) {
                    _isCloningSuccess = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCloningSuccess)));
                }
            }
        }

        public bool IsCloningError {
            get {
                return _isCloningError;
            }

            set {
                if (value != _isCloningError) {
                    _isCloningError = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCloningError)));
                }
            }
        }

        public bool IsLoading {
            get {
                return _isLoading;
            }

            set {
                if (value != _isLoading) {
                    _isLoading = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                }
            }
        }

        public bool IsLoadingSuccess {
            get {
                return _isLoadingSuccess;
            }

            set {
                if (value != _isLoadingSuccess) {
                    _isLoadingSuccess = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoadingSuccess)));
                }
            }
        }

        public bool IsLoadingError {
            get {
                return _isLoadingError;
            }

            set {
                if (value != _isLoadingError) {
                    _isLoadingError = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoadingError)));
                }
            }
        }

        public bool IsCreating {
            get {
                return _isCreating;
            }

            set {
                if (value != _isCreating) {
                    _isCreating = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCreating)));
                }
            }
        }

        public bool IsCreatingSuccess {
            get {
                return _isCreatingSuccess;
            }

            set {
                if (value != _isCreatingSuccess) {
                    _isCreatingSuccess = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCreatingSuccess)));
                }
            }
        }

        public bool IsCreatingError {
            get {
                return _isCreatingError;
            }

            set {
                if (value != _isCreatingError) {
                    _isCreatingError = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCreatingError)));
                }
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
                    searchTermTemplate.Image = KnownMonikers.GitNoColor;
                    Custom.Templates.Add(searchTermTemplate);
                    SearchResults.Add(Custom);
                    return;
                } else if (Directory.Exists(searchTerm)) {
                    searchTermTemplate.DisplayName = searchTerm;
                    searchTermTemplate.ClonedPath = searchTerm;
                    searchTermTemplate.Image = KnownMonikers.TestSuite;
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

        public Task DeleteTemplateAsync(TemplateViewModel template) {
            try {
                string remote = template.RemoteUrl;

                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.DeletingTemplateStarted, template.ClonedPath));

                ShellUtils.DeleteDirectory(template.ClonedPath);

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.DeletingTemplateSuccess, template.ClonedPath));
                _outputWindow.ShowAndActivate();

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

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.DeletingTemplateFailed, template.ClonedPath));
                _outputWindow.ShowAndActivate();
            }

            return Task.CompletedTask;
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

            if (IsCloneNeeded(selection)) {
                IsCloning = true;
                IsCloningSuccess = false;
                IsCloningError = false;

                try {
                    _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.CloningTemplateStarted, SelectedTemplate.DisplayName));

                    Directory.CreateDirectory(InstalledFolderPath);

                    var result = await _gitClient.CloneAsync(selection.RemoteUrl, InstalledFolderPath);
                    selection.ClonedPath = result.Item1;

                    IsCloning = false;
                    IsCloningSuccess = true;
                    IsCloningError = false;

                    _outputWindow.WriteLine(string.Join(Environment.NewLine, result.Item2.StandardOutputLines));
                    _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, result.Item2.StandardErrorLines));

                    _outputWindow.WriteLine(string.Empty);
                    _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.CloningTemplateSuccess, SelectedTemplate.DisplayName, selection.ClonedPath));
                    _outputWindow.ShowAndActivate();

                    // We now have a new template installed, so reload that section of the results
                    _installedSource.InvalidateCache();

                    _templateRefreshCancelTokenSource?.Cancel();
                    _templateRefreshCancelTokenSource = new CancellationTokenSource();
                    try {
                        Installed.Templates.Clear();
                        await AddFromSource(_installedSource, SearchTerm, KnownMonikers.TestSuite, Installed, CancellationToken.None);
                    } catch (OperationCanceledException) {
                    }

                    _templateLocalFolderPath = selection.ClonedPath;

                    await RefreshContextAsync(selection);
                } catch (ProcessException ex) {
                    IsCloning = false;
                    IsCloningSuccess = false;
                    IsCloningError = true;

                    _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.ProcessExitCodeMessage, ex.Result.ExeFileName, ex.Result.ExitCode));
                    _outputWindow.WriteLine(string.Join(Environment.NewLine, ex.Result.StandardOutputLines));
                    _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, ex.Result.StandardErrorLines));

                    _outputWindow.WriteLine(string.Empty);
                    _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.CloningTemplateFailed, SelectedTemplate.DisplayName));
                    _outputWindow.ShowAndActivate();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    IsCloning = false;
                    IsCloningSuccess = false;
                    IsCloningError = true;

                    _outputWindow.WriteErrorLine(ex.Message);

                    _outputWindow.WriteLine(string.Empty);
                    _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.CloningTemplateFailed, SelectedTemplate.DisplayName));
                    _outputWindow.ShowAndActivate();
                }
            } else {
                Debug.Assert(!string.IsNullOrEmpty(selection.ClonedPath));
                _templateLocalFolderPath = selection.ClonedPath;

                await RefreshContextAsync(selection);
            }
        }

        public void Reset() {
            ContextItems.Clear();

            IsCloning = false;
            IsCloningSuccess = false;
            IsCloningError = false;

            IsLoading = false;
            IsLoadingSuccess = false;
            IsLoadingError = false;

            IsCreating = false;
            IsCreatingSuccess = false;
            IsCreatingError = false;

            _templateLocalFolderPath = null;

            HomeClicked?.Invoke(this, EventArgs.Empty);
        }

        public async Task CreateFilesAsync() {
            IsCloning = false;
            IsCloningError = false;
            IsCloningSuccess = false;

            IsCreating = true;
            IsCreatingError = false;
            IsCreatingSuccess = false;

            try {
                var contextFilePath = Path.GetTempFileName();
                SaveUserInput(contextFilePath);

                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.RunningTemplateStarted, SelectedTemplate.DisplayName));

                var result = await _cutterClient.GenerateProjectAsync(_templateLocalFolderPath, UserConfigFilePath, contextFilePath, OutputFolderPath);

                try {
                    File.Delete(contextFilePath);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }

                IsCreating = false;
                IsCreatingSuccess = true;
                IsCreatingError = false;

                _outputWindow.WriteLine(string.Join(Environment.NewLine, result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, result.StandardErrorLines));

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.RunningTemplateSuccess, SelectedTemplate.DisplayName, OutputFolderPath));
                _outputWindow.ShowAndActivate();

                if (_openFolder != null) {
                    _openFolder(OutputFolderPath);
                }
            } catch (ProcessException ex) {
                IsCreating = false;
                IsCreatingSuccess = false;
                IsCreatingError = true;

                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.ProcessExitCodeMessage, ex.Result.ExeFileName, ex.Result.ExitCode));
                _outputWindow.WriteLine(string.Join(Environment.NewLine, ex.Result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, ex.Result.StandardErrorLines));

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.RunningTemplateFailed, SelectedTemplate.DisplayName));
                _outputWindow.ShowAndActivate();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                IsCreating = false;
                IsCreatingSuccess = false;
                IsCreatingError = true;

                _outputWindow.WriteErrorLine(ex.Message);

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.RunningTemplateFailed, SelectedTemplate.DisplayName));
                _outputWindow.ShowAndActivate();
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
                    await AddFromSource(_githubSource, null, KnownMonikers.GitNoColor, GitHub, _templateRefreshCancelTokenSource.Token, continuationToken);
                } catch (OperationCanceledException) {
                }
            }
        }

        private async Task<bool> EnsureCookiecutterIsInstalled() {
            if (await _cutterClient.IsCookiecutterInstalled()) {
                return true;
            }

            IsInstalling = true;
            IsInstallingSuccess = false;
            IsInstallingError = false;

            try {
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.InstallingCookiecutterStarted));

                var result = await _cutterClient.CreateCookiecutterEnv();
                _outputWindow.WriteLine(string.Join(Environment.NewLine, result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, result.StandardErrorLines));

                result = await _cutterClient.InstallPackage();
                _outputWindow.WriteLine(string.Join(Environment.NewLine, result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, result.StandardErrorLines));

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.InstallingCookiecutterSuccess));
                _outputWindow.ShowAndActivate();

                IsInstalling = false;
                IsInstallingSuccess = true;
                IsInstallingError = false;

                return true;
            } catch (ProcessException ex) {
                IsInstalling = false;
                IsInstallingSuccess = false;
                IsInstallingError = true;

                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.ProcessExitCodeMessage, ex.Result.ExeFileName, ex.Result.ExitCode));
                _outputWindow.WriteLine(string.Join(Environment.NewLine, ex.Result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, ex.Result.StandardErrorLines));

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.InstallingCookiecutterFailed));
                _outputWindow.ShowAndActivate();

                return false;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                IsInstalling = false;
                IsInstallingSuccess = false;
                IsInstallingError = true;

                _outputWindow.WriteErrorLine(ex.Message);

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.InstallingCookiecutterFailed));
                _outputWindow.ShowAndActivate();

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
                IsLoading = true;
                IsLoadingSuccess = false;
                IsLoadingError = false;


                var result = await _cutterClient.LoadContextAsync(selection.ClonedPath, UserConfigFilePath);

                ContextItems.Clear();
                foreach (var item in result.Item1) {
                    ContextItems.Add(new ContextItemViewModel(item.Name, item.DefaultValue, item.Values));
                }

                IsLoading = false;
                IsLoadingSuccess = true;
                IsLoadingError = false;

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.LoadingTemplateSuccess, SelectedTemplate.DisplayName));
                _outputWindow.ShowAndActivate();

                // Go to the context page
                ContextLoaded?.Invoke(this, EventArgs.Empty);
            } catch (InvalidOperationException ex) {
                IsLoading = false;
                IsLoadingSuccess = false;
                IsLoadingError = true;

                _outputWindow.WriteErrorLine(ex.Message);

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.LoadingTemplateFailed, SelectedTemplate.DisplayName));
                _outputWindow.ShowAndActivate();
            } catch (ProcessException ex) {
                IsLoading = false;
                IsLoadingSuccess = false;
                IsLoadingError = true;

                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.ProcessExitCodeMessage, ex.Result.ExeFileName, ex.Result.ExitCode));
                _outputWindow.WriteLine(string.Join(Environment.NewLine, ex.Result.StandardOutputLines));
                _outputWindow.WriteErrorLine(string.Join(Environment.NewLine, ex.Result.StandardErrorLines));

                _outputWindow.WriteLine(string.Empty);
                _outputWindow.WriteLine(string.Format(CultureInfo.CurrentUICulture, Strings.LoadingTemplateFailed, SelectedTemplate.DisplayName));
                _outputWindow.ShowAndActivate();
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
    }
}
