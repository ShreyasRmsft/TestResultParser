// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JestParserStateExpectingStackTraces : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public JestParserStateExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parseName)
             : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parseName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JestRegexs.StackTraceStart, StackTraceStartMatched),
                new RegexActionPair(JestRegexs.SummaryStart, SummaryStartMatched),
                new RegexActionPair(JestRegexs.TestRunStart, TestRunStartMatched),
                new RegexActionPair(JestRegexs.FailedTestsSummaryIndicator, FailedTestsSummaryIndicatorMatched)
            };
        }

        private Enum StackTraceStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // Set this by default to -1, if a genuine stack trace was encountered then the actual index will be set.
            jestStateContext.CurrentStackTraceIndex = -1;

            if (jestStateContext.FailedTestsSummaryIndicatorEncountered)
            {
                this.logger.Verbose($"JestTestResultParser : ExpectingStackTraces: Ignoring StackTrace/Failed test case at line " +
                    $"{stateContext.CurrentLineNumber} as it is part of the summarized failures.");
                return JestParserStates.ExpectingStackTraces;
            }

            // In non verbose mode console out appears as a failed test case
            // Only difference being it's not colored red
            // Also this generally is the first "stack trace" hence this code is ideally
            // not likely to be hit but keeping it here as safety check
            if (match.Groups[RegexCaptureGroups.TestCaseName].Value == "Console")
            {
                this.logger.Verbose($"JestTestResultParser : ExpectingStackTraces: Ignoring apparent StackTrace/Failed test case at line " +
                    $"{stateContext.CurrentLineNumber} as Jest prints console out in this format in non verbose mode.");
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

            return JestParserStates.ExpectingStackTraces;
        }

        private Enum SummaryStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.LinesWithinWhichMatchIsExpected = 1;
            jestStateContext.NextExpectedMatch = "tests summary";

            this.logger.Info($"JestTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestRunSummary" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingTestRunSummary;
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // If a test run start indicator is encountered after failedTestsSummaryInidicator has
            // been encountered it must be ignored
            if (jestStateContext.FailedTestsSummaryIndicatorEncountered)
            {
                return JestParserStates.ExpectingStackTraces;
            }

            this.logger.Info($"JestTestResultParser : ExpectingStackTraces : Transitioned to state ExpectingTestResults" +
                $" at line {jestStateContext.CurrentLineNumber}.");

            return JestParserStates.ExpectingTestResults;
        }

        private Enum FailedTestsSummaryIndicatorMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.FailedTestsSummaryIndicatorEncountered = true;
            this.logger.Info($"JestTestResultParser : ExpectingStackTraces : ");

            return JestParserStates.ExpectingStackTraces;
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

            var jestStateContext = stateContext as JestParserStateContext;

            // Index out of range can never occur as the stack traces immediately follow the failed test case
            if (jestStateContext.CurrentStackTraceIndex > -1)
            {
                stateContext.TestRun.FailedTests[jestStateContext.CurrentStackTraceIndex].StackTrace += Environment.NewLine + line;
            }

            return false;
        }
    }
}
