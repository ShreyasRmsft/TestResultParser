// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha.States
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public abstract class MochaParserStateBase : ITestResultParserState
    {
        protected ITraceLogger logger;
        protected ITelemetryDataCollector telemetryDataCollector;
        protected ParserResetAndAttempPublish attemptPublishAndResetParser;

        protected MochaParserStateBase(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.attemptPublishAndResetParser = parserResetAndAttempPublish;
        }

        public virtual List<RegexActionPair> RegexesToMatch => throw new NotImplementedException();

        protected void PrepareTestResult(TestResult testResult, TestOutcome testOutcome, Match match)
        {
            testResult.Outcome = testOutcome;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;
        }

        protected void ExtractTestRunTime(Match match, MochaTestResultParserStateContext mochaStateContext)
        {
            // Handling parse errors is unnecessary
            long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken);

            // Store time taken based on the unit used
            switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
            {
                case "ms":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                    break;

                case "s":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                    break;

                case "m":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                    break;

                case "h":
                    mochaStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                    break;
            }
        }
    }
}
