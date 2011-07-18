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
using System.Globalization;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Helper class to invoke a method on UI thread.
    /// </summary>
    public static class UIThreadInvoker
    {
        /// <summary>
        /// Used to invoke code on UI thread.
        /// </summary>
        private static Control s_uiThreadControl;

        /// <summary>
        /// Invokes specified method on main UI thread.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <returns>The value returned by invoked method.</returns>
        public static object Invoke(Delegate method)
        {
            Debug.Assert(s_uiThreadControl != null, "UIThreadInvoker.Invoke: the Control used to invoke code on UI thread has not been initialized!");

            if (method == null)
            {
                throw new ArgumentNullException("method");
            }
            return s_uiThreadControl.Invoke(method);
        }

        /// <summary>
        /// Invokes specified method on main UI thread.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="args">Arguments to the method to invoke.</param>
        /// <returns>The value returned by invoked method.</returns>
        public static object Invoke(Delegate method, params object[] args)
        {
            if (method == null)
            {
                throw new ArgumentNullException("method");
            }
            return s_uiThreadControl.Invoke(method, args);
        }

        /// <summary>
        /// Initializes the class.
        /// </summary>
        /// <remarks>
        /// This needs to be called on the UI thread.
        /// The control itself does not know about UI thread. How it works is:
        /// control.Invoke is called on the same thread where Control was initialized.
        /// 
        /// This method should be internal.
        /// The reason this is public is that assemblies in the sample are not signed.
        /// If you sign your Host Adapter assembly please make this "set" accessor internal 
        /// and add InternalsVisibleTo attribute to this assembly to enable Host Adapter assembly access internal methods of this assembly.
        /// </remarks>
        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId="handle")]
        public static void Initialize()
        {
            if (s_uiThreadControl == null)
            {
                s_uiThreadControl = new Control();
                // Force creating the control's handle needed by Control.Invoke.
                IntPtr handle = s_uiThreadControl.Handle;
            }
            else
            {
                Debug.Fail("UIThreadInvoker.Initialize: the Control used to invoke code on UI thread has already been initialized!");
            }
        }
    }
}
