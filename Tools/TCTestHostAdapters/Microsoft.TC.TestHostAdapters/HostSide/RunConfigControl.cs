/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Vsip;
using Microsoft.TC.TestHostAdapters;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// UI control for my host adapter configuartion. Hosted inside test run config editor.
    /// It contains a data grid view where you could define environment variables.
    /// </summary>
    public sealed partial class RunConfigControl : UserControl, IRunConfigurationCustomHostEditor
    {
        #region Private
        private RunConfigData m_data;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        public RunConfigControl()
        {
            InitializeComponent();
        }
        #endregion

        #region IRunConfigurationEditor

        /// <summary>
        /// Initialize the editor to a default state based on given test run.
        /// </summary>
        /// <param name="serviceProvider">VS Service Provider.</param>
        /// <param name="run">Obselete. Always null.</param>
        void IRunConfigurationEditor.Initialize(System.IServiceProvider serviceProvider, TestRun run)
        {
            // Initialize to something like: 7.0, 7.1, 8.0, 9.0, 10.0
            foreach (string version in VsRegistry.GetVersions())
            {
                m_hiveCombo.Items.Add(version);
            }
        }

        /// <summary>
        /// Fire this event when data are modified in this editor.
        /// </summary>
        public event EventHandler DataGetDirty;

        /// <summary>
        /// Handle the event that core (non-host and not-test-specific) run config data are modified outside this editor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="dirtyEventArgs">contains run config object that is changed outside</param>
        void IRunConfigurationEditor.OnCommonDataDirty(object sender, CommonRunConfigurationDirtyEventArgs dirtyEventArgs)
        {
            // Out test config does not depend on other data contained in the run config
            // but for the case when nobody modifies our config we still want to have our default section in RC,
            // that's why when the user switches hosts to VS IDE and we did not exist we say we are dirty, and get data will return our data.
            if (m_data == null)
            {
                SetDirty();

                // Select 1st item
                if (m_hiveCombo.SelectedIndex < 0)
                {
                    m_hiveCombo.SelectedItem = VsRegistry.GetDefaultVersion();
                }
            }
        }

        /// <summary>
        /// Desciption about this editor is displayed in the help panel of main run config editor.
        /// </summary>
        string IRunConfigurationEditor.Description
        {
            get
            {
                return Resources.HostAdapterDescription;
            }
        }

        /// <summary>
        /// The keyword that is hooked up with the help topic.
        /// </summary>
        string IRunConfigurationEditor.HelpKeyword
        {
            get
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Verify the data in the editor. Prompt the user when neccessary.
        /// </summary>
        /// <returns>true if the data are correct and don't need correction; otherwise, false.</returns>
        bool IRunConfigurationEditor.VerifyData()
        {
            return true;
        }
        #endregion

        #region IRunConfigurationCustomHostEditor
        /// <summary>
        /// The host adapter type that this editor is used for.
        /// </summary>
        string IRunConfigurationCustomHostEditor.HostType
        {
            get { return Constants.VsIdeHostAdapterName; }
        }

        /// <summary>
        /// Called by the main editor to load the data into this control.
        /// </summary>
        /// <param name="data">Host specific data.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")] // Taking care of rightAlign is not important for this sample.
        void IRunConfigurationCustomHostEditor.SetData(IHostSpecificRunConfigurationData data)
        {
            string latestVersion = VsRegistry.GetDefaultVersion();  // Throws if VS is not installed.
            
            RunConfigData vsIdeHostData = data as RunConfigData;
            if (vsIdeHostData == null)
            {
                vsIdeHostData = new RunConfigData(VsRegistry.GetDefaultVersion());
            }
            else if (!m_hiveCombo.Items.Contains(vsIdeHostData.RegistryHive))
            {
                // If .testrunconfig file has VS version that we do not have in combobox,
                // show message box and use default version.
                MessageBox.Show(
                    this, 
                    string.Format(CultureInfo.InvariantCulture, Resources.WrongVSVersionPassedToRunConfigControl, 
                        vsIdeHostData.RegistryHive, latestVersion), 
                    Resources.MicrosoftVisualStudio);

                vsIdeHostData = new RunConfigData(latestVersion);
            }

            if ((object)m_data != (object)vsIdeHostData)
            {
                SetDirty();
            }

            // Set the data.
            m_data = vsIdeHostData;

            int selectedIndex = m_hiveCombo.Items.IndexOf(vsIdeHostData.RegistryHive);
            if (selectedIndex < 0)
            {
                selectedIndex = m_hiveCombo.Items.IndexOf(latestVersion);
                Debug.Assert(selectedIndex >= 0);
            }
            if (selectedIndex >= 0)
            {
                m_hiveCombo.SelectedIndex = selectedIndex;
            }
        }

        /// <summary>
        /// Main editor is asking the control for the current host specific data.
        /// </summary>
        /// <returns></returns>
        IHostSpecificRunConfigurationData IRunConfigurationCustomHostEditor.GetData()
        {
            if (m_data == null)
            {
                m_data = new RunConfigData(VsRegistry.GetDefaultVersion());
            }
            return m_data;
        }
        #endregion

        #region Private
        /// <summary>
        /// Set the "dirty" state for the control. I.e. it has modified data.
        /// </summary>
        private void SetDirty()
        {
            if (DataGetDirty != null)
            {
                DataGetDirty(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when Visual Studio Hive combobox value is changed.
        /// </summary>
        private void HiveCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Debug.Assert(m_data != null);

            // Set internal data from combo box.
            if (m_hiveCombo.SelectedItem != null && m_data != null)
            {
                string selectedHive = m_hiveCombo.SelectedItem.ToString();
                if (!string.Equals(m_data.RegistryHive, selectedHive, StringComparison.OrdinalIgnoreCase))
                {
                    m_data.RegistryHive = selectedHive;
                    SetDirty();
                }
            }
        }
        #endregion
    }
}
