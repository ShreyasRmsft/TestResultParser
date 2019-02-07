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
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parseName)
             : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parseName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.TestRunTimeMatcher, TestRunTimeMatched),
                new RegexActionPair(JasmineRegexes.TestRunStart, TestRunStartMatched),
            };
        }

        private Enum TestRunTimeMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            var timeTaken = double.Parse(match.Groups[RegexCaptureGroups.TestRunTime].Value);
            jasmineStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
            jasmineStateContext.IsTimeParsed = true;

            Logger.Info($"{ParserName} : {StateName} : Test run time matched, transitioned to state ExpectingTestRunStart" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            AttemptPublishAndResetParser();

            return JasmineParserStates.ExpectingTestRunStart;
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            Logger.Info($"{ParserName} : {StateName} : Resetting the parser, test run start matched unexpectedly, transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // If a test run started is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            AttemptPublishAndResetParser();

            return JasmineParserStates.ExpectingTestResults;
        }
    }
}
