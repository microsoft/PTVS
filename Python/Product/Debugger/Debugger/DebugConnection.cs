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

using LDP = Microsoft.PythonTools.Debugger.LegacyDebuggerProtocol;

namespace Microsoft.PythonTools.Debugger
{
	/// <summary>
	/// Handles connection from one debugger.
	/// </summary>
	internal class DebugConnection : IDisposable
	{
		private Stream _stream;
		private TextWriter _debugLog;
		private Connection _connection;
		private Thread _eventThread;
		private Thread _debuggerThread;
		private readonly object _isListeningLock = new object();
		private readonly object _eventHandlingLock = new object();
		private readonly ConcurrentQueue<EventReceivedEventArgs> _eventsPending = new ConcurrentQueue<EventReceivedEventArgs>();
		private readonly AutoResetEvent _eventsPendingWakeUp = new AutoResetEvent(false);
		private readonly ManualResetEventSlim _listeningReadyEvent = new ManualResetEventSlim(false);
		private bool _isListening;
		private bool _isAuthenticated;
		private bool _isPaused;
		private bool _isReady;
		private Guid _processGuid = Guid.Empty;

		public event EventHandler ProcessingMessagesEnded;
		public event EventHandler<LDP.DetachEvent> LegacyDetach;
		public event EventHandler<LDP.LastEvent> LegacyLast;
		public event EventHandler<LDP.RequestHandlersEvent> LegacyRequestHandlers;
		public event EventHandler<LDP.ExceptionEvent> LegacyException;
		public event EventHandler<LDP.BreakpointHitEvent> LegacyBreakpointHit;
		public event EventHandler<LDP.AsyncBreakEvent> LegacyAsyncBreak;
		public event EventHandler<LDP.ThreadCreateEvent> LegacyThreadCreate;
		public event EventHandler<LDP.ThreadExitEvent> LegacyThreadExit;
		public event EventHandler<LDP.ModuleLoadEvent> LegacyModuleLoad;
		public event EventHandler<LDP.StepDoneEvent> LegacyStepDone;
		public event EventHandler<LDP.LocalConnectedEvent> LegacyLocalConnected;
		public event EventHandler<LDP.ProcessLoadEvent> LegacyProcessLoad;
		public event EventHandler<LDP.BreakpointSetEvent> LegacyBreakpointSet;
		public event EventHandler<LDP.BreakpointFailedEvent> LegacyBreakpointFailed;
		public event EventHandler<LDP.DebuggerOutputEvent> LegacyDebuggerOutput;
		public event EventHandler<LDP.ExecutionResultEvent> LegacyExecutionResult;
		public event EventHandler<LDP.ExecutionExceptionEvent> LegacyExecutionException;
		public event EventHandler<LDP.EnumChildrenEvent> LegacyEnumChildren;
		public event EventHandler<LDP.ThreadFrameListEvent> LegacyThreadFrameList;
		public event EventHandler<LDP.RemoteConnectedEvent> LegacyRemoteConnected;
		public event EventHandler<LDP.ModulesChangedEvent> LegacyModulesChanged;

		public DebugConnection(Stream stream, TextWriter debugLog)
		{
			_stream = stream;
			_debugLog = debugLog ?? new DebugTextWriter();
			_connection = new Connection(stream, false, stream, false, null, LDP.RegisteredTypes, "DebugConnection", debugLog);
			_connection.EventReceived += _connection_EventReceived;
		}

		private void _connection_EventReceived(object sender, EventReceivedEventArgs e)
		{
			// Process events in a separate thread from the one that is processing messages
			// so that event handling code that needs access to the UI thread don't end up racing with other
			// code on the UI thread which may be waiting for a response to a request.
			_debugLog.WriteLine("PythonProcess enqueuing event: " + e.Name);
			_eventsPending.Enqueue(e);
			_eventsPendingWakeUp.Set();
		}

		/// <summary>
		/// Starts listening for debugger messages.
		/// </summary>
		public void StartListening()
		{
			_eventThread = new Thread(EventHandlingThread)
			{
				Name = "Python Debugger Event Handling " + _processGuid
			};
			_eventThread.Start();

			_debuggerThread = new Thread(MessageProcessingThread)
			{
				Name = "Python Debugger Message Processing " + _processGuid
			};
			_debuggerThread.Start();

			_listeningReadyEvent.Wait();
		}

