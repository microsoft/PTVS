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
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project
{
	/// <summary>
	///This class provides some useful static shell based methods. 
	/// </summary>
	
	public static class UIHierarchyUtilities
	{
		/// <summary>
		/// Get reference to IVsUIHierarchyWindow interface from guid persistence slot.
		/// </summary>
		/// <param name="serviceProvider">The service provider.</param>
		/// <param name="persistenceSlot">Unique identifier for a tool window created using IVsUIShell::CreateToolWindow. 
		/// The caller of this method can use predefined identifiers that map to tool windows if those tool windows 
		/// are known to the caller. </param>
		/// <returns>A reference to an IVsUIHierarchyWindow interface.</returns>
		public static IVsUIHierarchyWindow GetUIHierarchyWindow(IServiceProvider serviceProvider, Guid persistenceSlot)
		{
            Utilities.ArgumentNotNull("serviceProvider", serviceProvider);

			IVsUIShell shell = serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;

			Debug.Assert(shell != null, "Could not get the ui shell from the project");
            Utilities.CheckNotNull(shell);			

			object pvar = null;
			IVsWindowFrame frame = null;
			IVsUIHierarchyWindow uiHierarchyWindow = null;

			try
			{
				ErrorHandler.ThrowOnFailure(shell.FindToolWindow(0, ref persistenceSlot, out frame));
				ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar));
			}
			finally
			{
				if(pvar != null)
				{
					uiHierarchyWindow = (IVsUIHierarchyWindow)pvar;
				}
			}

			return uiHierarchyWindow;
		}
	}
}
