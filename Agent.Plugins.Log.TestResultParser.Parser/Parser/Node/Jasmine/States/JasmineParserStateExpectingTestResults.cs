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

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            var passesTestsToExpect = match.ToString();
            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum TestStatusMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            var testStatus = new List<char>(match.ToString());
            jasmineStateContext.passedTestsToExpect = testStatus.FindAll((char x) => { return x == '.'; }).Count;
            jasmineStateContext.failedTestsToExpect = testStatus.FindAll((char x) => { return x == 'F'; }).Count;
            jasmineStateContext.skippedTestsToExpect = testStatus.FindAll((char x) => { return x == '*'; }).Count;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailedOrPendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // If it is a failed testcase , pendingStarterMatched is false
            if (!jasmineStateContext.pendingStarterMatched)
            {
                if (testCaseNumber != jasmineStateContext.LastFailedTestCaseNumber + 1)
                {
                    this.logger.Error($"JasmineTestResultParser : ExpectingTestResults : Expecting failed test case with" +
                        $" number {jasmineStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                    this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.UnexpectedFailedTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

                    // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                    // as a match but do not add it to our list of test cases
                    if (testCaseNumber != 1)
                    {
                        return JasmineParserStates.ExpectingTestResults;
                    }

                    // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                    // This is something we might choose to change if we realize there is a chance we can get such false detections often in the middle of a run
                    this.attemptPublishAndResetParser();
                }

                // Increment either ways whether it was expected or context was reset and the encountered number was 1
                jasmineStateContext.LastFailedTestCaseNumber++;

                var testResult = PrepareTestResult(TestOutcome.Failed, match);
                jasmineStateContext.TestRun.FailedTests.Add(testResult);
            }
            else
            {
                if (testCaseNumber != jasmineStateContext.LastPendingTestCaseNumber + 1)
                {
                    this.logger.Error($"JasmineTestResultParser : ExpectingTestResults : Expecting failed test case with" +
                        $" number {jasmineStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                    this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.UnexpectedPendingTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

                    // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                    // as a match but do not add it to our list of test cases
                    if (testCaseNumber != 1)
                    {
                        return JasmineParserStates.ExpectingTestResults;
                    }

                    // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                    // This is something we might choose to change if we realize there is a chance we can get such false detections often in the middle of a run
                    this.attemptPublishAndResetParser();
                }

                // Increment either ways whether it was expected or context was reset and the encountered number was 1
                jasmineStateContext.LastPendingTestCaseNumber++;

                var testResult = PrepareTestResult(TestOutcome.NotExecuted, match);
                jasmineStateContext.TestRun.SkippedTests.Add(testResult);
            }

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailuresStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All failures are reported after FailureStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            jasmineStateContext.pendingStarterMatched = false;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum PendingStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All pending are reported after PendingStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // We set this as true so that any failedOrpending regex match after pending starter matched will be reported as oending tests
            // as pending and failed have the same regex
            jasmineStateContext.pendingStarterMatched = true;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum SuiteErrorMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // Suite error is counted as failed and summary includes this while reporting
            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            jasmineStateContext.TestRun.FailedTests.Add(testResult);
            //jasmineStateContext.failedTestsToExpect++;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum SummaryMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            int totalTests, failedTests, skippedTests;
            int.TryParse(match.Groups[RegexCaptureGroups.TotalTests].Value, out totalTests);
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out failedTests);
            int.TryParse(match.Groups[RegexCaptureGroups.SkippedTests].Value, out skippedTests);

            // Refer to testCase004. Here suite error is counted as a failure.
            // So total passed tests will be counted as 926-14-5 = 907
            // Actual total passed is 926-13-5 = 908
            // jasmineStateContext.failedTestsToExpect is reporting correct. That is 13 failed tests.
            // What to publish in this case.

            jasmineStateContext.TestRun.TestRunSummary.TotalTests = totalTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalFailed = failedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalSkipped = skippedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalPassed = totalTests - failedTests - skippedTests;

            return JasmineParserStates.ExpectingTestRunSummary;
        }

    }
}
