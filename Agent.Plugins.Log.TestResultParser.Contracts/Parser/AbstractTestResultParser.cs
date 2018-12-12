// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public abstract class AbstractTestResultParser : ITestResultParser
    {
        protected ITestRunManager testRunManager;
        protected ITraceLogger logger;
        protected ITelemetryDataCollector telemetry;

        protected AbstractTestResultParser(ITestRunManager testRunManager, ITraceLogger traceLogger, ITelemetryDataCollector telemetryDataCollector)
        {
            this.testRunManager = testRunManager;
            this.logger = traceLogger;
            this.telemetry = telemetryDataCollector;
        }

        public abstract void Parse(LogData line);
        public abstract string Name { get; }
        public abstract string Version { get; }
    }
}
