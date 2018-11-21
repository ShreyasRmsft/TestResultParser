using System;
using System.IO;
using System.Threading.Tasks;
using Agent.Plugins.TestResultParser.Gateway;
using Agent.Plugins.TestResultParser.Parser;
using Agent.Plugins.TestResultParser.Parser.Interfaces;
using Agent.Plugins.TestResultParser.Parser.Models;

namespace Agent.Plugins.TestResultParser.Bus
{
    public static class DataStreamGatewayExtensions
    {
        public static Task SendAsync(this DataStreamGateway bus, Stream message)
        {
            return bus.ProcessDataAsync(message);
        }

        public static Guid Subscribe(this DataStreamGateway bus, Func<Action<LogData>> handlerActionFactory)
        {
            return bus.Subscribe(message => handlerActionFactory().Invoke(message));
        }

        public static Guid Subscribe<THandler>(this DataStreamGateway bus) where THandler : ITestResultParser, new()
        {
            return bus.Subscribe(message => new THandler().Parse(message));
        }
    }
}
