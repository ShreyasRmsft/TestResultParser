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
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestResults(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parseName)
             : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parseName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.FailedOrPendingTestCase, FailedOrPendingTestCaseMatched),
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

            Logger.Info($"{ParserName} : {StateName} : Resetting the parser, test run start matched unexpectedly, transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // Test Run Start matched after already encountering test run start.
            // Parser should be reset.
            AttemptPublishAndResetParser();

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailedOrPendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // Set this by default to -1, if a genuine stack trace was encountered then the actual index will be set.
            jasmineStateContext.CurrentStackTraceIndex = -1;

            // If it is a failed testcase , FailureStarterMatched is true
            if (jasmineStateContext.FailureStarterMatched)
            {
                if (testCaseNumber != jasmineStateContext.LastFailedTestCaseNumber + 1)
                {
                    // There's a good chance we read some random line as a failed test case hence consider it a
                    // as a match but do not add it to our list of test cases

                    Logger.Error($"{ParserName} : {StateName} : Expecting failed test case with" +
                        $" number {jasmineStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                    Telemetry.AddAndAggregate(JasmineTelemetryConstants.UnexpectedFailedTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);

                    return JasmineParserStates.ExpectingTestResults;
                }

                // Increment
                jasmineStateContext.LastFailedTestCaseNumber++;

                var failedTestResult = PrepareTestResult(TestOutcome.Failed, match);
                jasmineStateContext.TestRun.FailedTests.Add(failedTestResult);

                jasmineStateContext.CurrentStackTraceIndex = jasmineStateContext.TestRun.FailedTests.Count - 1;

                // Expect the stack trace to not be more than 50 lines long
                // This is to ensure we don't skip publishing the run if the stack traces appear corrupted
                jasmineStateContext.LinesWithinWhichMatchIsExpected = 50;
                jasmineStateContext.NextExpectedMatch = "next failed test case or pending test cases start or test run summary";
                jasmineStateContext.TestRun.FailedTests[jasmineStateContext.CurrentStackTraceIndex].StackTrace = match.Value;

                return JasmineParserStates.ExpectingTestResults;
            }

            // If it is a pending testcase , PendingStarterMatched is true
            if (jasmineStateContext.PendingStarterMatched)
            {
                if (testCaseNumber != jasmineStateContext.LastPendingTestCaseNumber + 1)
                {
                    // There's a good chance we read some random line as a pending test case hence consider it a
                    // as a match but do not add it to our list of test cases

                    Logger.Error($"{ParserName} : {StateName} : Expecting pending test case with" +
                        $" number {jasmineStateContext.LastPendingTestCaseNumber + 1} but found {testCaseNumber} instead");
                    Telemetry.AddAndAggregate(JasmineTelemetryConstants.UnexpectedPendingTestCaseNumber,
                        new List<int> { jasmineStateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);

                    return JasmineParserStates.ExpectingTestResults;
                }

                // Increment
                jasmineStateContext.LastPendingTestCaseNumber++;

                var skippedTestResult = PrepareTestResult(TestOutcome.NotExecuted, match);
                jasmineStateContext.TestRun.SkippedTests.Add(skippedTestResult);

                return JasmineParserStates.ExpectingTestResults;
            }

            // If none of the starter has matched, it must be a random line. Fire telemetry and log error
            Logger.Error($"{ParserName} : {StateName} : Expecting failed/pending test case " +
                        $" but encountered test case with {testCaseNumber} without encountering failed/pending starter.");
            Telemetry.AddAndAggregate(JasmineTelemetryConstants.FailedPendingTestCaseWithoutStarterMatch,
                new List<int> { jasmineStateContext.TestRun.TestRunId }, JasmineTelemetryConstants.EventArea);

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

            jasmineStateContext.CurrentStackTraceIndex = jasmineStateContext.TestRun.FailedTests.Count - 1;

            // Expect the stack trace to not be more than 50 lines long
            // This is to ensure we don't skip publishing the run if the stack traces appear corrupted
            jasmineStateContext.LinesWithinWhichMatchIsExpected = 50;
            jasmineStateContext.NextExpectedMatch = "next failed test case or pending test cases start or test run summary";
            jasmineStateContext.TestRun.FailedTests[jasmineStateContext.CurrentStackTraceIndex].StackTrace = match.Value;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum SummaryMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            jasmineStateContext.LinesWithinWhichMatchIsExpected = 1;
            jasmineStateContext.NextExpectedMatch = "test run time";

            Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestRunSummary" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            int.TryParse(match.Groups[RegexCaptureGroups.TotalTests].Value, out int totalTests);
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int failedTests);
            int.TryParse(match.Groups[RegexCaptureGroups.SkippedTests].Value, out int skippedTests);

            // Since suite errors are added as failures in the summary, we need to remove this from passedTests
            // calculation.
            var passedTests = totalTests - skippedTests - (failedTests - jasmineStateContext.SuiteErrors);

            jasmineStateContext.TestRun.TestRunSummary.TotalTests = totalTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalFailed = failedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalSkipped = skippedTests;
            jasmineStateContext.TestRun.TestRunSummary.TotalPassed = passedTests;

            return JasmineParserStates.ExpectingTestRunSummary;
        }

        /// <summary>
        /// If none of the patterns matched then considers adding the current line to stack trace
        /// based on whether a stack trace start has been encountered
        /// </summary>
        /// <param name="line">Current line</param>
        /// <param name="stateContext">State context object containing information of the parser's state</param>
        /// <returns>True if the parser was reset</returns>
        public override bool PeformNoPatternMatchedAction(string line, AbstractParserStateContext stateContext)
        {
            if (base.PeformNoPatternMatchedAction(line, stateContext))
            {
                return true;
            }

            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // Index out of range can never occur as the stack traces immediately follow the failed test case
            if (jasmineStateContext.CurrentStackTraceIndex > -1)
            {
                stateContext.TestRun.FailedTests[jasmineStateContext.CurrentStackTraceIndex].StackTrace += Environment.NewLine + line;
            }

            return false;
        }
    }
}
