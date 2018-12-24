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
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public JasmineParserStateExpectingTestRunStart(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JasmineRegexes.TestRunStart, TestRunStartMatched),
                new RegexActionPair(JasmineRegexes.TestStatus, TestStatusMatched),
            };
        }

        private Enum TestRunStartMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunStart : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            return JasmineParserStates.ExpectingTestResults;
        }

        private Enum TestStatusMatched(Match match, AbstractParserStateContext stateContext)
        {
            var jasmineStateContext = stateContext as JasmineParserStateContext;

            this.logger.Info($"JasmineTestResultParser : ExpectingTestRunStart : Transitioned to state ExpectingTestResults" +
                $" at line {jasmineStateContext.CurrentLineNumber}.");

            var testStatus = new List<char>(match.ToString());
            jasmineStateContext.PassedTestsToExpect = testStatus.FindAll((char x) => { return x == '.'; }).Count;
            jasmineStateContext.FailedTestsToExpect = testStatus.FindAll((char x) => { return x == 'F'; }).Count;
            jasmineStateContext.SkippedTestsToExpect = testStatus.FindAll((char x) => { return x == '*'; }).Count;

            return JasmineParserStates.ExpectingTestResults;
        }
    }
}
