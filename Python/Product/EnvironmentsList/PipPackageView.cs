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
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal sealed class PipPackageView : INotifyPropertyChanged {
        private readonly PipPackageCache _provider;
        private readonly string _name;
        private readonly Pep440Version _version;
        private Pep440Version? _upgradeVersion;
        private string _description;

        private static readonly Regex PipListOutputRegex = new Regex(
            @"^\s*(?<name>[^\s=:]+)\s+\((?<version>[^:]+)\)(\:(?<description>.*))?\s*$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1.0)
        );
        private static readonly Regex PipFreezeOutputRegex = new Regex(
            @"^\s*(?<name>[^\s=:]+)==(?<version>[^:]+)?(\:(?<description>.*))?\s*$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1.0)
        );
        private static readonly Regex NameAndDescriptionRegex = new Regex(
            @"^\s*(?<name>[^\s=:]+)\:(?<description>.*)\s*$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1.0)
        );

        internal PipPackageView(PipPackageCache provider, string name, string version, string description) {
            _provider = provider;
            _name = name ?? "";
            if (!string.IsNullOrEmpty(version)) {
                Pep440Version.TryParse(version, out _version);
            }
            _description = description ?? "";
        }

        internal PipPackageView(PipPackageCache provider, string packageSpec, bool versionIsInstalled = true) {
            _provider = provider;
            Match m;
            try {
                m = PipListOutputRegex.Match(packageSpec);
            } catch (RegexMatchTimeoutException) {
                Debug.Fail("Regex timeout");
                m = null;
            }
            if (m == null || !m.Success) {
                try {
                    m = PipFreezeOutputRegex.Match(packageSpec);
                } catch (RegexMatchTimeoutException) {
                    Debug.Fail("Regex timeout");
                    m = null;
                }
                if (m == null || !m.Success) {
                    try {
                        m = NameAndDescriptionRegex.Match(packageSpec);
                    } catch (RegexMatchTimeoutException) {
                        Debug.Fail("Regex timeout");
                        m = null;
                    }
                }
            }

            Pep440Version version;
            if (m.Success) {
                _name = m.Groups["name"].Value;
                Pep440Version.TryParse(m.Groups["version"].Value, out version);
            } else {
                _name = packageSpec;
                version = Pep440Version.Empty;
            }

            var description = m.Groups["description"].Value;
            if (!string.IsNullOrEmpty(description)) {
                _description = Uri.UnescapeDataString(description);
            }

            if (versionIsInstalled) {
                _version = version;
                _upgradeVersion = null;
            } else {
                _version = Pep440Version.Empty;
                _upgradeVersion = version;
            }
        }

        private async void SetUpgradeVersionAsync() {
            if (!_upgradeVersion.HasValue) {
                try {
                    await _provider.UpdatePackageInfoAsync(this, CancellationToken.None);
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                    Debug.Fail("Unhandled exception: " + ex.ToString());
                    // Nowhere else to report the exception, so just swallow it to
                    // avoid bringing down the whole process.
                }
            }
        }


        public string PackageSpec {
            get {
                return GetPackageSpec(false, false);
            }
        }

        public string GetPackageSpec(bool includeDescription, bool useUpgradeVersion) {
            var descr = string.Empty;
            if (includeDescription && !string.IsNullOrEmpty(_description)) {
                descr = ":" + Uri.EscapeDataString(_description);
            }

            var ver = useUpgradeVersion ? _upgradeVersion.GetValueOrDefault() : _version;
            if (ver.IsEmpty) {
                return _name + descr;
            } else {
                return string.Format("{0}=={1}{2}", _name, ver, descr);
            }
        }

        public string Name {
            get { return _name; }
        }

        public Pep440Version Version {
            get { return _version; }
        }

        public string DisplayName {
            get {
                if (_version.IsEmpty) {
                    return _name;
                } else {
                    return string.Format("{0} ({1})", _name, _version);
                }
            }
        }

        public string Description {
            get {

                return _description;
            }
            set {
                if (_description != value) {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public Pep440Version UpgradeVersion {
            get {
                if (!_upgradeVersion.HasValue) {
                    // Kick off the Get, but return empty. We will raise an
                    // event when the get completes.
                    SetUpgradeVersionAsync();
                }
                return _upgradeVersion.GetValueOrDefault();
            }
            set {
                if (!_upgradeVersion.HasValue || !_upgradeVersion.GetValueOrDefault().Equals(value)) {
                    _upgradeVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

}
