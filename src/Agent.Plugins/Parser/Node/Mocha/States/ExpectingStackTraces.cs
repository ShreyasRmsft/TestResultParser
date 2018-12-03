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

    public class ExpectingStackTraces : MochaParserStateBase
    {
        /// <inheritdoc />
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        public ExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttempPublish) : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <inheritdoc />
        public ExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector) 
            : base(parserResetAndAttempPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };
        }

        private Enum PassedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a passed test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data
            // This check is safety check for when we try to parse stack trace contents, as of now it will always evaluate to true
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingStackTraces :  Expecting stack traces but found passed test case instead at line {mochaStateContext.CurrentLineNumber}.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.Passed, match);
            mochaStateContext.TestRun.PassedTests.Add(testResult);

            return MochaTestResultParserStates.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // Handling parse errors is unnecessary
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // In the event the failed test case number does not match the expected test case number log an error
            if (testCaseNumber != mochaStateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingStackTraces : Expecting stack trace with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedStackTraceNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not consider it a valid stack trace
                if (testCaseNumber != 1)
                {
                    // If we are parsing stack traces then we should not return this as
                    // a successful match. If we do so then stack trace addition will not 
                    // happen for the current line
                    return MochaTestResultParserStates.ExpectingStackTraces;
                }

                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.AttemptPublishAndResetParser,
                    new List<string> { $"Expecting stack trace with number {mochaStateContext.LastFailedTestCaseNumber} but found {testCaseNumber} instead" });

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                this.attemptPublishAndResetParser();

                mochaStateContext.LastFailedTestCaseNumber++;

                var testResult = PrepareTestResult(TestOutcome.Failed, match);
                mochaStateContext.TestRun.FailedTests.Add(testResult);

                return MochaTestResultParserStates.ExpectingTestResults;
            }

            mochaStateContext.LastFailedTestCaseNumber++;

            // As of now we are ignoring stack traces
            // Otherwise parsing stacktrace code will go here

            mochaStateContext.StackTracesToSkipParsingPostSummary--;

            if (mochaStateContext.StackTracesToSkipParsingPostSummary == 0)
            {
                // We can also choose to ignore extra failures post summary if the number is not 1
                this.attemptPublishAndResetParser();
                return MochaTestResultParserStates.ExpectingTestResults;
            }

            return MochaTestResultParserStates.ExpectingStackTraces;
        }

        private Enum PendingTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a pending test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data

            // This check is safety check for when we try to parse stack trace contents
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error("MochaTestResultParser : ExpectingStackTraces : Expecting stack traces but found pending test case instead.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.Skipped, match);
            mochaStateContext.TestRun.SkippedTests.Add(testResult);

            return MochaTestResultParserStates.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;
            this.logger.Info($"MochaTestResultParser : ExpectingStackTraces : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            // If we were expecting more stack traces but got summary instead
            if (mochaStateContext.StackTracesToSkipParsingPostSummary != 0)
            {
                this.logger.Error("MochaTestResultParser : ExpectingStackTraces : Expecting stack traces but found passed summary instead.");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            this.attemptPublishAndResetParser();

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed/pending tests summary";

            // Handling parse errors is unnecessary
            var totalPassed = int.Parse(match.Groups[RegexCaptureGroups.PassedTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            this.logger.Info("MochaTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestRunSummary.");
            return MochaTestResultParserStates.ExpectingTestRunSummary;
        }
    }
}
