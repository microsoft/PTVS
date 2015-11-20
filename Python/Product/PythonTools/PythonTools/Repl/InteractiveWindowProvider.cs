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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(InteractiveWindowProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class InteractiveWindowProvider {
        private readonly List<IVsInteractiveWindow> _windows = new List<IVsInteractiveWindow>();
        private readonly List<IVsInteractiveWindow> _mruWindows = new List<IVsInteractiveWindow>();
        private readonly Dictionary<string, int> _temporaryWindows = new Dictionary<string, int>();

        private readonly IServiceProvider _serviceProvider;
        private readonly IInteractiveEvaluatorProvider[] _evaluators;
        private readonly IVsInteractiveWindowFactory _windowFactory;
        private readonly IContentType _pythonContentType;

        [ImportingConstructor]
        public InteractiveWindowProvider(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            [Import] IVsInteractiveWindowFactory factory,
            [ImportMany] IInteractiveEvaluatorProvider[] evaluators,
            [Import] IContentTypeRegistryService contentTypeService
        ) {
            _serviceProvider = serviceProvider;
            _evaluators = evaluators;
            _windowFactory = factory;
            _pythonContentType = contentTypeService.GetContentType(PythonCoreConstants.ContentType);
        }

        public IEnumerable<IVsInteractiveWindow> AllOpenWindows {
            get {
                return _windows.Where(w => w != null).ToArray();
            }
        }

        public IVsInteractiveWindow OpenOrCreate(string replId) {
            IVsInteractiveWindow wnd;
            int curId = 0;
            while (curId < _windows.Count) {
                wnd = _windows[curId];
                var eval = wnd?.InteractiveWindow?.Evaluator as SelectableReplEvaluator;
                if (eval?.CurrentEvaluator == replId) {
                    wnd.Show(true);
                    return wnd;
                }
                ++curId;
            }

            wnd = Create(replId);
            wnd.Show(true);
            return wnd;
        }

        public IVsInteractiveWindow Create(string replId) {
            int curId = 0;
            while (curId < _windows.Count && _windows[curId] != null) {
                ++curId;
            }

            var window = CreateInteractiveWindowInternal(
                new SelectableReplEvaluator(_evaluators, replId),
                _pythonContentType,
                true,
                curId + 1,
                SR.GetString(SR.ReplCaptionNoEvaluator),
                typeof(Navigation.PythonLanguageInfo).GUID,
                "PythonInteractive"
            );

            if (curId >= _windows.Count) {
                _windows.Add(window);
            } else {
                _windows[curId] = window;
            }

            window.InteractiveWindow.TextView.Closed += (s, e) => {
                Debug.Assert(ReferenceEquals(_windows[curId], window));
                _windows[curId] = null;
            };

            return window;
        }

        public IVsInteractiveWindow OpenOrCreateTemporary(string replId, string title) {
            bool dummy;
            return OpenOrCreateTemporary(replId, title, out dummy);
        }

        public IVsInteractiveWindow OpenOrCreateTemporary(string replId, string title, out bool wasCreated) {
            IVsInteractiveWindow wnd;
            int curId;
            if (_temporaryWindows.TryGetValue(replId, out curId)) {
                wnd = curId >= 0 && curId < _windows.Count ? _windows[curId] : null;
                wnd.Show(true);
                wasCreated = false;
                return wnd;
            }

            wnd = CreateTemporary(replId, title);
            wnd.Show(true);
            wasCreated = true;
            return wnd;
        }

        public IVsInteractiveWindow CreateTemporary(string replId, string title) {
            int curId = 0;
            while (curId < _windows.Count && _windows[curId] != null) {
                ++curId;
            }

            var window = CreateInteractiveWindowInternal(
                _evaluators.Select(p => p.GetEvaluator(replId)).FirstOrDefault(e => e != null),
                _pythonContentType,
                true,
                curId + 1,
                title,
                typeof(Navigation.PythonLanguageInfo).GUID,
                replId
            );

            if (curId >= _windows.Count) {
                _windows.Add(window);
            } else {
                _windows[curId] = window;
            }
            _temporaryWindows[replId] = curId;

            window.InteractiveWindow.TextView.Closed += (s, e) => {
                Debug.Assert(ReferenceEquals(_windows[curId], window));
                _windows[curId] = null;
                _temporaryWindows.Remove(replId);
            };

            return window;
        }

        //private const string ActiveReplsKey = "ActiveRepls";
        //private const string ContentTypeKey = "ContentType";
        //private const string RolesKey = "Roles";
        //private const string TitleKey = "Title";
        //private const string ReplIdKey = "ReplId";
        //private const string LanguageServiceGuidKey = "LanguageServiceGuid";

        //private static RegistryKey GetRegistryRoot() {
        //    return VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, writable: true).CreateSubKey(ActiveReplsKey);
        //}

        //private void SaveInteractiveInfo(int id, IInteractiveEvaluator evaluator, IContentType contentType, string[] roles, string title, Guid languageServiceGuid, string replId) {
        //    using (var root = GetRegistryRoot()) {
        //        if (root != null) {
        //            using (var replInfo = root.CreateSubKey(id.ToString())) {
        //                replInfo.SetValue(ContentTypeKey, contentType.TypeName);
        //                replInfo.SetValue(TitleKey, title);
        //                replInfo.SetValue(ReplIdKey, replId.ToString());
        //                replInfo.SetValue(LanguageServiceGuidKey, languageServiceGuid.ToString());
        //            }
        //        }
        //    }
        //}

        //internal bool CreateFromRegistry(IComponentModel model, int id) {
        //    string contentTypeName, title, replId, languageServiceId;

        //    using (var root = GetRegistryRoot()) {
        //        if (root == null) {
        //            return false;
        //        }

        //        using (var replInfo = root.OpenSubKey(id.ToString())) {
        //            if (replInfo == null) {
        //                return false;
        //            }

        //            contentTypeName = replInfo.GetValue(ContentTypeKey) as string;
        //            if (contentTypeName == null) {
        //                return false;
        //            }

        //            title = replInfo.GetValue(TitleKey) as string;
        //            if (title == null) {
        //                return false;
        //            }

        //            replId = replInfo.GetValue(ReplIdKey) as string;
        //            if (replId == null) {
        //                return false;
        //            }

        //            languageServiceId = replInfo.GetValue(LanguageServiceGuidKey) as string;
        //            if (languageServiceId == null) {
        //                return false;
        //            }
        //        }
        //    }

        //    Guid languageServiceGuid;
        //    if (!Guid.TryParse(languageServiceId, out languageServiceGuid)) {
        //        return false;
        //    }

        //    var contentTypes = model.GetService<IContentTypeRegistryService>();
        //    var contentType = contentTypes.GetContentType(contentTypeName);
        //    if (contentType == null) {
        //        return false;
        //    }

        //    string[] roles;
        //    var evaluator = GetInteractiveEvaluator(model, replId, out roles);
        //    if (evaluator == null) {
        //        return false;
        //    }

        //    CreateInteractiveWindow(evaluator, contentType, roles, id, title, languageServiceGuid, replId);
        //    return true;
        //}

        private IVsInteractiveWindow CreateInteractiveWindowInternal(
            IInteractiveEvaluator evaluator,
            IContentType contentType,
            bool alwaysCreate,
            int id,
            string title,
            Guid languageServiceGuid,
            string replId
        ) {
            var service = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));
            var model = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));

            //SaveInteractiveInfo(id, evaluator, contentType, roles, title, languageServiceGuid, replId);

            var creationFlags =
                __VSCREATETOOLWIN.CTW_fMultiInstance |
                __VSCREATETOOLWIN.CTW_fActivateWithProject;

            if (alwaysCreate) {
                creationFlags |= __VSCREATETOOLWIN.CTW_fForceCreate;
            }

            var replWindow = _windowFactory.Create(GuidList.guidPythonInteractiveWindowGuid, id, title, evaluator, creationFlags);
            ((ToolWindowPane)replWindow).BitmapImageMoniker = KnownMonikers.PYInteractiveWindow;
            replWindow.InteractiveWindow.Properties[typeof(IVsInteractiveWindow)] = replWindow;
            replWindow.SetLanguage(GuidList.guidPythonLanguageServiceGuid, contentType);
            replWindow.InteractiveWindow.InitializeAsync();

            return replWindow;
        }

    }
}
