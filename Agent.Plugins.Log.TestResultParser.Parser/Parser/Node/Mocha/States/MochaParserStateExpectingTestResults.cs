﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class MochaParserStateExpectingTestResults : MochaParserStateBase
    {
        /// <inheritdoc />
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public MochaParserStateExpectingTestResults(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parserName)
                : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parserName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaRegexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };
        }

        private Enum PassedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            var testResult = PrepareTestResult(TestOutcome.Passed, match);
            mochaStateContext.TestRun.PassedTests.Add(testResult);

            return MochaParserStates.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            // Handling parse errors is unnecessary
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // In the event the failed test case number does not match the expected test case number log an error
            if (testCaseNumber != mochaStateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"{this.parserName} : {this.stateName} : Expecting failed test case with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea, MochaTelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not add it to our list of test cases
                if (testCaseNumber != 1)
                {
                    return MochaParserStates.ExpectingTestResults;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                // This is something we might choose to change if we realize there is a chance we can get such false detections often in the middle of a run
                this.attemptPublishAndResetParser();
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            mochaStateContext.LastFailedTestCaseNumber++;

            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            mochaStateContext.TestRun.FailedTests.Add(testResult);

            return MochaParserStates.ExpectingTestResults;
        }

        private Enum PendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            var testResult = PrepareTestResult(TestOutcome.NotExecuted, match);
            mochaStateContext.TestRun.SkippedTests.Add(testResult);

            return MochaParserStates.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;
            this.logger.Info($"{this.parserName} : {this.stateName} : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.NextExpectedMatch = "failed/pending tests summary";
            mochaStateContext.LastFailedTestCaseNumber = 0;

            // Handling parse errors is unnecessary
            var totalPassed = int.Parse(match.Groups[RegexCaptureGroups.PassedTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                this.logger.Error($"{this.parserName} : {this.stateName} : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                this.telemetryDataCollector.AddToCumulativeTelemetry(MochaTelemetryConstants.EventArea,
                    MochaTelemetryConstants.PassedSummaryMismatch, new List<int> { mochaStateContext.TestRun.TestRunId }, true);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            this.logger.Info($"{this.parserName} : {this.stateName} : Transitioned to state ExpectingTestRunSummary" +
                $" at line {mochaStateContext.CurrentLineNumber}.");
            return MochaParserStates.ExpectingTestRunSummary;
        }
    }
}