		public async Task<T> SendRequestAsync<T>(Request<T> request, CancellationToken cancellationToken = default(CancellationToken), Action<T> postResponseAction = null)
			where T : Response, new()
		{

			// We'll never receive a response if we end up exiting out of
			// Connection.ProcessMessages during this request.
			// If that happens, we cancel the request.
			using (var stopped = new CancellationTokenSource())
			using (var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopped.Token))
			{
				EventHandler handler = (object sender, EventArgs ea) =>
				{
					try
					{
						stopped.Cancel();
					}
					catch (ObjectDisposedException)
					{
					}
				};

				ProcessingMessagesEnded += handler;

				try
				{
					// Final check before we send the request, if the state
					// changes after this then our handler will be invoked.
					lock (_isListeningLock)
					{
						if (!_isListening)
						{
							throw new OperationCanceledException();
						}
					}

					try
					{
						return await _connection.SendRequestAsync(
							request,
							linkedSource.Token,
							postResponseAction
						);
					}
					catch (IOException ex)
					{
						throw new OperationCanceledException(ex.Message, ex);
					}
					catch (ObjectDisposedException ex)
					{
						throw new OperationCanceledException(ex.Message, ex);
					}
				}
				finally
				{
					ProcessingMessagesEnded -= handler;
				}
			}
		}

		internal void SetProcess(Guid debugId)
		{
			_processGuid = debugId;
		}

		private void MessageProcessingThread()
		{
			MessageProcessingThreadAsync().WaitAndUnwrapExceptions();
		}

