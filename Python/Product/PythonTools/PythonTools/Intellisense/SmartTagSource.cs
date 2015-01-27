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
using System.Diagnostics;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
#if DEV14_OR_LATER
#pragma warning disable 0618
#endif

    // TODO: Switch from smart tags to Light Bulb: http://go.microsoft.com/fwlink/?LinkId=394601
    class SmartTagSource : ISmartTagSource {
        private readonly ITextBuffer _textBuffer;
        private readonly System.IServiceProvider _serviceProvider;
        
        public SmartTagSource(System.IServiceProvider serviceProvider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _serviceProvider = serviceProvider;
        }

        public void AugmentSmartTagSession(ISmartTagSession session, IList<SmartTagActionSet> smartTagActionSets) {
            AddImportTags(session, smartTagActionSets);
        }

        private void AddImportTags(ISmartTagSession session, IList<SmartTagActionSet> smartTagActionSets) {
            var textBuffer = _textBuffer;
            var span = session.CreateTrackingSpan(textBuffer);
            var imports = textBuffer.CurrentSnapshot.GetMissingImports(_serviceProvider, span);
            IOleComponentManager compMgr;
            SmartTagController controller;

            session.Properties.TryGetProperty<IOleComponentManager>(typeof(SmartTagController), out compMgr);
            session.Properties.TryGetProperty<SmartTagController>(typeof(SmartTagSource.AbortedAugmentInfo), out controller);

            if (imports != MissingImportAnalysis.Empty) {
                session.ApplicableToSpan = imports.ApplicableToSpan;

                // When doing idle processing we can keep getting kicked out and come back.  The whole process
                // of getting the import smart tags is done lazily through iterators.  If we keep trying again
                // and not getting enough idle time we'll never work away through the full list when it's large
                // (for example 'sys' in a large distro which is imported and exported everywhere).  So as long
                // as we're working on the same location (our SmartTagController tracks this) then we'll keep working
                // through the same enumerator so we make progress over time.   So we'll read the AbortedAugment
                // here and continue working, and if we run out of idletime we'll add or update the aborted augment.
                List<ImportSmartTagAction> actions;
                IEnumerator<ExportedMemberInfo> importsEnum;

                if (controller == null || controller._abortedAugment == null) {
                    actions = new List<ImportSmartTagAction>();
                    importsEnum = imports.AvailableImports.GetEnumerator();
                } else {
                    // continue processing of the old imports
                    importsEnum = controller._abortedAugment.Imports;
                    actions = controller._abortedAugment.Actions;
                }

                bool aborted = false;
                while (importsEnum.MoveNext()) {
                    var import = importsEnum.Current;

                    if (import.IsDefinedInModule) {
                        int lastDot;

                        if ((lastDot = import.Name.LastIndexOf('.')) == -1) {
                            // simple import
                            actions.Add(new ImportSmartTagAction(import.Name, _textBuffer, session.TextView, _serviceProvider));
                        } else {
                            // importing a package or member of a module
                            actions.Add(new ImportSmartTagAction(import.Name.Substring(0, lastDot), import.Name.Substring(lastDot + 1), _textBuffer, session.TextView, _serviceProvider));
                        }
                    }

                    if (compMgr != null && compMgr.FContinueIdle() == 0) {
                        // we've run out of time, save our progress...
                        if (controller != null) {
                            controller._sessionIsInvalid = true;
                            controller._abortedAugment = new AbortedAugmentInfo(importsEnum, actions);
                        }
                        aborted = true;
                        break;
                    }
                }

                if (!aborted && controller != null) {
                    controller._abortedAugment = null;
                }

                if (actions.Count > 0 && !aborted) {
                    actions.Sort(SmartTagComparer);
                    smartTagActionSets.Add(new SmartTagActionSet(new ReadOnlyCollection<ISmartTagAction>(actions.ToArray())));
                }
            }
        }

        private int SmartTagComparer(ImportSmartTagAction left, ImportSmartTagAction right) {
            if (left.FromName == null) {
                if (right.FromName != null) {
                    // left is import <fob>, order it first
                    return -1;
                }

                // two imports, order by name
                return String.Compare(left.Name, right.Name);
            } else if (right.FromName == null) {
                // left is from import, right is import, import comes first
                return 1;
            }

            // two from imports, order by from names, shorter names will come first
            return String.Compare(left.FromName, right.FromName);
        }

        public void Dispose() {
        }

        internal class AbortedAugmentInfo {
            public readonly IEnumerator<ExportedMemberInfo> Imports;
            public readonly List<ImportSmartTagAction> Actions;

            public AbortedAugmentInfo(IEnumerator<ExportedMemberInfo> importsEnum, List<ImportSmartTagAction> actions) {
                Imports = importsEnum;
                Actions  = actions;
            }
        }
    }
}
