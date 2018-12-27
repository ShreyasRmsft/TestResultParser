// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineParserStateExpectingTestResults : JasmineParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestResults(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.FailedOrPendingTestCase, FailedOrPendingTestCaseMatched),
                new RegexActionPair(JasmineRegexes.TestStatus, TestStatusMatched),
                new RegexActionPair(JasmineRegexes.FailuresStart, FailuresStartMatched),
                new RegexActionPair(JasmineRegexes.PendingStart, PendingStartMatched),
                new RegexActionPair(JasmineRegexes.TestsSummaryMatcher, SummaryMatched),
                new RegexActionPair(JasmineRegexes.TestRunStart, TestRunStartMatched),
                new RegexActionPair(JasmineRegexes.SuiteError, SuiteErrorMatched)
            };
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // Test Run Start matched after already encountering test run start.
            // Parser should be reset.

            this.attemptPublishAndResetParser();

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum TestStatusMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;
            jasmineStateContext.LinesWithinWhichMatchIsExpected = 0;

            var testStatus = match.ToString();
            jasmineStateContext.PassedTestsToExpect = Regex.Matches(testStatus, "[.]").Count;
            jasmineStateContext.FailedTestsToExpect = Regex.Matches(testStatus, "[F]").Count;
            jasmineStateContext.SkippedTestsToExpect = Regex.Matches(testStatus, "[*]").Count;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailedOrPendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // If it is a failed testcase , FailureStarterMatched is true
            if (jasmineStateContext.FailureStarterMatched)
            {
                if (testCaseNumber != jasmineStateContext.LastFailedTestCaseNumber + 1)
                {
                    // There's a good chance we read some random line as a failed test case hence consider it a
                    // as a match but do not add it to our list of test cases

                    this.logger.Error($"JasmineTestResultParser : ExpectingTestResults : Expecting failed test case with" +
                        $" number {jasmineStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                    this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.UnexpectedFailedTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

                    return JasmineParserStates.ExpectingTestResults;
                }

                // Increment
                jasmineStateContext.LastFailedTestCaseNumber++;

                var failedTestResult = PrepareTestResult(TestOutcome.Failed, match);
                jasmineStateContext.TestRun.FailedTests.Add(failedTestResult);

                return JasmineParserStates.ExpectingTestResults;
            }

            // If it is a pending testcase , PendingStarterMatched is true
            if (jasmineStateContext.PendingStarterMatched)
            {
                if (testCaseNumber != jasmineStateContext.LastPendingTestCaseNumber + 1)
                {
                    // There's a good chance we read some random line as a pending test case hence consider it a
                    // as a match but do not add it to our list of test cases

                    this.logger.Error($"JasmineTestResultParser : ExpectingTestResults : Expecting pending test case with" +
                        $" number {jasmineStateContext.LastPendingTestCaseNumber + 1} but found {testCaseNumber} instead");
                    this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.UnexpectedPendingTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

                    return JasmineParserStates.ExpectingTestResults;
                }

                // Increment
                jasmineStateContext.LastPendingTestCaseNumber++;

                var skippedTestResult = PrepareTestResult(TestOutcome.NotExecuted, match);
                jasmineStateContext.TestRun.SkippedTests.Add(skippedTestResult);

                return JasmineParserStates.ExpectingTestResults;
            }

            // If none of the starter has matched, it must be a random line. Fire telemetry and log error
            this.logger.Error($"JasmineTestResultParser : ExpectingTestResults : Expecting failed/pending test case " +
                        $" but encountered test case with {testCaseNumber} without encountering failed/pending starter.");
            this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.FailedPendingTestCaseWithoutStarterMatch,
                new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailuresStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All failures are reported after FailureStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            jasmineStateContext.FailureStarterMatched = true;
            jasmineStateContext.PendingStarterMatched = false;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum PendingStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All pending are reported after PendingStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // We set this as true so that any failedOrpending regex match after pending starter matched will be reported as pending tests
            // as pending and failed have the same regex
            jasmineStateContext.PendingStarterMatched = true;
            jasmineStateContext.FailureStarterMatched = false;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum SuiteErrorMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // Suite error is counted as failed and summary includes this while reporting
            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            jasmineStateContext.TestRun.FailedTests.Add(testResult);
            jasmineStateContext.SuiteErrors++;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum SummaryMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            jasmineStateContext.LinesWithinWhichMatchIsExpected = 1;
            jasmineStateContext.NextExpectedMatch = "test run time";

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestRunSummary" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            int totalTests, failedTests, skippedTests;
            int.TryParse(match.Groups[RegexCaptureGroups.TotalTests].Value, out totalTests);
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out failedTests);
            int.TryParse(match.Groups[RegexCaptureGroups.SkippedTests].Value, out skippedTests);

            // Since suite errors are added as failures in the summary, we need to remove this from passedTests
            // calculation.
            var passedTests = totalTests - skippedTests - (failedTests - jasmineStateContext.SuiteErrors);

            jasmineStateContext.TestRun.TestRunSummary.TotalTests = totalTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalFailed = failedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalSkipped = skippedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalPassed = passedTests;

            return JasmineParserStates.ExpectingTestRunSummary;
        }
    }
}
