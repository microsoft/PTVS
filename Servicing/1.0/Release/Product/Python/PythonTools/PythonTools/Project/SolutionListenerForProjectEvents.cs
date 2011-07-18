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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Project
{
	/// <summary>
	/// This class triggers the project events for "our" hierrachies.
	/// </summary>
	internal class SolutionListenerForProjectEvents : SolutionListener, IProjectEvents
	{
		#region events
		/// Event raised just after the project file opened.
		/// </summary>
		public event EventHandler<AfterProjectFileOpenedEventArgs> AfterProjectFileOpened;

		/// <summary>
		/// Event raised before the project file closed.
		/// </summary>
		public event EventHandler<BeforeProjectFileClosedEventArgs> BeforeProjectFileClosed;
		#endregion

		#region ctor
		internal SolutionListenerForProjectEvents(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
		}
		#endregion

		#region overridden methods
		public override int OnAfterOpenProject(IVsHierarchy hierarchy, int added)
		{
			IProjectEventsListener projectEventListener = hierarchy as IProjectEventsListener;
			if(projectEventListener != null && projectEventListener.IsProjectEventsListener)
			{
				this.RaiseAfterProjectFileOpened((added != 0) ? true : false);
			}

			return VSConstants.S_OK;
		}

		public override int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
		{
			IProjectEventsListener projectEvents = hierarchy as IProjectEventsListener;
			if(projectEvents != null && projectEvents.IsProjectEventsListener)
			{
				this.RaiseBeforeProjectFileClosed(hierarchy, (removed != 0) ? true : false);
			}

			return VSConstants.S_OK;
		}
		#endregion

		#region helpers
		/// <summary>
		/// Raises after project file opened event.
		/// </summary>
		/// <param name="added">True if the project is added to the solution after the solution is opened. false if the project is added to the solution while the solution is being opened.</param>
		private void RaiseAfterProjectFileOpened(bool added)
		{
			// Save event in temporary variable to avoid race condition.
			EventHandler<AfterProjectFileOpenedEventArgs> tempEvent = this.AfterProjectFileOpened;
			if(tempEvent != null)
			{
				tempEvent(this, new AfterProjectFileOpenedEventArgs());
			}
		}




		/// <summary>
		/// Raises the before  project file closed event.
		/// </summary>
		/// <param name="added">true if the project was removed from the solution before the solution was closed. false if the project was removed from the solution while the solution was being closed.</param>
		private void RaiseBeforeProjectFileClosed(IVsHierarchy hierarchy, bool removed)
		{
			// Save event in temporary variable to avoid race condition.
			EventHandler<BeforeProjectFileClosedEventArgs> tempEvent = this.BeforeProjectFileClosed;
			if(tempEvent != null)
			{
				tempEvent(this, new BeforeProjectFileClosedEventArgs(hierarchy, removed));
			}
		}
	}
		#endregion
}
