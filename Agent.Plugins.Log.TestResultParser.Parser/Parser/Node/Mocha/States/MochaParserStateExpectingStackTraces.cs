// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;

    public class MochaParserStateExpectingStackTraces : MochaParserStateBase
    {
        /// <inheritdoc />
        public override IEnumerable<RegexActionPair> RegexesToMatch { get; }

        /// <inheritdoc />
        public MochaParserStateExpectingStackTraces(ParserResetAndAttemptPublish parserResetAndAttempPublish, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, string parserName)
                : base(parserResetAndAttempPublish, logger, telemetryDataCollector, parserName)
        {
            RegexesToMatch = new List<RegexActionPair>
            {
                new RegexActionPair(MochaRegexes.FailedTestCase, FailedTestCaseMatched),
                new RegexActionPair(MochaRegexes.PassedTestCase, PassedTestCaseMatched),
                new RegexActionPair(MochaRegexes.PendingTestCase, PendingTestCaseMatched),
                new RegexActionPair(MochaRegexes.PassedTestsSummary, PassedTestsSummaryMatched)
            };
        }

        private Enum PassedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            // If a passed test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data
            // This check is safety check for when we try to parse stack trace contents, as of now it will always evaluate to true
            if (mochaStateContext.StackTracesToExpectPostSummary != 0)
            {
                Logger.Error($"{ParserName} : {StateName} : Expecting stack traces but found passed test case instead at line {mochaStateContext.CurrentLineNumber}.");
                Telemetry.AddAndAggregate(MochaTelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, MochaTelemetryConstants.EventArea);
            }

            AttemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.Passed, match);
            mochaStateContext.TestRun.PassedTests.Add(testResult);

            Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestResults " +
                $"at line {mochaStateContext.CurrentLineNumber}.");

            return MochaParserStates.ExpectingTestResults;
        }

        private Enum FailedTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            // Handling parse errors is unnecessary
            var testCaseNumber = int.Parse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value);

            // In the event the failed test case number does not match the expected test case number log an error
            if (testCaseNumber != mochaStateContext.LastFailedTestCaseNumber + 1)
            {
                Logger.Error($"{ParserName} : {StateName} : Expecting stack trace with" +
                    $" number {mochaStateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                Telemetry.AddAndAggregate(MochaTelemetryConstants.UnexpectedFailedStackTraceNumber,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, MochaTelemetryConstants.EventArea);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // as a match but do not consider it a valid stack trace
                if (testCaseNumber != 1)
                {
                    // If we are parsing stack traces then we should not return this as
                    // a successful match. If we do so then stack trace addition will not 
                    // happen for the current line
                    return MochaParserStates.ExpectingStackTraces;
                }

                Telemetry.AddAndAggregate(MochaTelemetryConstants.AttemptPublishAndResetParser,
                    new List<string> { $"Expecting stack trace with number {mochaStateContext.LastFailedTestCaseNumber} but found {testCaseNumber} instead" }, MochaTelemetryConstants.EventArea);

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                AttemptPublishAndResetParser();

                mochaStateContext.LastFailedTestCaseNumber++;

                var testResult = PrepareTestResult(TestOutcome.Failed, match);
                mochaStateContext.TestRun.FailedTests.Add(testResult);

                Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestResults " +
                    $"at line {mochaStateContext.CurrentLineNumber}.");

                return MochaParserStates.ExpectingTestResults;
            }

            mochaStateContext.LastFailedTestCaseNumber++;

            // Only add the stack trace if a failed test had been encountered
            if (mochaStateContext.CurrentStackTraceIndex < mochaStateContext.TestRun.FailedTests.Count)
            {
                // Consider matching the name of the test in the stack trace with what was parsed earlier
                // Suite name is also available. Should we use it for reporting?
                mochaStateContext.TestRun.FailedTests[mochaStateContext.CurrentStackTraceIndex].StackTrace = match.Value;
            }

            // Expect the stack trace to not be more than 50 lines long
            // This is to ensure we don't skip publishing the run if the stack traces appear corrupted
            mochaStateContext.LinesWithinWhichMatchIsExpected = 50;

            mochaStateContext.StackTracesToExpectPostSummary--;

            if (mochaStateContext.StackTracesToExpectPostSummary == 0)
            {
                AttemptPublishAndResetParser();
                return MochaParserStates.ExpectingTestResults;
            }

            return MochaParserStates.ExpectingStackTraces;
        }

        private Enum PendingTestCaseMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;

            // If a pending test case is encountered while in the stack traces state it indicates corruption
            // or incomplete stack trace data

            // This check is safety check for when we try to parse stack trace contents
            if (mochaStateContext.StackTracesToExpectPostSummary != 0)
            {
                Logger.Error($"{ParserName} : {StateName} : Expecting stack traces but found pending test case instead.");
                Telemetry.AddAndAggregate(MochaTelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, MochaTelemetryConstants.EventArea);
            }

            AttemptPublishAndResetParser();

            var testResult = PrepareTestResult(TestOutcome.NotExecuted, match);
            mochaStateContext.TestRun.SkippedTests.Add(testResult);

            Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestResults " +
                $"at line {mochaStateContext.CurrentLineNumber}.");

            return MochaParserStates.ExpectingTestResults;
        }

        private Enum PassedTestsSummaryMatched(Match match, AbstractParserStateContext stateContext)
        {
            var mochaStateContext = stateContext as MochaParserStateContext;
            Logger.Info($"{ParserName} : {StateName} : Passed test summary encountered at line {mochaStateContext.CurrentLineNumber}.");

            // If we were expecting more stack traces but got summary instead
            if (mochaStateContext.StackTracesToExpectPostSummary != 0)
            {
                Logger.Error($"{ParserName} : {StateName} : Expecting stack traces but found passed summary instead.");
                Telemetry.AddAndAggregate(MochaTelemetryConstants.SummaryWithNoTestCases,
                    new List<int> { mochaStateContext.TestRun.TestRunId }, MochaTelemetryConstants.EventArea);
            }

            AttemptPublishAndResetParser();

            mochaStateContext.LinesWithinWhichMatchIsExpected = 1;
            mochaStateContext.NextExpectedMatch = "failed/pending tests summary";

            // Handling parse errors is unnecessary
            var totalPassed = int.Parse(match.Groups[RegexCaptureGroups.PassedTests].Value);

            mochaStateContext.TestRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (mochaStateContext.TestRun.TestRunSummary.TotalPassed != mochaStateContext.TestRun.PassedTests.Count)
            {
                Logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {mochaStateContext.CurrentLineNumber}");
                Telemetry.AddAndAggregate( MochaTelemetryConstants.PassedSummaryMismatch, 
                    new List<int> { mochaStateContext.TestRun.TestRunId }, MochaTelemetryConstants.EventArea);
            }

            // Extract the test run time from the passed tests summary
            ExtractTestRunTime(match, mochaStateContext);

            Logger.Info($"{ParserName} : {StateName} : Transitioned to state ExpectingTestRunSummary " +
                $"at line {mochaStateContext.CurrentLineNumber}.");
            return MochaParserStates.ExpectingTestRunSummary;
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

            var mochaStateContext = stateContext as MochaParserStateContext;
            if (mochaStateContext.CurrentStackTraceIndex > -1 && mochaStateContext.CurrentStackTraceIndex < stateContext.TestRun.FailedTests.Count)
            {
                stateContext.TestRun.FailedTests[mochaStateContext.CurrentStackTraceIndex].StackTrace += Environment.NewLine + line;
            }

            return false;
        }
    }
}
