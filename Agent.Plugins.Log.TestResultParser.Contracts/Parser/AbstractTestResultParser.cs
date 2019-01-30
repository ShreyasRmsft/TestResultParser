// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public abstract class AbstractTestResultParser : ITestResultParser
    {
        protected ITestRunManager _testRunManager;
        protected ITraceLogger _logger;
        protected ITelemetryDataCollector _telemetry;

        protected TimeSpan ParseOperationPermissibleThreshold = TimeSpan.FromMilliseconds(50);

        protected AbstractTestResultParser(ITestRunManager testRunManager, ITraceLogger traceLogger, ITelemetryDataCollector telemetryDataCollector)
        {
            this._testRunManager = testRunManager;
            this._logger = traceLogger;
            this._telemetry = telemetryDataCollector;
        }

        public abstract void Parse(LogData line);
        public abstract string Name { get; }
        public abstract string Version { get; }
    }
}
