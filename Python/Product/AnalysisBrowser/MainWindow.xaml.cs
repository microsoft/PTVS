/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Analysis.Browser {
    public partial class MainWindow : Window {
        public static readonly ICommand BrowseSaveCommand = new RoutedCommand();
        public static readonly ICommand GoToItemCommand = new RoutedCommand();

        public MainWindow() {
            InitializeComponent();

            var path = Environment.GetCommandLineArgs().LastOrDefault();
            try {
                if (Directory.Exists(path)) {
                    Load(path);
                }
            } catch {
            }
        }


        internal AnalysisView Analysis {
            get { return (AnalysisView)GetValue(AnalysisProperty); }
            private set { SetValue(AnalysisPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey AnalysisPropertyKey = DependencyProperty.RegisterReadOnly("Analysis", typeof(AnalysisView), typeof(MainWindow), new PropertyMetadata());
        public static readonly DependencyProperty AnalysisProperty = AnalysisPropertyKey.DependencyProperty;


        public bool HasAnalysis {
            get { return (bool)GetValue(HasAnalysisProperty); }
            private set { SetValue(HasAnalysisPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey HasAnalysisPropertyKey = DependencyProperty.RegisterReadOnly("HasAnalysis", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public static readonly DependencyProperty HasAnalysisProperty = HasAnalysisPropertyKey.DependencyProperty;

        public bool Loading {
            get { return (bool)GetValue(LoadingProperty); }
            private set { SetValue(LoadingPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey LoadingPropertyKey = DependencyProperty.RegisterReadOnly("Loading", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public static readonly DependencyProperty LoadingProperty = LoadingPropertyKey.DependencyProperty;


        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = !Loading;
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e) {
            string path;
            using (var bfd = new System.Windows.Forms.FolderBrowserDialog()) {
                bfd.RootFolder = Environment.SpecialFolder.Desktop;
                if (HasAnalysis) {
                    path = Analysis.Path;
                } else {
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Python Tools");
                }
                while (path.Length >= 4 && !Directory.Exists(path)) {
                    path = Path.GetDirectoryName(path);
                }
                if (path.Length <= 3) {
                    path = null;
                }
                bfd.SelectedPath = path;
                if (bfd.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) {
                    return;
                }
                path = bfd.SelectedPath;
            }

            Load(path);
        }

        private void Load(string path) {
            HasAnalysis = false;
            Loading = true;
            Analysis = null;
            Cursor = Cursors.Wait;
            Task.Factory.StartNew(() => {
                var av = new AnalysisView(path, new Version(2, 7));

                foreach (var mod in av.Modules) {
                    mod.Children.ToList();
                }

                return av;
            }, TaskCreationOptions.LongRunning).ContinueWith(t => {
                Analysis = t.Result;
                HasAnalysis = true;
                Loading = false;
                Cursor = Cursors.Arrow;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Export_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = HasAnalysis && !string.IsNullOrEmpty(ExportFilename.Text);
            if (e.Command == AnalysisView.ExportDiffableCommand) {
                // Not implemented yet
                e.CanExecute = false;
            }
        }

        private void Export_Executed(object sender, ExecutedRoutedEventArgs e) {
            e.Handled = true;

            var path = ExportFilename.Text;
            var filter = ExportFilter.Text;

            Cursor = Cursors.AppStarting;
            Task t = null;
            if (e.Command == AnalysisView.ExportTreeCommand) {
                t = Analysis.ExportTree(path, filter);
            } else if (e.Command == AnalysisView.ExportDiffableCommand) {
                t = Analysis.ExportDiffable(path, filter);
            }
            if (t != null) {
                t.ContinueWith(t2 => {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
                t.ContinueWith(t2 => {
                    Cursor = Cursors.Arrow;
                    if (t2.Exception != null) {
                        MessageBox.Show(string.Format("An error occurred while exporting:{0}{0}{1}",
                            Environment.NewLine,
                            t2.Exception));
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void BrowseSave_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Source is TextBox && e.Parameter is string;
        }

        private void BrowseSave_Executed(object sender, ExecutedRoutedEventArgs e) {
            using (var dialog = new System.Windows.Forms.SaveFileDialog()) {
                dialog.Filter = (string)e.Parameter;
                dialog.AutoUpgradeEnabled = true;
                var path = ((TextBox)e.Source).Text;
                try {
                    dialog.FileName = path;
                    dialog.InitialDirectory = Path.GetDirectoryName(path);
                } catch (ArgumentException) {
                    dialog.FileName = string.Empty;
                    dialog.InitialDirectory = Analysis.Path;
                }
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) {
                    return;
                }
                ((TextBox)e.Source).SetCurrentValue(TextBox.TextProperty, dialog.FileName);
            }
        }

        private void GoToItem_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is IAnalysisItemView;
        }

        private static Stack<TreeViewItem> SelectChild(TreeViewItem root, object value) {
            if (root == null) {
                return null;
            }

            if (root.DataContext == value) {
                var lst = new Stack<TreeViewItem>();
                lst.Push(root);
                return lst;
            }
            foreach (var child in root.Items
                .OfType<object>()
                .Select(i => (TreeViewItem)root.ItemContainerGenerator.ContainerFromItem(i))) {
                var lst = SelectChild(child, value);
                if (lst != null) {
                    lst.Push(root);
                    return lst;
                }
            }
            return null;
        }

        private static void SelectChild(TreeView tree, object value) {
            Stack<TreeViewItem> result = null;

            foreach (var item in tree.Items
                .OfType<object>()
                .Select(i => (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(i))) {
                if ((result = SelectChild(item, value)) != null) {
                    break;
                }
            }

            if (result != null) {
                while (result.Any()) {
                    var item = result.Pop();
                    item.IsExpanded = true;
                    item.Focus();
                }
            }
        }

        private void GoToItem_Executed(object sender, ExecutedRoutedEventArgs e) {
            Cursor = Cursors.Wait;
            try {
                SelectChild(DatabaseTreeView, e.Parameter);
            } finally {
                Cursor = Cursors.Arrow;
            }
        }

        private void Close_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }
    }

    class PropertyItemTemplateSelector : DataTemplateSelector {
        public DataTemplate Text { get; set; }
        public DataTemplate AnalysisItem { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (item is string) {
                return Text;
            } else if (item is IAnalysisItemView) {
                return AnalysisItem;
            }
            return null;
        }
    }
}