		private async Task MessageProcessingThreadAsync()
		{
			_debugLog.WriteLine("MessageProcessingThreadAsync Started");

			try
			{
				Debug.Assert(_connection != null);
				if (_connection != null)
				{
					lock (_isListeningLock)
					{
						_isListening = true;
						_listeningReadyEvent.Set();
					}
					await _connection.ProcessMessages();
				}
			}
			catch (IOException)
			{
			}
			catch (ObjectDisposedException ex)
			{
				// Socket or stream have been disposed
				Debug.Assert(
					ex.ObjectName == typeof(NetworkStream).FullName ||
					ex.ObjectName == typeof(Socket).FullName,
					"Accidentally handled ObjectDisposedException(" + ex.ObjectName + ")"
				);
			}
			catch (Exception ex) when (!ex.IsCriticalException())
			{
				Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(DebugConnection)));
			}
			finally
			{
				lock (_isListeningLock)
				{
					// Exit out of the event handling thread
					_isListening = false;
					_eventsPendingWakeUp.Set();
				}
			}

			ProcessingMessagesEnded?.Invoke(this, EventArgs.Empty);

			_debugLog.WriteLine("MessageProcessingThreadAsync Ended");
		}

		private void EventHandlingThread()
		{
			_debugLog.WriteLine("EventHandlingThread Started");
			_listeningReadyEvent.Wait();

			while (true)
			{
				bool paused;
				lock (_isListeningLock)
				{
					if (!_isListening)
					{
						break;
					}

					paused = _isPaused;
				}

				EventReceivedEventArgs eventReceived = null;
				if (!paused && _eventsPending.TryDequeue(out eventReceived))
				{
					try
					{
						HandleEvent(eventReceived);
					}
					catch (OperationCanceledException)
					{
					}
					catch (Exception e) when (!e.IsCriticalException())
					{
						Debug.Fail(string.Format("Error while handling debugger event '{0}'.\n{1}", eventReceived.Name, e));
					}
				}
				else
				{
					_eventsPendingWakeUp.WaitOne();
				}
			}

			_debugLog.WriteLine("EventHandlingThread Ended");
		}

		private void HandleEvent(EventReceivedEventArgs e)
		{
			_debugLog.WriteLine(string.Format("PythonProcess handling event: {0}", e.Event.name));
			lock (_eventHandlingLock)
			{
				Debug.Assert(e.Event.name == LDP.LocalConnectedEvent.Name || e.Event.name == LDP.RemoteConnectedEvent.Name || _isAuthenticated);
				switch (e.Event.name)
				{
					case LDP.AsyncBreakEvent.Name:
						LegacyAsyncBreak?.Invoke(this, (LDP.AsyncBreakEvent)e.Event);
						break;
					case LDP.BreakpointFailedEvent.Name:
						LegacyBreakpointFailed?.Invoke(this, (LDP.BreakpointFailedEvent)e.Event);
						break;
					case LDP.BreakpointHitEvent.Name:
						LegacyBreakpointHit?.Invoke(this, (LDP.BreakpointHitEvent)e.Event);
						break;
					case LDP.BreakpointSetEvent.Name:
						LegacyBreakpointSet?.Invoke(this, (LDP.BreakpointSetEvent)e.Event);
						break;
					case LDP.DebuggerOutputEvent.Name:
						LegacyDebuggerOutput?.Invoke(this, (LDP.DebuggerOutputEvent)e.Event);
						break;
					case LDP.DetachEvent.Name:
						LegacyDetach?.Invoke(this, (LDP.DetachEvent)e.Event);
						break;
					case LDP.EnumChildrenEvent.Name:
						LegacyEnumChildren?.Invoke(this, (LDP.EnumChildrenEvent)e.Event);
						break;
					case LDP.ExceptionEvent.Name:
						LegacyException?.Invoke(this, (LDP.ExceptionEvent)e.Event);
						break;
					case LDP.ExecutionExceptionEvent.Name:
						LegacyExecutionException?.Invoke(this, (LDP.ExecutionExceptionEvent)e.Event);
						break;
					case LDP.ExecutionResultEvent.Name:
						LegacyExecutionResult?.Invoke(this, (LDP.ExecutionResultEvent)e.Event);
						break;
					case LDP.LastEvent.Name:
						LegacyLast?.Invoke(this, (LDP.LastEvent)e.Event);
						break;
					case LDP.LocalConnectedEvent.Name:
						LegacyLocalConnected?.Invoke(this, (LDP.LocalConnectedEvent)e.Event);
						break;
					case LDP.ModuleLoadEvent.Name:
						LegacyModuleLoad?.Invoke(this, (LDP.ModuleLoadEvent)e.Event);
						break;
					case LDP.ProcessLoadEvent.Name:
						LegacyProcessLoad?.Invoke(this, (LDP.ProcessLoadEvent)e.Event);
						break;
					case LDP.RemoteConnectedEvent.Name:
						LegacyRemoteConnected?.Invoke(this, (LDP.RemoteConnectedEvent)e.Event);
						break;
					case LDP.ModulesChangedEvent.Name:
						LegacyModulesChanged?.Invoke(this, (LDP.ModulesChangedEvent)e.Event);
						break;
					case LDP.RequestHandlersEvent.Name:
						LegacyRequestHandlers?.Invoke(this, (LDP.RequestHandlersEvent)e.Event);
						break;
					case LDP.StepDoneEvent.Name:
						LegacyStepDone?.Invoke(this, (LDP.StepDoneEvent)e.Event);
						break;
					case LDP.ThreadCreateEvent.Name:
						LegacyThreadCreate?.Invoke(this, (LDP.ThreadCreateEvent)e.Event);
						break;
					case LDP.ThreadFrameListEvent.Name:
						LegacyThreadFrameList?.Invoke(this, (LDP.ThreadFrameListEvent)e.Event);
						break;
					case LDP.ThreadExitEvent.Name:
						LegacyThreadExit?.Invoke(this, (LDP.ThreadExitEvent)e.Event);
						break;
					default:
						Debug.Fail("Unknown event: {0}".FormatUI(e.Event.name));
						break;
				}
			}
		}

		public void Dispose()
		{
			// Avoiding ?. syntax because FxCop doesn't understand it
			if (_connection != null)
			{
				_connection.Dispose();
			}
			// The connection dispose above won't close the stream, because we don't give it ownership
			// Disposing of the stream will cause the message thread to stop, which causes the event thread to stop
			_stream?.Dispose();
			WaitForWorkerThreads();
			_eventsPendingWakeUp.Dispose();
			_listeningReadyEvent.Dispose();
		}

		public Stream DetachStream()
		{
			var stream = _stream;
			_stream = null;
			return stream;
		}

		private void WaitForWorkerThreads()
		{
			if (!_debuggerThread.Join(5000))
			{
				Debug.Fail("Failed to terminate debugger message thread");
			}
			if (!_eventThread.Join(5000))
			{
				Debug.Fail("Failed to terminate debugger event thread");
			}
		}

		internal void Authenticated()
		{
			lock (_isListeningLock)
			{
				_isAuthenticated = true;
				if (!_isReady)
				{
					_isPaused = true;
				}
			}
		}

		internal void WaitForAuthentication()
		{
			lock (_isListeningLock)
			{
				_isPaused = false;
				_isReady = true;
			}
			_eventsPendingWakeUp.Set();
		}
	}
}
