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

namespace Microsoft.PythonTools.EnvironmentsList
{
	internal sealed class PipPackageView : INotifyPropertyChanged
	{
		private readonly IPackageManager _provider;
		private readonly PackageSpec _package;
		private readonly bool _isInstalled;
		private PackageVersion? _upgradeVersion;

		internal PipPackageView(IPackageManager provider, PackageSpec package, bool isInstalled)
		{
			_provider = provider;
			_package = package;
			_isInstalled = isInstalled;
		}

		public override string ToString() => DisplayName;

		private async void TriggerUpdate()
		{
			if (_upgradeVersion.HasValue && !string.IsNullOrEmpty(_package.Description))
			{
				return;
			}
			if (_provider == null)
			{
				return;
			}

			try
			{
				var p = await _provider.GetInstallablePackageAsync(_package, CancellationToken.None);
				if (p.IsValid)
				{
					if (!p.ExactVersion.IsEmpty && (!_upgradeVersion.HasValue || !_upgradeVersion.Value.Equals(p.ExactVersion)))
					{
						_upgradeVersion = p.ExactVersion;
						OnPropertyChanged("UpgradeVersion");
					}

					if (!String.IsNullOrEmpty(p.Description) && p.Description != _package.Description)
					{
						_package.Description = p.Description;
						OnPropertyChanged("Description");
					}
					else if (_package.Description == null)
					{
						_package.Description = string.Empty;
						OnPropertyChanged("Description");
					}
				}
			}
			catch (Exception ex) when (!ex.IsCriticalException())
			{
				Debug.Fail("Unhandled exception: " + ex.ToString());
				// Nowhere else to report the exception, so just swallow it to
				// avoid bringing down the whole process.
			}
		}

		public PackageSpec Package => _package;

		public string PackageSpec
		{
			get
			{
				if (_package.IsValid)
				{
					if (_package.ExactVersion.IsEmpty)
					{
						return _package.Name;
					}
					return "{0}=={1}".FormatInvariant(_package.Name, _package.ExactVersion);
				}
				return Resources.PipPackageUnknownPackageSpec;
			}
		}

		public string Name => _package.Name;

		public PackageVersion Version => _package.ExactVersion;

		public string DisplayName => Version.IsEmpty ? Name : "{0} ({1})".FormatInvariant(Name, Version);

		public string Description
		{
			get
			{
				if (_package.Description == null)
				{
					TriggerUpdate();
					return Resources.LoadingDescription;
				}
				else if (string.IsNullOrEmpty(_package.Description))
				{
					return Resources.NoDescription;
				}
				return _package.Description;
			}
		}

		public PackageVersion UpgradeVersion
		{
			get
			{
				if (!_isInstalled)
				{
					// Package is not installed, so the latest version is always
					// shown.
					return PackageVersion.Empty;
				}

				if (!_upgradeVersion.HasValue)
				{
					// Kick off the Get, but return empty. We will raise an
					// event when the get completes.
					TriggerUpdate();
				}
				return _upgradeVersion.GetValueOrDefault();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
