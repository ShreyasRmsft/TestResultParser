namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : ITestResultParser
    {
        private ITelemetryDataCollector telemetryDataCollector;
        private ITraceLogger diagnosticDataCollector;

        public PythonTestResultParser() : this(TelemetryDataCollector.Instance, TraceLogger.Instance)
        {
        }

        internal PythonTestResultParser(ITelemetryDataCollector telemetryCollector, ITraceLogger diagnosticsCollector)
        {
            this.telemetryDataCollector = telemetryCollector;
            this.diagnosticDataCollector = diagnosticsCollector;
        }

        public string Name => throw new NotImplementedException();

        public string Version => throw new NotImplementedException();

        /// <summary>
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        public void Parse(LogData Line)
        {
            throw new NotImplementedException();
        }
    }
}
