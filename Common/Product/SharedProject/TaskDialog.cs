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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.VisualStudioTools {
    sealed class TaskDialog {
        private readonly IServiceProvider _provider;
        private readonly List<TaskDialogButton> _buttons;
        private readonly List<TaskDialogButton> _radioButtons;

        public TaskDialog(IServiceProvider provider) {
            _provider = provider;
            _buttons = new List<TaskDialogButton>();
            _radioButtons = new List<TaskDialogButton>();
            UseCommandLinks = true;
        }

        public TaskDialogButton ShowModal() {
            var config = new NativeMethods.TASKDIALOGCONFIG();
            config.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.TASKDIALOGCONFIG));
            config.pButtons = IntPtr.Zero;
            config.pRadioButtons = IntPtr.Zero;

            var uiShell = (IVsUIShell)_provider.GetService(typeof(SVsUIShell));
            uiShell.GetDialogOwnerHwnd(out config.hwndParent);
            uiShell.EnableModeless(0);

            var customButtons = new List<TaskDialogButton>();
            config.dwCommonButtons = 0;

            foreach (var button in Buttons) {
                var flag = GetButtonFlag(button);
                if (flag != 0) {
                    config.dwCommonButtons |= flag;
                } else {
                    customButtons.Add(button);
                }
            }

            try {
                if (customButtons.Any()) {
                    config.cButtons = (uint)customButtons.Count;
                    var ptr = config.pButtons = Marshal.AllocHGlobal(customButtons.Count * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)));
                    for (int i = 0; i < customButtons.Count; ++i) {
                        NativeMethods.TASKDIALOG_BUTTON data;
                        data.nButtonID = GetButtonId(null, null, i);
                        data.pszButtonText = customButtons[i].Text;
                        Marshal.StructureToPtr(data, ptr + i * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)), false);
                    }
                } else {
                    config.cButtons = 0;
                    config.pButtons = IntPtr.Zero;
                }

                if (_buttons.Any() && SelectedButton != null) {
                    config.nDefaultButton = GetButtonId(SelectedButton, customButtons);
                } else {
                    config.nDefaultButton = 0;
                }

                if (_radioButtons.Any()) {
                    config.cRadioButtons = (uint)_radioButtons.Count;
                    var ptr = config.pRadioButtons = Marshal.AllocHGlobal(_radioButtons.Count * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)));
                    for (int i = 0; i < _radioButtons.Count; ++i) {
                        NativeMethods.TASKDIALOG_BUTTON data;
                        data.nButtonID = GetRadioId(null, null, i);
                        data.pszButtonText = _radioButtons[i].Text;
                        Marshal.StructureToPtr(data, ptr + i * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)), false);
                    }

                    if (SelectedRadioButton != null) {
                        config.nDefaultRadioButton = GetRadioId(SelectedRadioButton, _radioButtons);
                    } else {
                        config.nDefaultRadioButton = 0;
                    }
                }

                config.pszWindowTitle = Title;
                config.pszMainInstruction = MainInstruction;
                config.pszContent = Content;
                config.pszExpandedInformation = ExpandedInformation;
                config.pszExpandedControlText = ExpandedControlText;
                config.pszCollapsedControlText = CollapsedControlText;
                config.pfCallback = Callback;

                if (Width.HasValue) {
                    config.cxWidth = (uint)Width.Value;
                } else {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT;
                }
                if (EnableHyperlinks) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS;
                }
                if (AllowCancellation) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                }
                if (UseCommandLinks) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_USE_COMMAND_LINKS;
                }
                if (!string.IsNullOrEmpty(ExpandedInformation)) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_EXPAND_FOOTER_AREA;
                }
                if (ExpandedByDefault) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_EXPANDED_BY_DEFAULT;
                }
                if (SelectedVerified) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_VERIFICATION_FLAG_CHECKED;
                }
                if (CanMinimize) {
                    config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_CAN_BE_MINIMIZED;
                }

                config.dwFlags |= NativeMethods.TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW;

                int selectedButton, selectedRadioButton;
                bool verified;
                ErrorHandler.ThrowOnFailure(NativeMethods.TaskDialogIndirect(
                    ref config,
                    out selectedButton,
                    out selectedRadioButton,
                    out verified
                ));

                SelectedButton = GetButton(selectedButton, customButtons);
                SelectedRadioButton = GetRadio(selectedRadioButton, _radioButtons);
                SelectedVerified = verified;
            } finally {
                uiShell.EnableModeless(1);

                if (config.pButtons != IntPtr.Zero) {
                    for (int i = 0; i < customButtons.Count; ++i) {
                        Marshal.DestroyStructure(config.pButtons + i * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)), typeof(NativeMethods.TASKDIALOG_BUTTON));
                    }
                    Marshal.FreeHGlobal(config.pButtons);
                }
                if (config.pRadioButtons != IntPtr.Zero) {
                    for (int i = 0; i < _radioButtons.Count; ++i) {
                        Marshal.DestroyStructure(config.pRadioButtons + i * Marshal.SizeOf(typeof(NativeMethods.TASKDIALOG_BUTTON)), typeof(NativeMethods.TASKDIALOG_BUTTON));
                    }
                    Marshal.FreeHGlobal(config.pRadioButtons);
                }
            }

            return SelectedButton;
        }

        private int Callback(IntPtr hwnd, uint uNotification, UIntPtr wParam, IntPtr lParam, IntPtr lpRefData) {
            return VSConstants.S_OK;
        }

        public string Title { get; set; }
        public string MainInstruction { get; set; }
        public string Content { get; set; }
        public string VerificationText { get; set; }
        public string ExpandedInformation { get; set; }

        public bool ExpandedByDefault { get; set; }
        public string ExpandedControlText { get; set; }
        public string CollapsedControlText { get; set; }

        public int? Width { get; set; }
        public bool EnableHyperlinks { get; set; }
        public bool AllowCancellation { get; set; }
        public bool UseCommandLinks { get; set; }
        public bool CanMinimize { get; set; }

        public List<TaskDialogButton> Buttons {
            get {
                return _buttons;
            }
        }

        public List<TaskDialogButton> RadioButtons {
            get {
                return _radioButtons;
            }
        }

        public TaskDialogButton SelectedButton { get; set; }
        public TaskDialogButton SelectedRadioButton { get; set; }
        public bool SelectedVerified { get; set; }


        private static NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS GetButtonFlag(TaskDialogButton button) {
            if (button == TaskDialogButton.OK) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;
            } else if (button == TaskDialogButton.Cancel) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON;
            } else if (button == TaskDialogButton.Yes) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON;
            } else if (button == TaskDialogButton.No) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON;
            } else if (button == TaskDialogButton.Retry) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_RETRY_BUTTON;
            } else if (button == TaskDialogButton.Close) {
                return NativeMethods.TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
            } else {
                return 0;
            }
        }

        private static int GetButtonId(
            TaskDialogButton button,
            IList<TaskDialogButton> customButtons = null,
            int indexHint = -1
        ) {
            if (indexHint >= 0) {
                return indexHint + 1000;
            }

            if (button == TaskDialogButton.OK) {
                return NativeMethods.IDOK;
            } else if (button == TaskDialogButton.Cancel) {
                return NativeMethods.IDCANCEL;
            } else if (button == TaskDialogButton.Yes) {
                return NativeMethods.IDYES;
            } else if (button == TaskDialogButton.No) {
                return NativeMethods.IDNO;
            } else if (button == TaskDialogButton.Retry) {
                return NativeMethods.IDRETRY;
            } else if (button == TaskDialogButton.Close) {
                return NativeMethods.IDCLOSE;
            } else if (customButtons != null) {
                int i = customButtons.IndexOf(button);
                if (i >= 0) {
                    return i + 1000;
                }
            }

            return -1;
        }

        private static TaskDialogButton GetButton(int id, IList<TaskDialogButton> customButtons = null) {
            switch (id) {
                case NativeMethods.IDOK:
                    return TaskDialogButton.OK;
                case NativeMethods.IDCANCEL:
                    return TaskDialogButton.Cancel;
                case NativeMethods.IDYES:
                    return TaskDialogButton.Yes;
                case NativeMethods.IDNO:
                    return TaskDialogButton.No;
                case NativeMethods.IDRETRY:
                    return TaskDialogButton.Retry;
                case NativeMethods.IDCLOSE:
                    return TaskDialogButton.Close;
            }

            if (customButtons != null && id >= 1000 && id - 1000 < customButtons.Count) {
                return customButtons[id - 1000];
            }

            return null;
        }

        private static int GetRadioId(
            TaskDialogButton button,
            IList<TaskDialogButton> buttons,
            int indexHint = -1
        ) {
            if (indexHint >= 0) {
                return indexHint + 2000;
            }

            return buttons.IndexOf(button) + 2000;
        }

        private static TaskDialogButton GetRadio(int id, IList<TaskDialogButton> buttons) {
            if (id >= 2000 && id - 2000 < buttons.Count) {
                return buttons[id - 2000];
            }

            return null;
        }

        private static class NativeMethods {
            internal const int IDOK = 1;
            internal const int IDCANCEL = 2;
            internal const int IDABORT = 3;
            internal const int IDRETRY = 4;
            internal const int IDIGNORE = 5;
            internal const int IDYES = 6;
            internal const int IDNO = 7;
            internal const int IDCLOSE = 8;

            internal enum TASKDIALOG_FLAGS {
                TDF_ENABLE_HYPERLINKS = 0x0001,
                TDF_USE_HICON_MAIN = 0x0002,
                TDF_USE_HICON_FOOTER = 0x0004,
                TDF_ALLOW_DIALOG_CANCELLATION = 0x0008,
                TDF_USE_COMMAND_LINKS = 0x0010,
                TDF_USE_COMMAND_LINKS_NO_ICON = 0x0020,
                TDF_EXPAND_FOOTER_AREA = 0x0040,
                TDF_EXPANDED_BY_DEFAULT = 0x0080,
                TDF_VERIFICATION_FLAG_CHECKED = 0x0100,
                TDF_SHOW_PROGRESS_BAR = 0x0200,
                TDF_SHOW_MARQUEE_PROGRESS_BAR = 0x0400,
                TDF_CALLBACK_TIMER = 0x0800,
                TDF_POSITION_RELATIVE_TO_WINDOW = 0x1000,
                TDF_RTL_LAYOUT = 0x2000,
                TDF_NO_DEFAULT_RADIO_BUTTON = 0x4000,
                TDF_CAN_BE_MINIMIZED = 0x8000,
                TDF_SIZE_TO_CONTENT = 0x01000000
            }

            internal enum TASKDIALOG_COMMON_BUTTON_FLAGS {
                TDCBF_OK_BUTTON = 0x0001,
                TDCBF_YES_BUTTON = 0x0002,
                TDCBF_NO_BUTTON = 0x0004,
                TDCBF_CANCEL_BUTTON = 0x0008,
                TDCBF_RETRY_BUTTON = 0x0010,
                TDCBF_CLOSE_BUTTON = 0x0020
            }

            [DllImport("comctl32.dll", SetLastError = true)]
            internal static extern int TaskDialogIndirect(
                ref TASKDIALOGCONFIG pTaskConfig,
                out int pnButton,
                out int pnRadioButton,
                [MarshalAs(UnmanagedType.Bool)] out bool pfverificationFlagChecked);

            internal delegate int PFTASKDIALOGCALLBACK(IntPtr hwnd, uint uNotification, UIntPtr wParam, IntPtr lParam, IntPtr lpRefData);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct TASKDIALOG_BUTTON {
                public int nButtonID;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszButtonText;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct TASKDIALOGCONFIG {
                public uint cbSize;
                public IntPtr hwndParent;
                public IntPtr hInstance;
                public TASKDIALOG_FLAGS dwFlags;
                public TASKDIALOG_COMMON_BUTTON_FLAGS dwCommonButtons;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszWindowTitle;
                public IntPtr hMainIcon;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszMainInstruction;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszContent;
                public uint cButtons;
                public IntPtr pButtons;
                public int nDefaultButton;
                public uint cRadioButtons;
                public IntPtr pRadioButtons;
                public int nDefaultRadioButton;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszVerificationText;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszExpandedInformation;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszExpandedControlText;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszCollapsedControlText;
                public IntPtr hFooterIcon;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pszFooter;
                public PFTASKDIALOGCALLBACK pfCallback;
                public IntPtr lpCallbackData;
                public uint cxWidth;
            }
        }
    }

    class TaskDialogButton {
        public TaskDialogButton(string text) {
            Text = text;
        }

        public string Text { get; set; }

        private TaskDialogButton() { }
        public static readonly TaskDialogButton OK = new TaskDialogButton();
        public static readonly TaskDialogButton Cancel = new TaskDialogButton();
        public static readonly TaskDialogButton Yes = new TaskDialogButton();
        public static readonly TaskDialogButton No = new TaskDialogButton();
        public static readonly TaskDialogButton Retry = new TaskDialogButton();
        public static readonly TaskDialogButton Close = new TaskDialogButton();
    }
}