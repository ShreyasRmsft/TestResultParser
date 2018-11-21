using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Agent.Plugins.TestResultParser.Bus;
using Agent.Plugins.TestResultParser.Bus.Interfaces;
using Agent.Plugins.TestResultParser.Gateway.Interfaces;
using Agent.Plugins.TestResultParser.Parser;
using Agent.Plugins.TestResultParser.Parser.Models;
using Agent.Plugins.TestResultParser.Parser.Node.Mocha;
using Agent.Plugins.TestResultParser.TestRunManger;

namespace Agent.Plugins.TestResultParser.Gateway
{
    public class DataStreamGateway : IDataStreamGateway, IBus<LogData>
    {
        public void Initialize()
        {
            //var t = new MochaTestResultParser(new TestRunManager());
            //Subscribe(t.Parse);

            throw new NotImplementedException();
        }

        public async Task ProcessDataAsync(Stream stream)
        {
            //TODO string or stream?

            // Process line
            const int bufferSize = 1024;
            using (var streamReader = new StreamReader(stream, Encoding.UTF8, true, bufferSize))
            {
                LogData logLine =  new LogData();
                while ((logLine.Line = streamReader.ReadLine()) != null)
                {
                    await _broadcast.SendAsync(logLine);
                }
            }
        }

        public void Complete()
        {
            _broadcast.Complete();
            Task.WaitAll(_subscribers.Values.Select(x => x.Completion).ToArray());
        }

        //TODO evaluate ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 }
        public Guid Subscribe(Action<LogData> handlerAction)
        {
            var handler = new ActionBlock<LogData>(handlerAction);

            _broadcast.LinkTo(handler, new DataflowLinkOptions { PropagateCompletion = true });

            return AddSubscription(handler);
        }

        public void Unsubscribe(Guid subscriptionId)
        {
            if (_subscribers.TryRemove(subscriptionId, out var subscription))
            {
                subscription.Complete();
            }
        }

        private Guid AddSubscription(ITargetBlock<LogData> subscription)
        {
            var subscriptionId = Guid.NewGuid();
            _subscribers.TryAdd(subscriptionId, subscription);
            return subscriptionId;
        }

        private readonly BroadcastBlock<LogData> _broadcast = new BroadcastBlock<LogData>(message => message);
        private readonly ConcurrentDictionary<Guid, ITargetBlock<LogData>> _subscribers = new ConcurrentDictionary<Guid, ITargetBlock<LogData>>();
    }
}
