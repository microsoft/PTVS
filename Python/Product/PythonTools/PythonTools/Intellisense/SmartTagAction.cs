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
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Intellisense {
#if !DEV14_OR_LATER
    abstract class SmartTagAction : ISmartTagAction {
        private readonly RefactoringIconKind _iconKind;
        protected readonly IServiceProvider _serviceProvider;

        private static IVsResourceManager _resourceManager;
        private static Guid CmdDefUiPackageGuid = new Guid("{44E07B02-29A5-11D3-B882-00C04F79F802}");   // http://msdn.microsoft.com/en-us/library/dd891106.aspx
        private static Dictionary<RefactoringIconKind, ImageSource> _icons = new Dictionary<RefactoringIconKind, ImageSource>();
        private const string IDBMP_REFACTOR_IMAGES = "#2029";   // SharedCmdDef.vsct

        /// <summary>
        /// Creates a new smart tag with the specified icon.
        /// </summary>
        protected SmartTagAction(IServiceProvider serviceProvider, RefactoringIconKind iconKind) {
            _serviceProvider = serviceProvider;
            _iconKind = iconKind;
        }

        public ReadOnlyCollection<SmartTagActionSet> ActionSets {
            get { return null; }
        }

        public ImageSource Icon {
            get {
                ImageSource res;
                if (!_icons.TryGetValue(_iconKind, out res)) {
                    IVsResourceManager manager;
                    if (TryGetResourceManager(out manager)) {
                        IntPtr hbmpValue;
                        if (ErrorHandler.Succeeded(manager.LoadResourceBitmap(ref CmdDefUiPackageGuid, 0, IDBMP_REFACTOR_IMAGES, out hbmpValue))) {
                            const int iconSize = 16;

                            using (Bitmap bitmap = Bitmap.FromHbitmap(hbmpValue)) {
                                // Get rid of the backdrop behind the refactoring icons.
#if DEV11_OR_LATER
                                bitmap.MakeTransparent(System.Drawing.Color.Black);
#else
                                bitmap.MakeTransparent(System.Drawing.Color.White);
#endif

                                _icons[_iconKind] = res = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(),
                                    IntPtr.Zero,
                                    new Int32Rect((int)_iconKind * iconSize, 0, iconSize, iconSize),
                                    BitmapSizeOptions.FromEmptyOptions());
                            }
                        }
                    }
                }
                return res;
            }
        }

        private bool TryGetResourceManager(out IVsResourceManager resourceManager) {
            if (_resourceManager == null) {
                _resourceManager = _serviceProvider.GetService(typeof(SVsResourceManager)) as IVsResourceManager;
            }

            resourceManager = _resourceManager;
            return (resourceManager != null);
        }

        /// <summary>
        /// Gets the text which the user sees in the smart tag menu.
        /// </summary>
        public abstract string DisplayText {
            get;
        }

        /// <summary>
        /// Invokes the smart tag when the user clicks on it from the menu.
        /// </summary>
        public abstract void Invoke();

        public virtual bool IsEnabled {
            get { return true; }
        }
    }
#endif
}
