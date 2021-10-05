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

namespace Microsoft.PythonTools.XamlDesignerSupport
{
	/// <summary>
	/// Provides access to the DesignerContext and WpfEventBindingProvider assuming that functionality
	/// is installed into VS.  If it's not installed then this becomes a nop and DesignerContextType
	/// returns null;
	/// </summary>
	[Export(typeof(IXamlDesignerSupport))]
	class XamlDesignerSupport : IXamlDesignerSupport
	{
		private readonly Lazy<Guid> _DesignerContextTypeGuid = new Lazy<Guid>(() =>
		{
			try
			{
				return typeof(DesignerContext).GUID;
			}
			catch
			{
				return Guid.Empty;
			}
		});

		public Guid DesignerContextTypeGuid => _DesignerContextTypeGuid.Value;

		public object CreateDesignerContext()
		{
			var context = new DesignerContext();
			//Set the RuntimeNameProvider so the XAML designer will call it when items are added to
			//a design surface. Since the provider does not depend on an item context, we provide it at 
			//the project level.
			// This is currently disabled because we don't successfully serialize to the remote domain
			// and the default name provider seems to work fine.  Likely installing our assembly into
			// the GAC or implementing an IsolationProvider would solve this.
			//context.RuntimeNameProvider = new PythonRuntimeNameProvider();
			return context;
		}

		public void InitializeEventBindingProvider(object designerContext, IXamlDesignerCallback callback)
		{
			Debug.Assert(designerContext is DesignerContext);
			((DesignerContext)designerContext).EventBindingProvider = new WpfEventBindingProvider(callback);
		}
	}
}
