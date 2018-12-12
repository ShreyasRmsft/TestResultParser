namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;
    
    public class JestExpectingTestRunSummary : JestParserStateBase
    {
        public override IEnumerable<RegexActionPair> RegexsToMatch { get; }

        /// <inheritdoc />
        public JestExpectingTestRunSummary(ParserResetAndAttemptPublish parserResetAndAttemptPublish, ITraceLogger logger, ITelemetryDataCollector telemetryDataCollector)
            : base(parserResetAndAttemptPublish, logger, telemetryDataCollector)
        {
            RegexsToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(JestRegexs.TestsSummaryMatcher, TestsSummaryMatched),
                new RegexActionPair(JestRegexs.TestRunTimeMatcher, TestRunTimeMatched),
                new RegexActionPair(JestRegexs.TestRunStart, TestRunStartMatched)
            };
        }

        private Enum TestsSummaryMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            jestStateContext.LinesWithinWhichMatchIsExpected = 2;
            jestStateContext.NextExpectedMatch = "test run time";

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed);
            int.TryParse(match.Groups[RegexCaptureGroups.SkippedTests].Value, out int totalSkipped);
            int.TryParse(match.Groups[RegexCaptureGroups.TotalTests].Value, out int totalTests);

            jestStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;
            jestStateContext.TestRun.TestRunSummary.TotalFailed = totalFailed;
            jestStateContext.TestRun.TestRunSummary.TotalSkipped = totalSkipped;
            jestStateContext.TestRun.TestRunSummary.TotalTests = totalTests;

            return JestParserStates.ExpectingTestRunSummary;
        }

        private Enum TestRunTimeMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            // Extract the test run time
            // Handling parse errors is unnecessary
            var timeTaken = double.Parse(match.Groups[RegexCaptureGroups.TestRunTime].Value);

            // Store time taken based on the unit used
            switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
            {
                case "ms":
                    jestStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                    break;

                case "s":
                    jestStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                    break;

                case "m":
                    jestStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                    break;

                case "h":
                    jestStateContext.TestRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                    break;
            }

            this.attemptPublishAndResetParser();

            return JestParserStates.ExpectingTestRunStart;
        }

        private Enum TestRunStartMatched(Match match, TestResultParserStateContext stateContext)
        {
            var jestStateContext = stateContext as JestParserStateContext;

            this.logger.Error($"JestTestResultParser : ExpectingTestRunSummary : Transitioned to state ExpectingTestResults" +
                $" at line {jestStateContext.CurrentLineNumber} as test run start indicator was encountered before encountering" +
                $" the full summary.");
            this.telemetryDataCollector.AddToCumulativeTelemetry(JestTelemetryConstants.EventArea,
                JestTelemetryConstants.UnexpectedTestRunStart, new List<int> { jestStateContext.TestRun.TestRunId }, true);

            this.attemptPublishAndResetParser();

            return JestParserStates.ExpectingTestResults;
        }
    }
}
