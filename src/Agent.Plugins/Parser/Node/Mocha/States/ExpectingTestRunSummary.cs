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

    public class ExpectingTestRunSummary : MochaParserStateBase
    {
        public override List<RegexActionPair> RegexesToMatch { get; }

        public ExpectingTestRunSummary(ParserResetAndAttempPublish parserResetAndAttempPublish) : this(parserResetAndAttempPublish, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {

        }

        public ExpectingTestRunSummary(ParserResetAndAttempPublish parserResetAndAttempPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttempPublish, logger, telemetryDataCollector)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestsSummary, PendingTestsSummaryMatched),
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestsSummary, FailedTestsSummaryMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestsSummary, PassedTestsSummaryMatched),
            };

            this.logger = logger;
            this.telemetryDataCollector = telemetryDataCollector;
            this.attemptPublishAndResetParser = parserResetAndAttempPublish;
        }

        private Enum PassedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a passed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless.
            this.attemptPublishAndResetParser();

            var testResult = new TestResult();
            PrepareTestResult(testResult, TestOutcome.Passed, match);

            mochaStateContext.TestRun.PassedTests.Add(testResult);
            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a failed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            var testResult = new TestResult();

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
            // as a match but do not add it to our list of test cases
            if (testCaseNumber != 1)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Expecting failed test case with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                return MochaTestResultParserState.ExpectingTestResults;
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

            // If a pending test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless.
            this.attemptPublishAndResetParser();

            var testResult = new TestResult();
            PrepareTestResult(testResult, TestOutcome.Skipped, match);

            mochaStateContext.TestRun.SkippedTests.Add(testResult);
            return MochaTestResultParserState.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            // Passed tests summary is not expected soon after encountering passed tests summary, atleast one test case should have been there.
            this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Was expecting atleast one test case before encountering" +
                $" summary again at line {mochaStateContext.CurrentLineNumber}");
            this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                new List<int> { mochaStateContext.TestRun.TestRunId }, true);

            // Reset the parser and start over
            this.attemptPublishAndResetParser();

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed/pending tests summary";

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            this.logger.Info("MochaTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestRunSummary.");
            return MochaTestResultParserState.ExpectingTestRunSummary;
        }

        private Enum PendingTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Pending tests summary encountered at line {mochaStateContext.CurrentLineNumber}.");
            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending);

            mochaStateContext.TestRun.TestRunSummary.TotalSkipped = totalPending;

            // If encountered skipped tests does not match summary fire telemtry
            if (mochaStateContext.TestRun.TestRunSummary.TotalSkipped != mochaStateContext.TestRun.SkippedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Pending tests count does not match pending summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            return MochaTestResultParserState.ExpectingTestRunSummary;
        }

        private Enum FailedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Failed tests summary encountered at line {mochaStateContext.CurrentLineNumber}.");
            mochaStateContext.LinesWithinWhichMatchIsExpected = 0;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed);

            mochaStateContext.TestRun.TestRunSummary.TotalFailed = totalFailed;
            mochaStateContext.StackTracesToSkipParsingPostSummary = totalFailed;

            // If encountered failed tests does not match summary fire telemtry
            if (mochaStateContext.TestRun.TestRunSummary.TotalFailed != mochaStateContext.TestRun.FailedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Failed tests count does not match failed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // TODO: do we want transition logs here?
            this.logger.Info("MochaTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingStackTraces.");
            return MochaTestResultParserState.ExpectingStackTraces;
        }
    }
}
