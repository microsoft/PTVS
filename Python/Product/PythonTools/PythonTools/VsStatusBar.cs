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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Common.Wpf.Extensions;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using StatusBarControl = System.Windows.Controls.Primitives.StatusBar;

namespace Microsoft.PythonTools {
    class VsStatusBar {
        private readonly IServiceProvider _serviceProvider;
        private readonly IdleManager _idleManager;
        private ItemsControl _itemsControl;
        private bool _onIdleScheduled;

        public VsStatusBar(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _idleManager = new IdleManager(serviceProvider);
        }

        public IDisposable AddItem(UIElement element) {
            EnsureItemsControlCreated();

            _itemsControl.Items.Insert(0, element);
            return Disposable.Create(() => _serviceProvider.GetUIThread().Invoke(() => _itemsControl.Items.Remove(element)));
        }

        private Visual GetRootVisual() {
            var shell = _serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            shell.GetDialogOwnerHwnd(out IntPtr window);
            if (window == IntPtr.Zero) {
                return null;
            }

            var hwndSource = HwndSource.FromHwnd(window);
            return hwndSource?.RootVisual;
        }

        private bool TryAddItemsControlToVisualRoot() {
            if (_itemsControl.Parent != null) {
                return true;
            }

            // Note that the visual root may be wrong initialy due to Get to Code dialog
            // so don't cache it, otherwise we may never find the status bar.
            var visualRoot = GetRootVisual();
            if (visualRoot == null) {
                return false;
            }

            var resizeGrip = visualRoot.FindFirstVisualChildBreadthFirst<ResizeGrip>();
            var statusBarPanel = resizeGrip?.Parent as DockPanel;
            if (statusBarPanel == null) {
                return false;
            }

            var taskStatusCenterHost = statusBarPanel.FindName("PART_TaskStatusCenterHost") as FrameworkElement;
            if (taskStatusCenterHost == null) {
                return false;
            }

            // Position it between the task status center and the standard status bar text
            DockPanel.SetDock(_itemsControl, Dock.Left);
            var taskStatusCenterIndex = statusBarPanel.Children.IndexOf(taskStatusCenterHost);
            if (taskStatusCenterIndex >= 0) {
                statusBarPanel.Children.Insert(taskStatusCenterIndex + 1, _itemsControl);
            }

            return true;
        }

        private void EnsureItemsControlCreated() {
            if (_itemsControl == null) {
                var frameworkElementFactory = new FrameworkElementFactory(typeof(StackPanel));
                frameworkElementFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                _itemsControl = new ItemsControl { ItemsPanel = new ItemsPanelTemplate(frameworkElementFactory) };
            }

            if (!TryAddItemsControlToVisualRoot() && !_onIdleScheduled) {
                _idleManager.OnIdle += OnIdle;
                _onIdleScheduled = true;
            }
        }

        private void OnIdle(object sender, ComponentManagerEventArgs e) {
            _idleManager.OnIdle -= OnIdle;
            if (!TryAddItemsControlToVisualRoot()) {
                _idleManager.OnIdle += OnIdle;
            }
        }
    }
}
