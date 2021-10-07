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

namespace TestUtilities.Mocks
{
	[ExcludeFromCodeCoverage]
	[Export(typeof(ITextUndoHistoryRegistry))]
	[Export(typeof(MockTextUndoHistoryRegistry))]
	public class MockTextUndoHistoryRegistry : ITextUndoHistoryRegistry
	{
		private readonly Dictionary<ITextUndoHistory, int> _histories;
		private readonly Dictionary<KeyWeakReference, ITextUndoHistory> _weakContextMapping;
		private readonly Dictionary<object, ITextUndoHistory> _strongContextMapping;

		public MockTextUndoHistoryRegistry()
		{
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
		public ITextUndoHistory RegisterHistory(object context)
		{
			if (context == null)
			{
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
		public ITextUndoHistory RegisterHistory(object context, bool keepAlive)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			if (_strongContextMapping.TryGetValue(context, out var result))
			{
				if (!keepAlive)
				{
					_strongContextMapping.Remove(context);
					_weakContextMapping.Add(new KeyWeakReference(context), result);
				}

				return result;
			}

			KeyWeakReference reference = new KeyWeakReference(context);
			if (_weakContextMapping.TryGetValue(reference, out result))
			{
				if (keepAlive)
				{
					_weakContextMapping.Remove(reference);
					_strongContextMapping.Add(context, result);
				}

				return result;
			}

			result = new MockTextUndoHistory(this);
			_histories.Add(result, 1);

			if (keepAlive)
			{
				_strongContextMapping.Add(context, result);
			}
			else
			{
				_weakContextMapping.Add(reference, result);
			}

			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public ITextUndoHistory GetHistory(object context)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			if (!TryGetHistory(context, out ITextUndoHistory history))
			{
				throw new InvalidOperationException("Cannot find context in registry");
			}

			return history;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		/// <param name="history"></param>
		/// <returns></returns>
		public bool TryGetHistory(object context, out ITextUndoHistory history)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			return _strongContextMapping.TryGetValue(context, out history)
				|| _weakContextMapping.TryGetValue(new KeyWeakReference(context), out history);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		/// <param name="history"></param>
		public void AttachHistory(object context, ITextUndoHistory history)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			if (history == null)
			{
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
		public void AttachHistory(object context, ITextUndoHistory history, bool keepAlive)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			if (history == null)
			{
				throw new ArgumentNullException(nameof(history));
			}

			if (_strongContextMapping.ContainsKey(context) || _weakContextMapping.ContainsKey(new KeyWeakReference(context)))
			{
				throw new InvalidOperationException("Attached history already containst context");
			}

			if (!_histories.ContainsKey(history))
			{
				_histories.Add(history, 1);
			}
			else
			{
				++_histories[history];
			}

			if (keepAlive)
			{
				_strongContextMapping.Add(context, history);
			}
			else
			{
				_weakContextMapping.Add(new KeyWeakReference(context), history);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="history"></param>
		public void RemoveHistory(ITextUndoHistory history)
		{
			if (history == null)
			{
				throw new ArgumentNullException(nameof(history));
			}

			if (!_histories.ContainsKey(history))
			{
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