// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineParserStateExpectingTestRunSummary : JasmineParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.TestRunTimeMatcher, TestRunTimeMatched),
                new RegexActionPair(JasmineRegexes.FailedOrPendingTestCase, FailedOrPendingTestCaseMatched),
                new RegexActionPair(JasmineRegexes.FailuresStart, FailuresStartMatched),
                new RegexActionPair(JasmineRegexes.PendingStart, PendingStartMatched),
                new RegexActionPair(JasmineRegexes.TestRunStart, TestRunStartMatched),
            };
        }

        private Enum TestRunTimeMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunStart : Transitioned to state ExpectingTestRunStart" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            if (jasmineStateContext.LinesWithinWhichMatchIsExpected == 1)
            {
                var timeTaken = double.Parse(match.Groups[RegexCaptureGroups.TestRunTime].Value);
                jasmineStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);

                this.attemptPublishAndResetParser();
            }

            return JasmineParserStates.ExpectingTestRunStart;
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // If a test run started is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailuresStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All failures are reported after FailureStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // If a failures starter is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            jasmineStateContext.PendingStarterMatched = false;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum PendingStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            // All pending are reported after PendingStart regex is matched.
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            // If a pending starter is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // We set this as true so that any failedOrpending regex match after pending starter matched will be reported as oending tests
            // as pending and failed have the same regex
            jasmineStateContext.PendingStarterMatched = true;

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum FailedOrPendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            // If a failed or pending test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            this.attemptPublishAndResetParser();

            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // Since the parser is reset, the matched test case will be a failed test case or garbled value
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            if (testCaseNumber != jasmineStateContext.LastFailedTestCaseNumber + 1)
            {
                this.logger.Error($"JasmineTestResultParser : ExpectingTestRunSummary : Expecting failed test case with" +
                    $" number {jasmineStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                this.telemetryDataCollector.AddToCumulativeTelemetry(JasmineTelemetryConstants.EventArea, JasmineTelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { jasmineStateContext.TestRun.TestRunId }, true);

                return JasmineParserStates.ExpectingTestResults;
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            jasmineStateContext.LastFailedTestCaseNumber++;

            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            jasmineStateContext.TestRun.FailedTests.Add(testResult);

            return JasmineParserStates.ExpectingTestResults;
        }

    }
}
