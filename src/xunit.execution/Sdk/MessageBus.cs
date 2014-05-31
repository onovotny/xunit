﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Xunit.Sdk
{
    /// <summary>
    /// This is an internal class, and is not intended to be called from end-user code.
    /// </summary>
    public class MessageBus : IMessageBus
    {
        volatile bool continueRunning = true;
        readonly IMessageSink messageSink;
        readonly ConcurrentQueue<IMessageSinkMessage> reporterQueue = new ConcurrentQueue<IMessageSinkMessage>();
        readonly Task reporterThreadTask;
        readonly AutoResetEvent reporterWorkEvent = new AutoResetEvent(initialState: false);
        volatile bool shutdownRequested;

        /// <summary/>
        public MessageBus(IMessageSink messageSink)
        {
            this.messageSink = messageSink;

            reporterThreadTask = Task.Run(() => ReporterWorker());
        }

        private void DispatchMessages()
        {
            IMessageSinkMessage message;
            while (reporterQueue.TryDequeue(out message))
                try
                {
                    Debug.WriteLine("Sending Message: " + message.GetType()
                                                                   .GetTypeInfo()
                                                                   .Name);
                    if (!messageSink.OnMessage(message))
                        continueRunning = false;
                }
                catch { }
        }
        /// <summary/>
        public void Dispose()
        {
            shutdownRequested = true;

            reporterWorkEvent.Set();
            reporterThreadTask.Wait();

            reporterWorkEvent.Dispose();
        }

        /// <summary/>
        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (shutdownRequested)
                throw new ObjectDisposedException("MessageBus");

            reporterQueue.Enqueue(message);
            reporterWorkEvent.Set();
            return continueRunning;
        }

        void ReporterWorker()
        {
            while (!shutdownRequested)
            {
                reporterWorkEvent.WaitOne();
                DispatchMessages();
            }

            // One final dispatch pass
            DispatchMessages();
        }
    }
}