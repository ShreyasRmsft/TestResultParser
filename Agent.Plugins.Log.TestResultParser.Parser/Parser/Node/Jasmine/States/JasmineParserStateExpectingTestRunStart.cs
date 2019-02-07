// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class JasmineParserStateExpectingTestRunStart : JasmineParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestRunStart(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parseName)
             : base(parserResetAndAttemptPublish, logger, telemetryDataCollector, parseName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.TestRunStart, TestRunStartMatched),
            };
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            // Console logs are dumped after test run start, if the summary or failed/pending test do not appear withing
            // 500 line we wish to reset the parser to not incur unnecessary costs
            jasmineStateContext.LinesWithinWhichMatchIsExpected = 500;
            jasmineStateContext.NextExpectedMatch = "failed/pending tests or test run summary";

            return JasmineParserStates.ExpectingTestResults;
        }
    }
}
