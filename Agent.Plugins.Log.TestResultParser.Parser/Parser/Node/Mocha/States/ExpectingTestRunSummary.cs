﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Agent.Plugins.Log.TestResultParser.Contracts;
using TelemetryConstants = Agent.Plugins.Log.TestResultParser.Parser.NodeTelemetryConstants;

namespace Agent.Plugins.Log.TestResultParser.Parser
{

    public class ExpectingTestRunSummary : MochaParserStateBase
    {
        /// <inheritdoc />
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public ExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestsSummary, PendingTestsSummaryMatched),
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestsSummary, FailedTestsSummaryMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaTestResultParserRegexes.PassedTestsSummary, PassedTestsSummaryMatched),
            };
        }

        private Enum PassedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a passed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless.
            this.attemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.Passed, match);

            mochaStateContext.TestRun.PassedTests.Add(testResult);
            return MochaTestResultParserStates.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a failed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            // Handling parse errors is unnecessary
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
            // as a match but do not add it to our list of test cases
            if (testCaseNumber != 1)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Expecting failed test case with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                return MochaTestResultParserStates.ExpectingTestResults;
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            mochaStateContext.LastFailedTestCaseNumber++;

            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            mochaStateContext.TestRun.FailedTests.Add(testResult);

            return MochaTestResultParserStates.ExpectingTestResults;
        }

        private Enum PendingTestCaseMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            // If a pending test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless.
            this.attemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.NotExecuted, match);

            mochaStateContext.TestRun.SkippedTests.Add(testResult);
            return MochaTestResultParserStates.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            // Passed tests summary is not expected soon after encountering passed tests summary, atleast one test case should have been there.
            this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Was expecting atleast one test case before encountering" +
                $" summary again at line {mochaStateContext.CurrentLineNumber}");
            this.telemetryDataCollector.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                new List<int> { mochaStateContext.TestRun.TestRunId }, true);

            // Reset the parser and start over
            this.attemptPublishAndResetParser();

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed/pending tests summary";

            // Handling parse errors is unnecessary
            var totalPassed = int.Parse(match.Groups[RegexCaptureGroups.PassedTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"MochaTestResultParser : ExpectingTestRunSummary : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            this.logger.Info("MochaTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestRunSummary.");
            return MochaTestResultParserStates.ExpectingTestRunSummary;
        }

        private Enum PendingTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Pending tests summary encountered at line {mochaStateContext.CurrentLineNumber}.");
            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.ExpectedMatch = "failed tests summary";

            // Handling parse errors is unnecessary
            var totalPending = int.Parse(match.Groups[RegexCaptureGroups.PendingTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalSkipped = totalPending;

            return MochaTestResultParserStates.ExpectingTestRunSummary;
        }

        private Enum FailedTestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaTestResultParserStateContext;

            this.logger.Info($"MochaTestResultParser : ExpectingTestRunSummary : Failed tests summary encountered at line {mochaStateContext.CurrentLineNumber}.");
            mochaStateContext.LinesWithinWhichMatchIsExpected = 0;

            // Handling parse errors is unnecessary
            var totalFailed = int.Parse(match.Groups[RegexCaptureGroups.FailedTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalFailed = totalFailed;
            mochaStateContext.StackTracesToSkipParsingPostSummary = totalFailed;

            // Do we want transition logs here?
            this.logger.Info("MochaTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingStackTraces.");
            return MochaTestResultParserStates.ExpectingStackTraces;
        }
    }
}
