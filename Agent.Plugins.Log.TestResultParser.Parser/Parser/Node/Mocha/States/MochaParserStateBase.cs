// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    /// <summary>
    /// Base class for a mocha test result parser state
    /// Has common methods that each state will need to use
    /// </summary>
    public abstract class MochaParserStateBase : ITestResultParserState
    {
        protected ITraceLogger logger;
        protected ITelemetryDataCollector telemetryDataCollector;
        protected ParserResetAndAttemptPublish attemptPublishAndResetParser;

        /// <summary>
        /// List of Regexs and their corresponding post successful match actions
        /// </summary>
        public virtual IEnumerable<RegexActionPair> RegexsToMatch => throw new NotImplementedException();

        /// <summary>
        /// Constructor for a mocha parser state
        /// </summary>
        /// <param name="parserResetAndAttempPublish">Delegate sent by the parser to reset the parser and attempt publication of test results</param>
        /// <param name="logger"></param>
        /// <param name="telemetryDataCollector"></param>
        protected MochaParserStateBase(ParserResetAndAttemptPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
        {
            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.attemptPublishAndResetParser = parserResetAndAttempPublish;
        }

        /// <summary>
        /// Returns a test result with the outcome set and name extracted from the match
        /// </summary>
        /// <param name="testOutcome">Outcome of the test</param>
        /// <param name="match">Match object for the test case result</param>
        /// <returns></returns>
        protected TestResult PrepareTestResult(TestOutcome testOutcome, Match match)
        {
            return new TestResult
            {
                Outcome = testOutcome,
                Name = match.Groups[RegexCaptureGroups.TestCaseName].Value
            };
        }

        /// <summary>
        /// Extracts the test run time data from the passed summary match object and populates it into the test run
        /// </summary>
        /// <param name="match">Passed summary match object</param>
        /// <param name="mochaStateContext"></param>
        protected void ExtractTestRunTime(Match match, MochaParserStateContext mochaStateContext)
        {
            // Handling parse errors is unnecessary
            var timeTaken = long.Parse(match.Groups[RegexCaptureGroups.TestRunTime].Value);

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
