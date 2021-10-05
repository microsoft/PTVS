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
	internal partial class ConfigurationExtension : UserControl
	{
		public static readonly ICommand Apply = new RoutedCommand();
		public static readonly ICommand Reset = new RoutedCommand();
		public static readonly ICommand AutoDetect = new RoutedCommand();
		public static readonly ICommand Remove = new RoutedCommand();

		private readonly ConfigurationExtensionProvider _provider;

		public ConfigurationExtension(ConfigurationExtensionProvider provider)
		{
			_provider = provider;
			DataContextChanged += ConfigurationExtension_DataContextChanged;
			InitializeComponent();
		}

		void ConfigurationExtension_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			var view = e.NewValue as EnvironmentView;
			if (view != null)
			{
				var current = Subcontext.DataContext as ConfigurationEnvironmentView;
				if (current == null || current.EnvironmentView != view)
				{
					var cev = new ConfigurationEnvironmentView(view);
					_provider.ResetConfiguration(cev);
					Subcontext.DataContext = cev;
				}
			}
		}

		private void Apply_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var view = e.Parameter as ConfigurationEnvironmentView;
			e.CanExecute = view != null && _provider.CanApply(view) && _provider.IsConfigurationChanged(view);
			e.Handled = true;
		}

		private void Apply_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var cev = (ConfigurationEnvironmentView)e.Parameter;
			var id = _provider.ApplyConfiguration(cev);
			if (_provider._alwaysCreateNew)
			{
				ConfigurationEnvironmentView.Added.Execute(id);
			}
			e.Handled = true;
		}

		private void Reset_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var view = e.Parameter as ConfigurationEnvironmentView;
			e.CanExecute = view != null && _provider.IsConfigurationChanged(view);
			e.Handled = true;
		}

		private void Reset_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			_provider.ResetConfiguration((ConfigurationEnvironmentView)e.Parameter);
			e.Handled = true;
		}

		private void Remove_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var view = e.Parameter as ConfigurationEnvironmentView;
			e.CanExecute = !_provider._alwaysCreateNew && view != null;
			e.Handled = true;
		}

		private void Remove_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			_provider.RemoveConfiguration((ConfigurationEnvironmentView)e.Parameter);
			e.Handled = true;
		}


		private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			Commands.CanExecute(null, sender, e);
		}

		private void Browse_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			Commands.Executed(null, sender, e);
		}

		private void SelectAllText(object sender, RoutedEventArgs e)
		{
			var tb = sender as TextBox;
			if (tb != null)
			{
				tb.SelectAll();
			}
		}

		private void AutoDetect_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var view = e.Parameter as ConfigurationEnvironmentView;
			e.CanExecute = view != null && !view.IsAutoDetectRunning && (
				Directory.Exists(view.PrefixPath) ||
				File.Exists(view.InterpreterPath) ||
				File.Exists(view.WindowsInterpreterPath)
			);
		}


		private async void AutoDetect_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var view = (ConfigurationEnvironmentView)e.Parameter;
			try
			{
				view.IsAutoDetectRunning = true;
				CommandManager.InvalidateRequerySuggested();

				var newView = await AutoDetectAsync(view.Values);

				view.Values = newView;
				CommandManager.InvalidateRequerySuggested();
			}
			finally
			{
				view.IsAutoDetectRunning = false;
			}
		}

		private async Task<ConfigurationValues> AutoDetectAsync(ConfigurationValues view)
		{
			if (!Directory.Exists(view.PrefixPath))
			{
				if (File.Exists(view.InterpreterPath))
				{
					view.PrefixPath = Path.GetDirectoryName(view.InterpreterPath);
				}
				else if (File.Exists(view.WindowsInterpreterPath))
				{
					view.PrefixPath = Path.GetDirectoryName(view.WindowsInterpreterPath);
				}
				else
				{
					// Don't have enough information, so abort without changing
					// any settings.
					return view;
				}
				while (Directory.Exists(view.PrefixPath) && !File.Exists(PathUtils.FindFile(view.PrefixPath, "site.py")))
				{
					view.PrefixPath = Path.GetDirectoryName(view.PrefixPath);
				}
			}

			if (!Directory.Exists(view.PrefixPath))
			{
				// If view.PrefixPath is not valid by this point, we can't find anything
				// else, so abort withou changing any settings.
				return view;
			}

			if (string.IsNullOrEmpty(view.Description))
			{
				view.Description = PathUtils.GetFileOrDirectoryName(view.PrefixPath);
			}

			if (!File.Exists(view.InterpreterPath))
			{
				view.InterpreterPath = PathUtils.FindFile(
					view.PrefixPath,
					CPythonInterpreterFactoryConstants.ConsoleExecutable,
					firstCheck: new[] { "scripts" }
				);
			}
			if (!File.Exists(view.WindowsInterpreterPath))
			{
				view.WindowsInterpreterPath = PathUtils.FindFile(
					view.PrefixPath,
					CPythonInterpreterFactoryConstants.WindowsExecutable,
					firstCheck: new[] { "scripts" }
				);
			}

			if (File.Exists(view.InterpreterPath))
			{
				using (var output = ProcessOutput.RunHiddenAndCapture(
					view.InterpreterPath, "-c", "import sys; print('%s.%s' % (sys.version_info[0], sys.version_info[1]))"
				))
				{
					var exitCode = await output;
					if (exitCode == 0)
					{
						view.VersionName = output.StandardOutputLines.FirstOrDefault() ?? view.VersionName;
					}
				}

				var arch = CPythonInterpreterFactoryProvider.ArchitectureFromExe(view.InterpreterPath);
				if (arch != InterpreterArchitecture.Unknown)
				{
					view.ArchitectureName = arch.ToString();
				}
			}

			return view;
		}
	}

	sealed class ConfigurationExtensionProvider : IEnvironmentViewExtension
	{
		private FrameworkElement _wpfObject;
		private readonly IInterpreterOptionsService _interpreterOptions;
		internal readonly bool _alwaysCreateNew;

		internal ConfigurationExtensionProvider(IInterpreterOptionsService interpreterOptions, bool alwaysCreateNew)
		{
			_interpreterOptions = interpreterOptions;
			_alwaysCreateNew = alwaysCreateNew;
		}

		public string ApplyConfiguration(ConfigurationEnvironmentView view)
		{
			if (!_alwaysCreateNew)
			{
				var factory = view.EnvironmentView.Factory;
				if (view.Description != factory.Configuration.Description)
				{
					// We're renaming the interpreter, remove the old one...
					_interpreterOptions.RemoveConfigurableInterpreter(factory.Configuration.Id);
				}
			}

			if (!Version.TryParse(view.VersionName, out var version))
			{
				version = null;
			}

			return _interpreterOptions.AddConfigurableInterpreter(
				view.Description,
				new VisualStudioInterpreterConfiguration(
					"",
					view.Description,
					view.PrefixPath,
					view.InterpreterPath,
					view.WindowsInterpreterPath,
					view.PathEnvironmentVariable,
					InterpreterArchitecture.TryParse(view.ArchitectureName ?? ""),
					version
				)
			);
		}

		public bool CanApply(ConfigurationEnvironmentView view)
		{
			if (string.IsNullOrEmpty(view.Description) || string.IsNullOrEmpty(view.InterpreterPath))
			{
				return false;
			}
			return true;
		}

		public bool IsConfigurationChanged(ConfigurationEnvironmentView view)
		{
			if (_alwaysCreateNew)
			{
				return true;
			}

			var factory = view.EnvironmentView.Factory;
			return view.Description != factory.Configuration.Description ||
				view.PrefixPath != factory.Configuration.GetPrefixPath() ||
				view.InterpreterPath != factory.Configuration.InterpreterPath ||
				view.WindowsInterpreterPath != factory.Configuration.GetWindowsInterpreterPath() ||
				view.PathEnvironmentVariable != factory.Configuration.PathEnvironmentVariable ||
				InterpreterArchitecture.TryParse(view.ArchitectureName) != factory.Configuration.Architecture ||
				view.VersionName != factory.Configuration.Version.ToString();
		}

		public void ResetConfiguration(ConfigurationEnvironmentView view)
		{
			var factory = view.EnvironmentView?.Factory;
			view.Description = factory?.Configuration.Description;
			view.PrefixPath = factory?.Configuration.GetPrefixPath();
			view.InterpreterPath = factory?.Configuration.InterpreterPath;
			view.WindowsInterpreterPath = factory?.Configuration.GetWindowsInterpreterPath();
			view.PathEnvironmentVariable = factory?.Configuration.PathEnvironmentVariable;
			view.ArchitectureName = factory?.Configuration.Architecture.ToString();
			view.VersionName = factory?.Configuration.Version.ToString();
		}

		public void RemoveConfiguration(ConfigurationEnvironmentView view)
		{
			if (_alwaysCreateNew)
			{
				return;
			}

			var factory = view.EnvironmentView.Factory;
			_interpreterOptions.RemoveConfigurableInterpreter(factory.Configuration.Id);
		}

		public int SortPriority
		{
			get { return -9; }
		}

		public string LocalizedDisplayName
		{
			get { return Resources.ConfigurationExtensionDisplayName; }
		}

		public FrameworkElement WpfObject
		{
			get
			{
				if (_wpfObject == null)
				{
					_wpfObject = new ConfigurationExtension(this);
				}
				return _wpfObject;
			}
		}

		public object HelpContent
		{
			get { return Resources.ConfigurationExtensionHelpContent; }
		}

		public string HelpText
		{
			get { return Resources.ConfigurationExtensionHelpContent; }
		}
	}

	struct ConfigurationValues
	{
		public string Description;
		public string PrefixPath;
		public string InterpreterPath;
		public string WindowsInterpreterPath;
		public string PathEnvironmentVariable;
		public string VersionName;
		public string ArchitectureName;
	}

	sealed class ConfigurationEnvironmentView : INotifyPropertyChanged
	{
		public static readonly ICommand Added = new RoutedCommand();

		private static readonly string[] _architectureNames = new[] {
			InterpreterArchitecture.x86.ToString(),
			InterpreterArchitecture.x64.ToString()
		};

		private static readonly string[] _versionNames = new[] {
			"2.5",
			"2.6",
			"2.7",
			"3.0",
			"3.1",
			"3.2",
			"3.3",
			"3.4",
			"3.5",
			"3.6",
			"3.7",
			"3.8",
		};

		private readonly EnvironmentView _view;
		private bool _isAutoDetectRunning;
		private ConfigurationValues _values;

		public ConfigurationEnvironmentView(EnvironmentView view)
		{
			_view = view;
		}

		public EnvironmentView EnvironmentView => _view;
		public static IList<string> ArchitectureNames => _architectureNames;

		public static IList<string> VersionNames => _versionNames;

		public bool IsAutoDetectRunning
		{
			get { return _isAutoDetectRunning; }
			set
			{
				if (_isAutoDetectRunning != value)
				{
					_isAutoDetectRunning = value;
					OnPropertyChanged();
				}
			}
		}

		public ConfigurationValues Values
		{
			get { return _values; }
			set
			{
				Description = value.Description;
				PrefixPath = value.PrefixPath;
				InterpreterPath = value.InterpreterPath;
				WindowsInterpreterPath = value.WindowsInterpreterPath;
				PathEnvironmentVariable = value.PathEnvironmentVariable;
				VersionName = value.VersionName;
				ArchitectureName = value.ArchitectureName;
			}
		}


		public string Description
		{
			get { return _values.Description; }
			set
			{
				if (_values.Description != value)
				{
					_values.Description = value;
					OnPropertyChanged();
				}
			}
		}

		public string PrefixPath
		{
			get { return _values.PrefixPath; }
			set
			{
				if (_values.PrefixPath != value)
				{
					_values.PrefixPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string InterpreterPath
		{
			get { return _values.InterpreterPath; }
			set
			{
				if (_values.InterpreterPath != value)
				{
					_values.InterpreterPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string WindowsInterpreterPath
		{
			get { return _values.WindowsInterpreterPath; }
			set
			{
				if (_values.WindowsInterpreterPath != value)
				{
					_values.WindowsInterpreterPath = value;
					OnPropertyChanged();
				}
			}
		}

		public string PathEnvironmentVariable
		{
			get { return _values.PathEnvironmentVariable; }
			set
			{
				if (_values.PathEnvironmentVariable != value)
				{
					_values.PathEnvironmentVariable = value;
					OnPropertyChanged();
				}
			}
		}

		public string ArchitectureName
		{
			get { return _values.ArchitectureName; }
			set
			{
				if (_values.ArchitectureName != value)
				{
					_values.ArchitectureName = value;
					OnPropertyChanged();
				}
			}
		}

		public string VersionName
		{
			get { return _values.VersionName; }
			set
			{
				if (_values.VersionName != value)
				{
					_values.VersionName = value;
					OnPropertyChanged();
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
