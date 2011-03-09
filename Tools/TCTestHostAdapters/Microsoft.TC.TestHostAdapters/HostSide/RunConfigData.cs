/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Common.Xml;
using Microsoft.Win32;
using Microsoft.TC.TestHostAdapters;
using System.Xml;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// The data for this host type to extend Run Config with.
    /// - Registry Hive, like 10.0Exp.
    /// - Session id for debugging.
    /// </summary>
    [Serializable]
    internal class RunConfigData: IHostSpecificRunConfigurationData, IXmlTestStore, IXmlTestStoreCustom
    {
        #region Private
        private const string RegistryHiveAttributeName = "registryHive";
        private const string XmlNamespaceUri = "http://microsoft.com/schemas/TC/TCTestHostAdapters";
        private const string XmlElementName = "VsIdeTestHostRunConfig";

        /// <summary>
        /// The registry hive of VS to use for the VS instance to start.
        /// This field is persisted in the .TestRunConfig file.
        /// </summary>
        private string m_registryHive;
        #endregion

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="registryHive">The registry hive to use settings from for new Visual Studio instance.</param>
        internal RunConfigData(string registryHive)
        {
            m_registryHive = registryHive;  // null is OK. null means get latest version.
        }

        /// <summary>
        /// The description of this host to use in Run Config dialog.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]   // We have to implement interface.
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMethodsAsStatic")]
        public string RunConfigurationInformation
        {
            get { return Resources.RunConfigDataDescription; }
        }

        /// <summary>
        /// Implements ICloneable.Clone.
        /// </summary>
        public object Clone()
        {
            return new RunConfigData(m_registryHive);
        }

        /// <summary>
        /// The registry hive to use settings from for new Visual Studio instance.
        /// </summary>
        internal string RegistryHive
        {
            get { return m_registryHive; }
            set { m_registryHive = value; }
        }

        #region IXmlTestStore Members
        public void Load(XmlElement element, XmlTestStoreParameters parameters)
        {
            this.RegistryHive = element.GetAttribute(RegistryHiveAttributeName);
        }

        public void Save(XmlElement element, XmlTestStoreParameters parameters)
        {
            element.SetAttribute(RegistryHiveAttributeName, this.RegistryHive);
        }
        #endregion

        #region IXmlTestStoreCustom Members

        public string ElementName
        {
            get { return XmlElementName; }
        }

        public string NamespaceUri
        {
            get { return XmlNamespaceUri; }
        }

        #endregion
    }
}
