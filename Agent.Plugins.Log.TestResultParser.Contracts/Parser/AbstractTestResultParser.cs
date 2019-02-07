// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public abstract class AbstractTestResultParser : ITestResultParser
    {
        protected ITestRunManager TestRunManager;
        protected ITraceLogger Logger;
        protected ITelemetryDataCollector Telemetry;

        protected TimeSpan ParseOperationPermissibleThreshold = TimeSpan.FromMilliseconds(1);

        protected AbstractTestResultParser(ITestRunManager testRunManager, ITraceLogger traceLogger, ITelemetryDataCollector telemetryDataCollector)
        {
            TestRunManager = testRunManager;
            Logger = traceLogger;
            Telemetry = telemetryDataCollector;
        }

        public abstract void Parse(LogData line);
        public abstract string Name { get; }
        public abstract string Version { get; }
    }
}
