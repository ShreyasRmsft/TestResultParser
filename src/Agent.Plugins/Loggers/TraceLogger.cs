namespace Agent.Plugins.TestResultParser.Loggers
{
    using System;
    using System.Threading.Tasks;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;

    public class TraceLogger : ITraceLogger
    {
        private static ITraceLogger instance;

        /// <summary>
        /// Gets the singleton instance of diagnostics data collector.
        /// </summary>
        public static ITraceLogger Instance
        {
            get => instance ?? (instance = new TraceLogger());
            internal set => instance = value;
        }

        /// <inheritdoc />
        public void Error(string error)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Info(string text)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task PublishDiagnosticDataAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Verbose(string text)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Warning(string text)
        {
            throw new NotImplementedException();
        }
    }
}
