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
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace Microsoft.PythonTools.Wpf {
    static class Commands {
        public static readonly RoutedCommand BrowseFolder = new RoutedCommand();
        public static readonly RoutedCommand BrowseOpenFile = new RoutedCommand();
        public static readonly RoutedCommand BrowseSaveFile = new RoutedCommand();

        public static void CanExecute(Window window, object sender, CanExecuteRoutedEventArgs e) {
            if (e.Command == BrowseFolder || e.Command == BrowseOpenFile || e.Command == BrowseSaveFile) {
                e.CanExecute = e.OriginalSource is TextBox;
            }
        }

        public static void Executed(Window window, object sender, ExecutedRoutedEventArgs e) {
            if (e.Command == BrowseFolder) {
                BrowseFolderExecute(window, e);
            } else if (e.Command == BrowseOpenFile) {
                BrowseOpenFileExecute(window, e);
            } else if (e.Command == BrowseSaveFile) {
                BrowseSaveFileExecute(window, e);
            }
        }

        private static void BrowseFolderExecute(Window window, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            if (!tb.AcceptsReturn) {
                var path = e.Parameter as string ?? tb.GetValue(TextBox.TextProperty) as string;
                while (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) {
                    path = Path.GetDirectoryName(path);
                }
                path = PythonToolsPackage.Instance.BrowseForDirectory(new WindowInteropHelper(window).Handle, path);
                if (path != null) {
                    tb.SetCurrentValue(TextBox.TextProperty, path);
                    var binding = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                    if (binding != null) {
                        binding.UpdateSource();
                    }
                }
            } else {
                var existing = tb.GetValue(TextBox.TextProperty) as string;
                var path = e.Parameter as string;
                while (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) {
                    path = Path.GetDirectoryName(path);
                }
                path = PythonToolsPackage.Instance.BrowseForDirectory(new WindowInteropHelper(window).Handle, path);
                if (path != null) {
                    if (string.IsNullOrEmpty(existing)) {
                        tb.SetCurrentValue(TextBox.TextProperty, path);
                    } else {
                        tb.SetCurrentValue(TextBox.TextProperty, existing.TrimEnd(new[] { '\r', '\n' }) + Environment.NewLine + path);
                    }
                    var binding = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                    if (binding != null) {
                        binding.UpdateSource();
                    }
                }
            }
        }

        private static void BrowseOpenFileExecute(Window window, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            var filter = (e.Parameter as string) ?? "All Files (*.*)|*.*";

            var path = tb.GetValue(TextBox.TextProperty) as string;
            path = PythonToolsPackage.Instance.BrowseForFileOpen(new WindowInteropHelper(window).Handle, filter, path);
            if (path != null) {
                tb.SetCurrentValue(TextBox.TextProperty, path);
                var binding = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                if (binding != null) {
                    binding.UpdateSource();
                }
            }
        }

        private static void BrowseSaveFileExecute(Window window, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            var filter = (e.Parameter as string) ?? "All Files (*.*)|*.*";

            var path = tb.GetValue(TextBox.TextProperty) as string;
            path = PythonToolsPackage.Instance.BrowseForFileSave(new WindowInteropHelper(window).Handle, filter, path);
            if (path != null) {
                tb.SetCurrentValue(TextBox.TextProperty, path);
                var binding = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                if (binding != null) {
                    binding.UpdateSource();
                }
            }
        }

    }
}
