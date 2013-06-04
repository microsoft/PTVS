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

namespace Microsoft.VisualStudioTools.Project
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Windows.Forms;

    internal sealed class UIThread : IDisposable
    {
        private WindowsFormsSynchronizationContext synchronizationContext;
        private bool isUnitTestingMode;
        private Thread uithread;
#if DEBUG
        /// <summary>
        /// Stack trace when synchronizationContext was captured
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private StackTrace captureStackTrace;
#endif

        /// <summary>
        /// RunSync puts orignal exception stacktrace to Exception.Data by this key if action throws on UI thread
        /// </summary>
        /// WrappedStacktraceKey is a string to keep exception serializable.
        private const string WrappedStacktraceKey = "$$Microsoft.VisualStudio.Package.UIThread.WrappedStacktraceKey$$";

        /// <summary>
        /// The singleton instance.
        /// </summary>
        private static volatile UIThread instance = new UIThread();

        internal UIThread()
        {
            this.Initialize();
        }

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static UIThread Instance
        {
            get
            {
                return instance;
            }
        }

        /// <summary>
        /// Checks whether this is the UI thread.
        /// </summary>
        public bool IsUIThread
        {
            get { return this.uithread == System.Threading.Thread.CurrentThread; }
        }

        #region IDisposable Members
        /// <summary>
        /// Dispose implementation.
        /// </summary>
        public void Dispose()
        {
            if (this.synchronizationContext != null)
            {
                this.synchronizationContext.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// Initializes unit testing mode for this object
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal void InitUnitTestingMode()
        {
            Debug.Assert(this.synchronizationContext == null, "Context has already been captured; too late to InitUnitTestingMode");
            this.isUnitTestingMode = true;
        }

        [Conditional("DEBUG")]
        internal void MustBeCalledFromUIThread()
        {
            Debug.Assert(this.uithread == System.Threading.Thread.CurrentThread || this.isUnitTestingMode, "This must be called from the GUI thread");
        }

        /// <summary>
        /// Runs an action asynchronously on an associated forms synchronization context.
        /// </summary>
        /// <param name="a">The action to run</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal void Run(Action a)
        {
            if (this.isUnitTestingMode)
            {
                a();
                return;
            }
            Debug.Assert(this.synchronizationContext != null, "The SynchronizationContext must be captured before calling this method");
#if DEBUG
            StackTrace stackTrace = new StackTrace(true);
#endif
            this.synchronizationContext.Post(delegate(object ignore) {
                try
                {
                    this.MustBeCalledFromUIThread();
                    a();
                }
#if DEBUG
                catch (Exception e)
                {
                    // swallow, random exceptions should not kill process
                    Debug.Assert(false, string.Format(CultureInfo.InvariantCulture, "UIThread.Run caught and swallowed exception: {0}\n\noriginally invoked from stack:\n{1}", e.ToString(), stackTrace.ToString()));
                }
#else
                catch (Exception)
                {
                    // swallow, random exceptions should not kill process
                }
#endif
            }, null);

        }

        /// <summary>
        /// Runs an action synchronously on an associated forms synchronization context
        /// </summary>
        /// <param name="a">The action to run.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal void RunSync(Action a)
        {
            // if we're already on the UI thread run immediately - this prevents
            // re-entrancy at unexpected times when we're already on the UI thread.
            if (this.isUnitTestingMode || 
                uithread == Thread.CurrentThread)   
            {
                a();
                return;
            }
            Exception exn = null; ;
            Debug.Assert(this.synchronizationContext != null, "The SynchronizationContext must be captured before calling this method");

            // Send on UI thread will execute immediately.
            this.synchronizationContext.Send(ignore => {
                try
                {
                    this.MustBeCalledFromUIThread();
                    a();
                }
                catch (Exception e)
                {
                    exn = e;
                }
            }, null
            );
            if (exn != null)
            {
                // throw exception on calling thread, preserve stacktrace
                if (!exn.Data.Contains(WrappedStacktraceKey)) exn.Data[WrappedStacktraceKey] = exn.StackTrace;
                throw exn;
            }
        }

        /// <summary>
        /// Runs an action synchronously on an associated forms synchronization context
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="a">The function</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal T RunSync<T>(Func<T> a) {
            T retValue = default(T);
            if (this.isUnitTestingMode) {
                return a();                
            }
            Exception exn = null; ;
            Debug.Assert(this.synchronizationContext != null, "The SynchronizationContext must be captured before calling this method");

            // Send on UI thread will execute immediately.
            this.synchronizationContext.Send(ignore => {
                try {
                    this.MustBeCalledFromUIThread();
                    retValue = a();
                } catch (Exception e) {
                    exn = e;
                }
            }, null
            );
            if (exn != null) {
                // throw exception on calling thread, preserve stacktrace
                if (exn.Data != null && !exn.Data.Contains(WrappedStacktraceKey)) exn.Data[WrappedStacktraceKey] = exn.StackTrace;
                throw exn;
            }
            return retValue;
        }

        /// <summary>
        /// Initializes this object.
        /// </summary>
        private void Initialize()
        {
            if (this.isUnitTestingMode) return;
            this.uithread = System.Threading.Thread.CurrentThread;

            if (this.synchronizationContext == null)
            {
#if DEBUG
                // This is a handy place to do this, since the product and all interesting unit tests
                // must go through this code path.
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(delegate(object sender, UnhandledExceptionEventArgs args) {
                    if (args.IsTerminating)
                    {
                        string s = String.Format(CultureInfo.InvariantCulture, "An unhandled exception is about to terminate the process.  Exception info:\n{0}", args.ExceptionObject.ToString());
                        Debug.Assert(false, s);
                    }
                });

                this.captureStackTrace = new StackTrace(true);
#endif
                this.synchronizationContext = new WindowsFormsSynchronizationContext();
            }
            else
            {
                // Make sure we are always capturing the same thread.
                Debug.Assert(this.uithread == Thread.CurrentThread);
            }
        }
    }
}