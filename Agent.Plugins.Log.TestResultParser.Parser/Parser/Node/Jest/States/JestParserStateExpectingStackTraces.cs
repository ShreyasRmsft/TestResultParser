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
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public JestParserStateExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
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
    }
}
