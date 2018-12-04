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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.CookiecutterTools.Model;
using Microsoft.Data.ConnectionUI;

namespace Microsoft.CookiecutterTools.View {
    /// <summary>
    /// Infrastructure class.
    /// </summary>
    public static class SqlCommands {
        private static readonly RoutedCommand _browseOdbc = new RoutedCommand();

        /// <summary>
        /// Displays UI to edit an ODBC connection string and sets the TextBox that
        /// is specified as the CommandTarget to the selected path.
        /// </summary>
        public static RoutedCommand BrowseOdbc { get { return _browseOdbc; } }

        /// <summary>
        /// Handles the CanExecute event for all commands defined in this class.
        /// </summary>
        public static void CanExecute(Window window, object sender, CanExecuteRoutedEventArgs e) {
            if (e.Command == BrowseOdbc) {
                e.CanExecute = e.OriginalSource is TextBox;
            }
        }

        /// <summary>
        /// Handles the Executed event for all commands defined in this class.
        /// </summary>
        public static void Executed(Window window, object sender, ExecutedRoutedEventArgs e) {
            if (e.Command == BrowseOdbc) {
                BrowseOdbcExecute(window, e);
            }
        }

        private const string DefaultSqlConnectionString = "Data Source=(local);Integrated Security=true";

        private static void BrowseOdbcExecute(Window window, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            var odbc = tb.GetValue(TextBox.TextProperty) as string;

            odbc = EditConnectionString(odbc);

            if (odbc != null) {
                tb.SetCurrentValue(TextBox.TextProperty, odbc);
                var binding = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                if (binding != null) {
                    binding.UpdateSource();
                }
            }
        }

        private static string EditConnectionString(string odbcConnectionString) {
            var sqlConnectionString = odbcConnectionString.OdbcToSqlClient();

            do {
                using (var dlg = new DataConnectionDialog()) {
                    DataSource.AddStandardDataSources(dlg);
                    dlg.SelectedDataSource = DataSource.SqlDataSource;
                    dlg.SelectedDataProvider = DataProvider.SqlDataProvider;
                    try {
                        dlg.ConnectionString = sqlConnectionString ?? DefaultSqlConnectionString;
                        var result = DataConnectionDialog.Show(dlg);
                        switch (result) {
                            case System.Windows.Forms.DialogResult.Cancel:
                                return null;
                            case System.Windows.Forms.DialogResult.OK:
                                return dlg.ConnectionString.SqlClientToOdbc();
                        }
                    } catch (ArgumentException) {
                        if (MessageBox.Show(Strings.ConnectionStringFormatIncorrect, Strings.ProductTitle, MessageBoxButton.YesNo) == MessageBoxResult.No) {
                            return odbcConnectionString;
                        }
                        sqlConnectionString = DefaultSqlConnectionString;
                    } catch (InvalidOperationException ex) {
                        MessageBox.Show(ex.Message);
                        break;
                    }
                }
            } while (true);

            return null;
        }
    }
}
