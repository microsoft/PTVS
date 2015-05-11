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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
#if !DEV14_OR_LATER
    sealed class SmartTagAugmentTask {
        private readonly Task<ReadOnlyCollection<ISmartTagAction>> _task;
        private readonly CancellationTokenSource _cancel;

        public readonly ITrackingSpan ApplicableToSpan;

        public SmartTagAugmentTask(
            IServiceProvider serviceProvider,
            ITextBuffer textBuffer,
            ITextView textView,
            MissingImportAnalysis imports
        ) {
            ApplicableToSpan = imports.ApplicableToSpan;
            _cancel = new CancellationTokenSource();
            _task = Task.Run(() => GetImportTags(
                serviceProvider,
                textBuffer,
                textView,
                imports,
                _cancel.Token
            ), _cancel.Token);
        }

        private static ReadOnlyCollection<ISmartTagAction> GetImportTags(
            IServiceProvider serviceProvider,
            ITextBuffer textBuffer,
            ITextView textView,
            MissingImportAnalysis imports,
            CancellationToken cancel
        ) {
            var actions = new List<ISmartTagAction>();

            try {
                foreach (var import in imports.AvailableImports) {
                    cancel.ThrowIfCancellationRequested();

                    int lastDot;

                    if ((lastDot = import.Name.LastIndexOf('.')) == -1) {
                        // simple import
                        actions.Add(new ImportSmartTagAction(import.Name, textBuffer, textView, serviceProvider));
                    } else {
                        // importing a package or member of a module
                        actions.Add(new ImportSmartTagAction(
                            import.Name.Substring(0, lastDot),
                            import.Name.Substring(lastDot + 1),
                            textBuffer,
                            textView,
                            serviceProvider
                        ));
                    }
                }

                if (actions.Any()) {
                    actions.Sort(SmartTagComparer);
                }

                return new ReadOnlyCollection<ISmartTagAction>(actions);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                Debug.Fail(ex.ToString());
                return null;
            }
        }

        public SmartTagActionSet GetResultIfComplete() {
            if (IsAborted) {
                return null;
            }
            if (_task.IsCanceled) {
                IsAborted = true;
                return null;
            }
            if (!_task.IsCompleted) {
                return null;
            }
            return new SmartTagActionSet(_task.Result);
        }

        public bool IsAborted { get; private set; }

        public void Abort() {
            IsAborted = true;
            _cancel.Cancel();
        }

        private static int SmartTagComparer(ISmartTagAction x, ISmartTagAction y) {
            var left = x as ImportSmartTagAction;
            var right = y as ImportSmartTagAction;
            if (left == null) {
                return right == null ? 0 : 1;
            } else if (right == null) {
                return -1;
            }

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

    }

    sealed class SmartTagSource : ISmartTagSource {
        private readonly ITextBuffer _textBuffer;
        private readonly IServiceProvider _serviceProvider;
        
        public SmartTagSource(IServiceProvider serviceProvider, ITextBuffer textBuffer) {
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

            if (imports == MissingImportAnalysis.Empty) {
                return;
            }

            SmartTagController controller;
            session.Properties.TryGetProperty<SmartTagController>(typeof(SmartTagController), out controller);

            var task = Volatile.Read(ref controller._curTask);
            var origTask = task;

            var snapshot = textBuffer.CurrentSnapshot;
            if (task != null &&
                task.ApplicableToSpan.GetSpan(snapshot) != imports.ApplicableToSpan.GetSpan(snapshot)) {
                // Previous task is invalid, so abort it and we'll start a
                // new one.
                task.Abort();
                session.Properties.RemoveProperty(typeof(SmartTagAugmentTask));
                task = null;
            }
                
            if (task == null) {
                task = new SmartTagAugmentTask(
                    _serviceProvider,
                    textBuffer,
                    session.TextView,
                    imports
                );
                if (Interlocked.CompareExchange(ref controller._curTask, task, origTask) != origTask) {
                    // Item has been changed by someone else, so abort
                    // Except we should always be on the UI thread here, so
                    // there should be no races.
                    Debug.Fail("Race in AugmentSmartTagSession");
                    return;
                }
            }
                
            session.ApplicableToSpan = imports.ApplicableToSpan;

            var result = task.GetResultIfComplete();
            if (result != null && Interlocked.CompareExchange(ref controller._curTask, null, task) == task) {
                // Provide results if we were the current task and we are
                // now complete
                smartTagActionSets.Add(result);
            }
        }

        public void Dispose() {
        }
    }
#endif
}
