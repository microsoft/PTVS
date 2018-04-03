using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Text.Operations;

namespace TestUtilities.Mocks {
    [ExcludeFromCodeCoverage]
    [Export(typeof(ITextUndoHistoryRegistry))]
    [Export(typeof(MockTextUndoHistoryRegistry))]
    public class MockTextUndoHistoryRegistry : ITextUndoHistoryRegistry {
        #region Private Fields
        private readonly Dictionary<ITextUndoHistory, int> _histories;
        private readonly Dictionary<KeyWeakReference, ITextUndoHistory> _weakContextMapping;
        private readonly Dictionary<object, ITextUndoHistory> _strongContextMapping;
        #endregion // Private Fields

        public MockTextUndoHistoryRegistry() {
            // set up the list of histories
            _histories = new Dictionary<ITextUndoHistory, int>();

            // set up the mappings from contexts to histories
            _weakContextMapping = new Dictionary<KeyWeakReference, ITextUndoHistory>();
            _strongContextMapping = new Dictionary<object, ITextUndoHistory>();
        }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<ITextUndoHistory> Histories => _histories.Keys;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public ITextUndoHistory RegisterHistory(object context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            return RegisterHistory(context, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="keepAlive"></param>
        /// <returns></returns>
        public ITextUndoHistory RegisterHistory(object context, bool keepAlive) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            ITextUndoHistory result;

            if (_strongContextMapping.ContainsKey(context)) {
                result = _strongContextMapping[context];

                if (!keepAlive) {
                    _strongContextMapping.Remove(context);
                    _weakContextMapping.Add(new KeyWeakReference(context), result);
                }
            } else if (_weakContextMapping.ContainsKey(new KeyWeakReference(context))) {
                result = _weakContextMapping[new KeyWeakReference(context)];

                if (keepAlive) {
                    _weakContextMapping.Remove(new KeyWeakReference(context));
                    _strongContextMapping.Add(context, result);
                }
            } else {
                result = new MockTextUndoHistory(this);
                _histories.Add(result, 1);

                if (keepAlive) {
                    _strongContextMapping.Add(context, result);
                } else {
                    _weakContextMapping.Add(new KeyWeakReference(context), result);
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public ITextUndoHistory GetHistory(object context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            ITextUndoHistory result;

            if (_strongContextMapping.ContainsKey(context)) {
                result = _strongContextMapping[context];
            } else if (_weakContextMapping.ContainsKey(new KeyWeakReference(context))) {
                result = _weakContextMapping[new KeyWeakReference(context)];
            } else {
                throw new InvalidOperationException("Cannot find context in registry");
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        /// <returns></returns>
        public bool TryGetHistory(object context, out ITextUndoHistory history) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            ITextUndoHistory result = null;

            if (_strongContextMapping.ContainsKey(context)) {
                result = _strongContextMapping[context];
            } else if (_weakContextMapping.ContainsKey(new KeyWeakReference(context))) {
                result = _weakContextMapping[new KeyWeakReference(context)];
            }

            history = result;
            return (result != null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        public void AttachHistory(object context, ITextUndoHistory history) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (history == null) {
                throw new ArgumentNullException(nameof(history));
            }

            AttachHistory(context, history, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        /// <param name="keepAlive"></param>
        public void AttachHistory(object context, ITextUndoHistory history, bool keepAlive) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (history == null) {
                throw new ArgumentNullException(nameof(history));
            }

            if (_strongContextMapping.ContainsKey(context) || _weakContextMapping.ContainsKey(new KeyWeakReference(context))) {
                throw new InvalidOperationException("Attached history already containst context");
            }

            if (!_histories.ContainsKey(history)) {
                _histories.Add(history, 1);
            } else {
                ++_histories[history];
            }

            if (keepAlive) {
                _strongContextMapping.Add(context, history);
            } else {
                _weakContextMapping.Add(new KeyWeakReference(context), history);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="history"></param>
        public void RemoveHistory(ITextUndoHistory history) {
            if (history == null) {
                throw new ArgumentNullException(nameof(history));
            }

            if (!_histories.ContainsKey(history)) {
                return;
            }

            _histories.Remove(history);

            List<object> strongToRemove = _strongContextMapping.Keys.Where(o => ReferenceEquals(_strongContextMapping[o], history)).ToList();

            strongToRemove.ForEach(o => _strongContextMapping.Remove(o));

            var weakToRemove = _weakContextMapping.Keys.Where(o => ReferenceEquals(_weakContextMapping[o], history)).ToList();

            weakToRemove.ForEach(o => _weakContextMapping.Remove(o));
        }
    }
}