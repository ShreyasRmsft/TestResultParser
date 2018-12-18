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
                new RegexActionPair(JasmineRegexes.TestRunTimeMatcher, TestRunTimeMatched)
            };
        }

        private Enum TestRunTimeMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            var timeTaken = double.Parse(match.Groups[RegexCaptureGroups.TestRunTime].Value);
            jasmineStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);

            this.attemptPublishAndResetParser();

            return JasmineParserStates.ExpectingTestRunStart;
        }
    }
}
