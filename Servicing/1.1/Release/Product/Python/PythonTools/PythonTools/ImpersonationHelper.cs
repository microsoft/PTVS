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
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools {
    /// <summary>
    /// This class provides functionality to impersonate the User thread with
    /// credentials of one's choice
    /// </summary>
    internal class ImpersonationHelper : IDisposable {
        private IntPtr tokenHandle;
        private IntPtr dupeTokenHandle;
        private WindowsImpersonationContext impersonatedUser;
        private const int SecurityImpersonation = 2;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user">User to impersonate</param>
        public ImpersonationHelper(NetworkCredential user) {
            Impersonate(user);
        }

        /// <summary>
        /// Impersonates the user thread with the passed in credentials.
        /// If the thread is already being impersonated, then the current
        /// impersonation is undone first.
        /// Here we the allow the user to be null to be able to simplify the callers code
        /// </summary>
        /// <param name="user"></param>
        /// <returns>The full name of the user being impersonated</returns>
        public string Impersonate(NetworkCredential user) {
            if (user != null)
                return Impersonate(user.Domain, user.UserName, user.Password);
            return null;
        }

        /// <summary>
        /// Impersonates the user thread with the passed in credentials.
        /// If the thread is already being impersonated, then the current
        /// impersonation is undone first.
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns>The full name of the user being impersonated</returns>
        public string Impersonate(string domainName, string username, string password) {
            return Impersonate(domainName, username, password, false);
        }

        /// <summary>
        /// Impersonates the user thread with the passed in credentials.
        /// If the thread is already being impersonated, then the current
        /// impersonation is undone first.
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="forceImpersonation"></param>
        /// <returns>The full name of the user being impersonated</returns>
        public string Impersonate(string domainName, string username, string password, bool forceImpersonation) {
            if (forceImpersonation || !IsCurrentUser(domainName, username)) {
                UndoImpersonate();

                WindowsIdentity newId = GetWindowsIdentity(domainName, username, password);
                impersonatedUser = newId.Impersonate();
            }

            //System.Diagnostics.Debug.WriteLine("currentUser:" + GetCurrentUserName());
            return GetCurrentUserName();
        }

        /// <summary>
        /// Check if the current user is the same as the one in the parameters
        /// TODO: Update to support non-domain accounts
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        private bool IsCurrentUser(string domainName, string username) {
            Utilities.ArgumentNotNull("domainName", domainName);
            Utilities.ArgumentNotNull("username", username);

            string fullName = String.Format(@"{0}\{1}", domainName, username);
            return (fullName.Equals(GetCurrentUserName(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// If the user thread is currently being impersonated, it is reverted.
        /// </summary>
        /// <returns></returns>
        public string UndoImpersonate() {
            if (impersonatedUser != null) {
                impersonatedUser.Undo();
            }

            if (tokenHandle != IntPtr.Zero) {
                NativeMethods.CloseHandle(tokenHandle);
                tokenHandle = IntPtr.Zero;
            }
            if (dupeTokenHandle != IntPtr.Zero) {
                NativeMethods.CloseHandle(dupeTokenHandle);
                dupeTokenHandle = IntPtr.Zero;
            }

            return GetCurrentUserName();
        }

        /// <summary>
        /// Returns the full name of the user whose credentials are being used
        /// for the current user thread, as the Operating system sees it.
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentUserName() {
            return WindowsIdentity.GetCurrent().Name;
        }

        /// <summary>
        /// IDisposable interface
        /// </summary>
        public void Dispose() {
            UndoImpersonate();
        }

        #region Private
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private WindowsIdentity GetWindowsIdentity(string domainName, string username, string password) {
            tokenHandle = IntPtr.Zero;
            dupeTokenHandle = IntPtr.Zero;

            // Call LogonUser to obtain a handle to an access token.
            if (!NativeMethods.LogonUser(username, domainName, password,
                NativeMethods.LogonType.LOGON32_LOGON_INTERACTIVE,
                NativeMethods.LogonProvider.LOGON32_PROVIDER_DEFAULT,
                ref tokenHandle)) {

                string msg = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
                throw new Exception("Incorrect user name or password: " + msg);
            }
            
            if (!NativeMethods.DuplicateToken(tokenHandle, SecurityImpersonation, ref dupeTokenHandle)) {
                throw new Exception("DuplicateToken failed");
            }

            return new WindowsIdentity(dupeTokenHandle);
        }
        #endregion
    }
}
