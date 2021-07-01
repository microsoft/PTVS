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
    sealed class AddInstalledEnvironmentView : EnvironmentViewBase
    {
        private readonly IPythonToolsLogger _logger;
        private readonly IVsSetupCompositionService _setupService;
        private readonly IVsTrackProjectRetargeting2 _retargeting;

        public AddInstalledEnvironmentView(
            IServiceProvider serviceProvider,
            ProjectView[] projects,
            ProjectView selectedProject
        ) : base(serviceProvider, projects, selectedProject)
        {
            _logger = Site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
            PageName = Strings.AddInstalledEnvironmentTabHeader;
            AcceptCaption = Strings.AddInstalledEnvironmentInstallButton;
            AcceptAutomationName = Strings.AddInstalledEnvironmentInstallButtonAutomationName;
            IsAcceptShieldVisible = true;

            _setupService = Site.GetService(typeof(SVsSetupCompositionService)) as IVsSetupCompositionService;
            _retargeting = Site.GetService(typeof(SVsTrackProjectRetargeting)) as IVsTrackProjectRetargeting2;

            var packages = GetPackages(_setupService)
                .Where(p => p.PackageId.StartsWithOrdinal("Component.CPython"))
                .Select(p => new SetupPackageView(
                    p.PackageId,
                    p.Title,
                    p.CurrentState == (uint)__VsSetupPackageState.INSTALL_PACKAGE_PRESENT,
                    RefreshAcceptButton
            ));

            AvailablePackages = new ObservableCollection<object>(packages.Where(p => !p.Installed));
            if (!AvailablePackages.Any())
            {
                AvailablePackages.Add(new SetupPackageNoneView(Strings.AddInstalledEnvironmentNoneAvailable));
            }

            InstalledPackages = new ObservableCollection<object>(packages.Where(p => p.Installed));
            if (!InstalledPackages.Any())
            {
                InstalledPackages.Add(new SetupPackageNoneView(Strings.AddInstalledEnvironmentNone));
            }

            RefreshAcceptButton();
        }

        public ObservableCollection<object> InstalledPackages { get; }

        public ObservableCollection<object> AvailablePackages { get; }

        private void RefreshAcceptButton()
        {
            IsAcceptEnabled = AvailablePackages?.OfType<SetupPackageView>().Any(p => p.IsChecked) ?? false;
        }

        private static IVsSetupPackageInfo[] GetPackages(IVsSetupCompositionService setupService)
        {
            if (setupService != null)
            {
                // Get the count
                setupService.GetSetupPackagesInfo(0, null, out uint count);

                // Get the data
                var buffer = new IVsSetupPackageInfo[count];
                setupService.GetSetupPackagesInfo(count, buffer, out uint actual);

                // Extra safety in case count changed between the 2 calls
                return buffer.Take((int)Math.Min(actual, count)).ToArray();
            }
            else
            {
                return new IVsSetupPackageInfo[0];
            }
        }

        public override Task ApplyAsync()
        {
            _logger?.LogEvent(PythonLogEvent.InstallEnv, null);

            var ids = AvailablePackages.OfType<SetupPackageView>().Where(p => p.IsChecked).Select(p => p.PackageId).ToArray();
            if (ids.Length > 0)
            {
                IVsProjectAcquisitionSetupDriver driver;
                if (_retargeting != null &&
                    ErrorHandler.Succeeded(_retargeting.GetSetupDriver(VSConstants.SetupDrivers.SetupDriver_VS, out driver)) &&
                    driver != null)
                {
                    var task = driver.Install(ids);
                    if (task != null)
                    {
                        task.Start();
                    }
                }
            }
            else
            {
                Debug.Fail("Accept button should have been disabled");
            }

            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return Strings.AddInstalledEnvironmentTabHeader;
        }
    }

    sealed class SetupPackageView : DependencyObject
    {
        public SetupPackageView(string packageId, string title, bool installed, Action isCheckedChanged)
        {
            PackageId = packageId;
            Title = title;
            Installed = installed;
            IsCheckedChanged = isCheckedChanged;
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(SetupPackageView), new PropertyMetadata(false, IsChecked_Changed));

        public bool IsChecked
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public bool Installed { get; set; }

        public string Title { get; set; }

        public string PackageId { get; set; }

        private Action IsCheckedChanged { get; }

        private static void IsChecked_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SetupPackageView)d).IsCheckedChanged?.Invoke();
        }

        public override string ToString()
        {
            return Title;
        }
    }

    sealed class SetupPackageNoneView : DependencyObject
    {
        public SetupPackageNoneView(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    sealed class SetupPackageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PackageView { get; set; }

        public DataTemplate NoneView { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is SetupPackageView)
            {
                return PackageView;
            }
            else if (item is SetupPackageNoneView)
            {
                return NoneView;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
