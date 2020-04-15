using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Discord;

namespace Discord_Channel_Backup
{
    /// <summary>
    /// Thread that downloads messages.
    /// Must create a WriterThread first and pass the reference of the BlockingCollection<>.
    /// </summary>
    public class MessageDownloadThread
    {
        private readonly IMessageChannel _channel;
        private readonly BlockingCollection<List<IMessage>> _blockingQueue;
        private readonly IMessage _start;
        private readonly ThreadSafeBool _error;

        /// <summary>
        /// Inits the thread.
        /// </summary>
        /// <param name="channel">The channel to backup.</param>
        /// <param name="blockingQueue">The WriterThread's message queue.</param>
        /// <param name="start">The message to start downloading from.</param>
        public MessageDownloadThread(IMessageChannel channel, BlockingCollection<List<IMessage>> blockingQueue, IMessage start, ThreadSafeBool error)
        {
            _channel = channel;
            _blockingQueue = blockingQueue;
            _start = start;
            _error = error;
        }

        /// <summary>
        /// Call to start main download loop.
        /// Note that this should not be called again after a Run(); create a new thread instead.
        /// </summary>
        public async void Run()
        {
            Thread.CurrentThread.IsBackground = false;
            IMessage currentStart = _start;

            while (true)
            {
                IEnumerable<IMessage> msgEnum;
                try
                {
                    // GetMessagesAsync() may take a while if rate limiting happens
                    msgEnum = await _channel.GetMessagesAsync(currentStart, Direction.Before).FlattenAsync();
                }
                catch (Exception ex)
                {
                    _blockingQueue.CompleteAdding();
                    Console.WriteLine("An error occured while fetching messages. Try restarting the program in a bit.");
                    Console.WriteLine($"\tException: {ex}");
                    _error.Value = true;
                    return;
                }


                List<IMessage> messages = msgEnum.ToList();
                if (messages.Count == 0)
                {
                    _blockingQueue.CompleteAdding();
                    break;
                }

                _blockingQueue.Add(messages);
                currentStart = messages[messages.Count - 1];
            }

            Console.WriteLine("Done downloading all messages!");
        }
    }
}
