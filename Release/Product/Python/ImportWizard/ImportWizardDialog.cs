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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.ImportWizard {
    partial class ImportWizardDialog : Form {
        private IServiceProvider _provider;

        public static ImportSettings ShowImportDialog(IServiceProvider provider) {
            using (var dialog = new ImportWizardDialog(provider)) {
                var result = dialog.ShowDialog();

                if (result != DialogResult.OK) {
                    return null;
                }

                var settings = new ImportSettings {
                    SourceFilesPath = dialog.sourcePathTextBox.Text,
                    Filter = dialog.filterTextBox.Text,
                    SearchPaths = dialog.searchPathTextBox.Lines
                };

                var interp = dialog.interpreterCombo.SelectedItem as InterpreterItem;
                if (interp != null) {
                    settings.InterpreterId = interp.Id.ToString();
                    settings.InterpreterVersion = interp.Version;
                }

                var startupFile = dialog.startupFileList.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
                if (startupFile != null) {
                    settings.StartupFile = startupFile.Name;
                }

                return settings;
            }
        }

        private class InterpreterItem {
            public string Display;
            public Guid Id;
            public string Version;

            public InterpreterItem() { }

            public InterpreterItem(IPythonInterpreterFactory interpreter) {
                var version = interpreter.Configuration.Version;
                if (version.Revision == 0) {
                    if (version.Build == 0) {
                        if (version.Minor == 0) {
                            Version = version.Major.ToString();
                        } else {
                            Version = string.Format("{0}.{1}", version.Major, version.Minor);
                        }
                    } else {
                        Version = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
                    }
                } else {
                    Version = version.ToString();
                }

                Display = string.Format("{0} {1}", interpreter.Description, Version);
                Id = interpreter.Id;
            }

            public override string ToString() {
                return Display;
            }
        }

        public ImportWizardDialog(IServiceProvider provider) {
            _provider = provider;
            InitializeComponent();

            step1Panel.Dock = DockStyle.Fill;
            step2Panel.Dock = DockStyle.Fill;
            step1Panel.Visible = true;
            step2Panel.Visible = false;

            var interpreterList = new List<InterpreterItem>();
            interpreterCombo.Items.Add(new InterpreterItem { Display = "(Use my default)", Id = Guid.Empty });
            interpreterCombo.SelectedIndex = 0;

            try {
                var model = (IComponentModel)provider.GetService(typeof(SComponentModel));
                if (model != null) {
                    foreach (var factoryProvider in model.GetExtensions<IPythonInterpreterFactoryProvider>()) {
                        foreach (var interpreter in factoryProvider.GetInterpreterFactories()) {
                            interpreterList.Add(new InterpreterItem(interpreter));
                        }
                    }

                    interpreterCombo.Items.AddRange(interpreterList.OrderBy(t => t.Display).ToArray());
                }
            } catch (Exception ex) {
                Debug.Assert(false, "Creating interpreter list failed:\n" + ex.ToString());
            }

        }

        private bool ValidateContent(bool displayMessages) {
            var sourcePath = sourcePathTextBox.Text;
            var searchPaths = searchPathTextBox.Lines;

            if (string.IsNullOrEmpty(sourcePath)) {
                if (displayMessages) {
                    MessageBox.Show("A path with source files is required.", "Python Tools");
                }
                return false;
            } else if (!Directory.Exists(sourcePath)) {
                if (displayMessages) {
                    MessageBox.Show("The provided source path does not exist.", "Python Tools");
                } return false;
            }

            foreach (var p in searchPaths) {
                if (!Directory.Exists(p)) {
                    if (displayMessages) {
                        MessageBox.Show(string.Format("Search path \"{0}\" cannot be found.", p), "Python Tools");
                    } return false;
                }
            }

            return true;
        }

        protected override void OnClosing(CancelEventArgs e) {
            if (DialogResult == DialogResult.OK && !ValidateContent(true)) {
                e.Cancel = true;
            }

            base.OnClosing(e);
        }

        private string BrowseForDirectory(string initialDirectory) {
            IVsUIShell uiShell = _provider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (null == uiShell) {
                using (var ofd = new FolderBrowserDialog()) {
                    ofd.RootFolder = Environment.SpecialFolder.Desktop;
                    ofd.ShowNewFolderButton = false;
                    var result = ofd.ShowDialog(this);
                    if (result == DialogResult.OK) {
                        return ofd.SelectedPath;
                    } else {
                        return null;
                    }
                }
            }

            VSBROWSEINFOW[] browseInfo = new VSBROWSEINFOW[1];
            browseInfo[0].lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
            browseInfo[0].pwzInitialDir = initialDirectory;
            browseInfo[0].hwndOwner = Handle;
            browseInfo[0].nMaxDirName = 260;
            IntPtr pDirName = Marshal.AllocCoTaskMem(520);
            browseInfo[0].pwzDirName = pDirName;
            try {
                int hr = uiShell.GetDirectoryViaBrowseDlg(browseInfo);
                if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                    return null;
                }
                ErrorHandler.ThrowOnFailure(hr);
                return Marshal.PtrToStringAuto(browseInfo[0].pwzDirName);
            } finally {
                if (pDirName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(pDirName);
                }
            }
        }

        private void browsePathButton_Click(object sender, EventArgs e) {
            var path = BrowseForDirectory(sourcePathTextBox.Text);
            if (path != null) {
                sourcePathTextBox.Text = path;
            }
        }

        private void browseSearchPathButton_Click(object sender, EventArgs e) {
            var path = BrowseForDirectory(sourcePathTextBox.Text);
            if (path != null) {
                if (!string.IsNullOrWhiteSpace(searchPathTextBox.Lines.LastOrDefault())) {
                    searchPathTextBox.AppendText("\r\n");
                }
                searchPathTextBox.AppendText(path);
            }
        }

        private void backButton_Click(object sender, EventArgs e) {
            step1Panel.Visible = true;
            step2Panel.Visible = false;
            okButton.Visible = false;
            backButton.Visible = false;
            nextButton.Visible = true;
        }

        private void nextButton_Click(object sender, EventArgs e) {
            startupFileList.Items.Clear();
            try {
                foreach (var file in Directory.GetFiles(sourcePathTextBox.Text, "*.py", SearchOption.TopDirectoryOnly)) {
                    startupFileList.Items.Add(new ListViewItem {
                        Name = file,
                        Text = Path.GetFileName(file),
                        ImageKey = "PythonFile"
                    });
                }

                // Also include *.pyw files if they were in the filter list
                var pywFilters = filterTextBox.Text.Split(';').Where(filter => filter.TrimEnd().EndsWith(".pyw", StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var pywFilter in pywFilters) {
                    foreach (var file in Directory.GetFiles(sourcePathTextBox.Text, pywFilter, SearchOption.TopDirectoryOnly)) {
                        startupFileList.Items.Add(new ListViewItem {
                            Name = file,
                            Text = Path.GetFileName(file),
                            ImageKey = "PythonFile"
                        });
                    }
                }
            } catch (Exception ex) {
                Debug.Assert(false, "Creating startup file list failed:\n" + ex.ToString());
            }

            if (startupFileList.Items.Count == 0) {
                startupFileList.Items.Add(new ListViewItem { Text = "(No Python files found in project root)", ImageKey = null });
                startupFileList.Enabled = false;
            } else {
                startupFileList.Enabled = true;
            }

            step2Panel.Visible = true;
            step1Panel.Visible = false;
            okButton.Visible = true;
            backButton.Visible = true;
            nextButton.Visible = false;
        }

        private void startupFileList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
            if (e.Item != null && !string.IsNullOrEmpty(e.Item.Name)) {
                if (e.IsSelected) {
                    e.Item.ImageKey = "PythonStartupFile";
                } else {
                    e.Item.ImageKey = "PythonFile";
                }
            }
        }
    }
}
