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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonToolz {

    internal sealed class VsCredentials {
        // Integrated windows authentication methods
        private static readonly string[] windowsIntegratedAuthenticationMethods = new string[] {
                "NTLM",
                "Negotiate",
                "Kerberos",
            };

        /// <summary>
        /// Prompt the user for credentials.
        /// </summary>
        /// <param name="serviceProvider">
        /// <param name="targetUri">
        /// The credential target. It is displayed in the prompt dialog and is
        /// used for credential storage.
        /// </param>
        /// <param name="authenticationTypes"></param>
        /// <param name="realm"></param>
        /// <param name="credential">The user supplied credentials.</param>
        /// <returns>
        /// DialogResult.OK = if Successfully prompted user for credentials.
        /// DialogResult.Cancel = if user cancelled the prompt dialog.
        /// </returns>
        internal static DialogResult PromptForCredentials(IServiceProvider serviceProvider, Uri targetUri, string[] authenticationTypes, string realm, out NetworkCredential credential) {
            Utilities.ArgumentNotNull("targetUri", targetUri);
            Utilities.ArgumentNotNull("authenticationTypes", authenticationTypes);
            Utilities.ArgumentNotNull("realm", realm);

            VisualStudio.Shell.Interop.IVsUIShell uiShell = null;
            DialogResult dr = DialogResult.Cancel;
            credential = null;
            string username;
            string password;
            bool isWindowsAuthentication = IsWindowAuthentication(authenticationTypes);

            try {
                IntPtr hwndOwner = IntPtr.Zero;

                // Put the shell into a modal state
                uiShell = serviceProvider.GetService(typeof(VisualStudio.Shell.Interop.SVsUIShell)) as VisualStudio.Shell.Interop.IVsUIShell;
                if (uiShell != null) {
                    int hr = uiShell.EnableModeless(0 /*false*/);
                    Debug.Assert(VisualStudio.ErrorHandler.Succeeded(hr), "Error calling IVsUIShell.EnableModeless");

                    hr = uiShell.GetDialogOwnerHwnd(out hwndOwner);
                    Debug.Assert(VisualStudio.ErrorHandler.Succeeded(hr), "Error calling IVsUIShell.GetDialogOwnerHwnd");
                }

                // Show the OS credential dialog.
                dr = ShowOSCredentialDialog(hwndOwner, targetUri, isWindowsAuthentication, realm, out username, out password);
            } finally {
                if (uiShell != null) {
                    //If we were able to put the shell into a modal state earlier, now make it modeless
                    int hr = uiShell.EnableModeless(1 /*true*/);
                    Debug.Assert(VisualStudio.ErrorHandler.Succeeded(hr), "Error calling IVsUIShell.EnableModeless");
                }
            }

            // Create the NetworkCredential object.
            if (dr == DialogResult.OK && !String.IsNullOrEmpty(username) && password != null) {
                string domain;
                string user;
                if (!isWindowsAuthentication || !ParseUsername(username, out user, out domain)) {
                    user = username;
                    domain = String.Empty;
                }

                credential = CreateCredentials(user, password, domain);
            }

            return dr;
        }

        //---------------------------------------------------------------------
        // protected methods
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // private methods
        //---------------------------------------------------------------------

        /// <summary>
        /// check whether it is windows integrated authentication
        /// </summary>
        /// <param name="authenticationTypes"></param>
        /// <return></return>
        /// <remarks></remarks>
        private static bool IsWindowAuthentication(string[] authenticationTypes) {

            foreach (string authenticationType in authenticationTypes) {
                if (authenticationType != null) {
                    foreach (string windowsAuthenticationMethod in windowsIntegratedAuthenticationMethods) {
                        if (String.Equals(authenticationType, windowsAuthenticationMethod, StringComparison.OrdinalIgnoreCase)) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// This function calls the OS dialog to prompt user for credential.
        /// </summary>
        /// <param name="hwdOwner">The parent for the dialog.</param>
        /// <param name="targetUri">
        /// The credential target. It is displayed in the prompt dialog and is
        /// used for credential storage.
        /// </param>
        /// <param name="isWindowsAuthentication"></param>
        /// <param name="realm"></param>
        /// <param name="userName">The username supplied by the user.</param>
        /// <param name="password">The password supplied by the user.</param>
        /// <returns>
        /// DialogResult.OK = if Successfully prompted user for credentials.
        /// DialogResult.Cancel = if user cancelled the prompt dialog.
        /// </returns>
        private static DialogResult ShowOSCredentialDialog(IntPtr hwdOwner, Uri targetUri, bool isWindowsAuthentication, string realm, out string userName, out string password) {
            DialogResult retValue = DialogResult.Cancel;
            userName = string.Empty;
            password = string.Empty;

            string titleFormat = "Enter credentials...";
            string description = String.Format("Enter a user name and password with access to {0}", targetUri.GetLeftPart(UriPartial.Path));

            string target = targetUri.Host;

            // Create the CREDUI_INFO structure. 
            CredUI.CREDUI_INFO info = new CredUI.CREDUI_INFO();
            info.pszCaptionText = titleFormat;
            info.pszMessageText = description;
            info.hwndParentCERParent = hwdOwner;
            info.hbmBannerCERHandle = IntPtr.Zero;
            info.cbSize = Marshal.SizeOf(info);

            // We specify CRED_TYPE_SERVER_CREDENTIAL flag as the stored credentials appear in the 
            // "Control Panel->Stored Usernames and Password". It is how IE stores and retrieve
            // credentials. By using the CRED_TYPE_SERVER_CREDENTIAL flag allows IE and VS to
            // share credentials.
            // We dont specify the CREDUI_FLAGS_EXPECT_CONFIRMATION as the VS proxy service consumers
            // dont call back into the service to confirm that the call succeeded.
            CredUI.CREDUI_FLAGS flags = CredUI.CREDUI_FLAGS.SERVER_CREDENTIAL |
                                                CredUI.CREDUI_FLAGS.GENERIC_CREDENTIALS |
                                                CredUI.CREDUI_FLAGS.ALWAYS_SHOW_UI |
                                                CredUI.CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX |
                                                CredUI.CREDUI_FLAGS.EXCLUDE_CERTIFICATES;

            if (isWindowsAuthentication) {
                flags |= CredUI.CREDUI_FLAGS.COMPLETE_USERNAME;
            }

            StringBuilder user = new StringBuilder(Convert.ToInt32(CredUI.CREDUI_MAX_USERNAME_LENGTH));
            StringBuilder pwd = new StringBuilder(Convert.ToInt32(CredUI.CREDUI_MAX_PASSWORD_LENGTH));
            int saveCredentials = 0;
            // Ensures that CredUPPromptForCredentials results in a prompt.
            int netError = CredUI.ERROR_LOGON_FAILURE;

            // Call the OS API to prompt for credentials.
            CredUI.CredUIReturnCodes result = CredUI.CredUIPromptForCredentials(
                info,
                target,
                IntPtr.Zero,
                netError,
                user,
                CredUI.CREDUI_MAX_USERNAME_LENGTH,
                pwd,
                CredUI.CREDUI_MAX_PASSWORD_LENGTH,
                ref saveCredentials,
                flags);


            if (result == CredUI.CredUIReturnCodes.NO_ERROR) {
                userName = user.ToString();
                password = pwd.ToString();

                retValue = DialogResult.OK;
            } else {
                Debug.Assert(result == CredUI.CredUIReturnCodes.ERROR_CANCELLED);
                retValue = DialogResult.Cancel;
            }

            return retValue;
        }

        /// <summary>
        /// Generates a NetworkCredential object from username and password. The function will
        /// parse username part and invoke the correct NetworkCredential construction.
        /// </summary>
        /// <param name="username">username retrieved from user/registry.</param>
        /// <param name="password">password retrieved from user/registry.</param>
        /// <param name="domain"></param>
        /// <returns></returns>
        private static NetworkCredential CreateCredentials(string username, string password, string domain) {
            NetworkCredential cred = null;

            if (!string.IsNullOrEmpty(username) && password != null) {
                if (string.IsNullOrEmpty(domain)) {
                    cred = new NetworkCredential(username, password);
                } else {
                    cred = new NetworkCredential(username, password, domain);
                }
            }

            return cred;
        }

        /// <summary>
        /// This fuction calls CredUIParseUserName() to parse the user name.
        /// </summary>
        /// <param name="username">The username name to pass.</param>
        /// <param name="user">The user part of the username.</param>
        /// <param name="domain">The domain part of the username.</param>
        /// <returns>Returns true if it successfully parsed the username.</returns>
        private static bool ParseUsername(string username, out string user, out string domain) {
            user = string.Empty;
            domain = string.Empty;

            if (string.IsNullOrEmpty(username)) {
                return false;
            }

            bool successfullyParsed = true;

            StringBuilder strUser = new StringBuilder(Convert.ToInt32(CredUI.CREDUI_MAX_USERNAME_LENGTH));
            StringBuilder strDomain = new StringBuilder(Convert.ToInt32(CredUI.CREDUI_MAX_DOMAIN_TARGET_LENGTH));
            // Call the OS API to do the parsing.
            CredUI.CredUIReturnCodes result = CredUI.CredUIParseUserName(username,
                                                    strUser,
                                                    CredUI.CREDUI_MAX_USERNAME_LENGTH,
                                                    strDomain,
                                                    CredUI.CREDUI_MAX_DOMAIN_TARGET_LENGTH);

            successfullyParsed = result == CredUI.CredUIReturnCodes.NO_ERROR;

            if (successfullyParsed) {
                user = strUser.ToString();
                domain = strDomain.ToString();
            }

            return successfullyParsed;
        }
    }
}


