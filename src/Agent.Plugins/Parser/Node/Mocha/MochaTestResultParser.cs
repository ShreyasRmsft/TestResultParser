﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// TODO: Add heavy verbose logging 

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using TestResult = TestResult.Models.TestResult;

    public class MochaTestResultParser : ITestResultParser
    {
        private TestRun testRun = new TestRun { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };

        private MochaTestResultParserStateContext stateContext = new MochaTestResultParserStateContext();

        private MochaTestResultParserState state = MochaTestResultParserState.ParsingTestResults;

        public object ParseTestResultConsoleOut(string testResultsConsoleOut)
        {
            throw new NotImplementedException();
        }

        private IDiagnosticDataCollector diagnosticDataCollector;

        private ITelemetryDataCollector telemetryDataCollector;

        private ITestRunManager testRunManager;

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunManager"></param>
        public MochaTestResultParser(ITestRunManager testRunManager) : this(testRunManager, DiagnosticDataCollector.Instance, TelemetryDataCollector.Instance)
        {

        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public MochaTestResultParser(ITestRunManager testRunManager, IDiagnosticDataCollector diagnosticDataCollector, ITelemetryDataCollector telemetryDataCollector)
        {
            this.testRunManager = testRunManager;
            this.diagnosticDataCollector = diagnosticDataCollector;
            this.telemetryDataCollector = telemetryDataCollector;
        }

        public void StartNewTestResult()
        {
            stateContext = new MochaTestResultParserStateContext();
            testRunManager.Publish(testRun);
            testRun = new TestRun() { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), SkippedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
            state = MochaTestResultParserState.ParsingTestResults;
        }

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            // State model for the mocha parser that defines the regexes to match against in each state
            switch (state)
            {
                case MochaTestResultParserState.ParsingTestResults:

                    if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        return;
                    }

                    break;

                case MochaTestResultParserState.ParsingTestRunSummary:
                    
                    if (MatchPendingSummary(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedSummary(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        return;
                    }

                    break;

                case MochaTestResultParserState.PostSummaryParsing:

                    if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPendingTestCase(testResultsLine.Line))
                    {
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        return;
                    }

                    break;
            }

            if (stateContext.LinesWithinWhichMatchIsExpected == 1)
            {
                StartNewTestResult();
            }
            else if (stateContext.LinesWithinWhichMatchIsExpected > 1)
            {
                stateContext.LinesWithinWhichMatchIsExpected--;
            }
        }

        private bool MatchPassedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Passed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserState.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                testRun.PassedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                if (testCaseNumber != stateContext.LastFailedTestCaseNumber + 1)
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                stateContext.LastFailedTestCaseNumber++;

                if (stateContext.FailedTestsToSkipParsingPostSummary > 0)
                {
                    stateContext.FailedTestsToSkipParsingPostSummary--;
                    if (stateContext.FailedTestsToSkipParsingPostSummary == 0)
                    {
                        // we can also choose to ignore extra failures post summary if the number is not 1
                        stateContext.LastFailedTestCaseNumber = 0;
                        StartNewTestResult();
                    }

                    return true;
                }

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserState.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                testResult.Outcome = TestOutcome.Failed;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                testRun.FailedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchPendingTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Skipped;
                testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserState.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                testRun.SkippedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchPassedSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                stateContext.LinesWithinWhichMatchIsExpected = 1;
                stateContext.ExpectedMatch = "Failed tests summary";

                // Unexpected matches for Passed summary
                switch (state)
                {
                    case MochaTestResultParserState.ParsingTestRunSummary:

                        if (testRun.FailedTests.Count != 0)
                        {
                            diagnosticDataCollector.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }

                        StartNewTestResult();

                        break;

                    case MochaTestResultParserState.PostSummaryParsing:

                        if (stateContext.FailedTestsToSkipParsingPostSummary != 0)
                        {
                            diagnosticDataCollector.Error("TODO");
                            telemetryDataCollector.AddProperty("TODO", "TODO");
                        }
                        StartNewTestResult();

                        break;
                }
                
                state = MochaTestResultParserState.ParsingTestRunSummary;
                stateContext.LastFailedTestCaseNumber = 0;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalPassed = totalPassed;

                // Fire telemetry if summary does not agree with parsed tests count
                if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests.Count)
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                if (!long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
                {
                    case "ms":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                        break;

                    case "s":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                        break;

                    case "m":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                        break;

                    case "h":
                        testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                        break;
                }

                return true;
            }

            return false;
        }

        private bool MatchFailedSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                stateContext.LinesWithinWhichMatchIsExpected = 0;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalFailed = totalFailed;
                stateContext.FailedTestsToSkipParsingPostSummary = totalFailed;

                if (stateContext.FailedTestsToSkipParsingPostSummary == 0)
                {
                    state = MochaTestResultParserState.ParsingTestResults;
                    StartNewTestResult();
                }
                else
                {
                    state = MochaTestResultParserState.PostSummaryParsing;
                }

                if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests.Count)
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                return true;
            }

            return false;
        }

        private bool MatchPendingSummary(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PendingTestsSummaryMatcher.Match(testResultsLine);

            if (match.Success)
            {
                stateContext.LinesWithinWhichMatchIsExpected = 1;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalSkipped = totalPending;

                if (testRun.TestRunSummary.TotalSkipped != testRun.SkippedTests.Count)
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                return true;
            }

            return false;
        }
    }
}
