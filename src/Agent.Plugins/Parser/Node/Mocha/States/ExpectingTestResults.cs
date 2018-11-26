// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha.States
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public class ExpectingTestResults : MochaParserStateBase
    {
        public override List<RegexActionPair> RegexesToMatch { get; }

        public ExpectingTestResults(ParserResetAndAttempPublish parserResetAndAttempPublish)
            : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingTestResults(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttempPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };

            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.attemptPublishAndResetParser = parserResetAndAttempPublish;
        }

        private Enum PassedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            var testResult = new TestResult();

            PrepareTestResult(testResult, TestOutcome.Passed, match);
            mochaStateContext.TestRun.PassedTests.Add(testResult);

            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            var testResult = new TestResult();

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // In the event the failed test case number does not match the expected test case number log an error
            if (testCaseNumber != mochaStateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestResults : Expecting failed test case with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not add it to our list of test cases
                if (testCaseNumber != 1)
                {
                    return MochaTestResultParserState.ExpectingTestResults;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                // This is something we might choose to change if we realize there is a chance we can get such false detections often in the middle of a run
                this.attemptPublishAndResetParser();
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            mochaStateContext.LastFailedTestCaseNumber++;

            PrepareTestResult(testResult, TestOutcome.Failed, match);
            mochaStateContext.TestRun.FailedTests.Add(testResult);

            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum PendingTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            var testResult = new TestResult();

            PrepareTestResult(testResult, TestOutcome.Skipped, match);
            mochaStateContext.TestRun.SkippedTests.Add(testResult);

            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            this.logger.Info($"MochaTestResultParser : ExpectingTestResults : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed/pending tests summary";
            mochaStateContext.LastFailedTestCaseNumber = 0;

            this.logger.Info("MochaTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestRunSummary.");

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestResults : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            return MochaTestResultParserState.ExpectingTestRunSummary;
        }
    }
}
