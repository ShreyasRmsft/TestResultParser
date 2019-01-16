// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JestParserStateExpectingTestResults : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public JestParserStateExpectingTestResults(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parseName)
             : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parseName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JestRegexs.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(JestRegexs.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(JestRegexs.StackTraceStart, StackTraceStartMatched),
                new RegexActionPair(JestRegexs.SummaryStart, SummaryStartMatched),
                new RegexActionPair(JestRegexs.TestRunStart, TestRunStartMatched),
                new RegexActionPair(JestRegexs.FailedTestsSummaryIndicator, FailedTestsSummaryIndicatorMatched)
            };
        }

        private Enum PassedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            var testResult = PrepareTestResult(TestOutcome.Passed, match);
            jestStateContext.TestRun.PassedTests.Add(testResult);

            // Used for telemetry for identifying how many runs are using --verbose option
            jestStateContext.VerboseOptionEnabled = true;

            return JestParserStates.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // Used for telemetry for identifying how many runs are using --verbose option
            jestStateContext.VerboseOptionEnabled = true;

            // TODO: Revisit if we even need to match these, expcept for telemtry no other use
            // No-op as we would like to pick up failed test cases in the stack traces state

            return JestParserStates.ExpectingTestResults;
        }

        private Enum StackTraceStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // Set this by default to -1, if a genuine stack trace was encountered then the actual index will be set.
            jestStateContext.CurrentStackTraceIndex = -1;

            // In non verbose mode console out appears as a failed test case
            // Only difference being it's not colored red
            if (match.Groups[RegexCaptureGroups.TestCaseName].Value == "Console")
            {
                return JestParserStates.ExpectingStackTraces;
            }

            var testResult = PrepareTestResult(TestOutcome.Failed, match);
            jestStateContext.TestRun.FailedTests.Add(testResult);
            jestStateContext.CurrentStackTraceIndex = jestStateContext.TestRun.FailedTests.Count - 1;

            // Expect the stack trace to not be more than 50 lines long
            // This is to ensure we don't skip publishing the run if the stack traces appear corrupted
            jestStateContext.LinesWithinWhichMatchIsExpected = 50;
            jestStateContext.NextExpectedMatch = "next stacktraceStart/testrunStart/testrunSummary";
            jestStateContext.TestRun.FailedTests[jestStateContext.CurrentStackTraceIndex].StackTrace = match.Value;

            this.logger.Info($"JestTestResultParser : ExpectingTestResults : Transitioned to state ExpectingStackTraces" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingStackTraces;
        }

        private Enum SummaryStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.LinesWithinWhichMatchIsExpected = 1;
            jestStateContext.NextExpectedMatch = "tests summary";

            this.logger.Info($"JestTestResultParser : ExpectingTestResults : Transitioned to state ExpectingTestRunSummary" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingTestRunSummary;
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;
            return JestParserStates.ExpectingTestResults;
        }

        private Enum FailedTestsSummaryIndicatorMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;
            jestStateContext.FailedTestsSummaryIndicatorEncountered = true;

            this.logger.Info($"JestTestResultParser : ExpectingTestResults : Transitioned to state ExpectingStackTraces" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingStackTraces;
        }
    }
}
