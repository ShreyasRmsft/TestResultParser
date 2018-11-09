// Copyright (c) Microsoft Corporation. All rights reserved.
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
        private TestRun testRun = new TestRun { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };

        private MochaTestResultParserStateContext stateContext = new MochaTestResultParserStateContext();

        private MochaTestResultParserStateModel state = MochaTestResultParserStateModel.ParsingTestResults;

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

            testRun = new TestRun() { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>(), TestRunSummary = new TestRunSummary() };
        }

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            if (stateContext.LinesWithinWhichMatchIsExpected == 0)
            {
                StartNewTestResult();
            }
            else if(stateContext.LinesWithinWhichMatchIsExpected > 0)
            {
                stateContext.LinesWithinWhichMatchIsExpected--;
            }

            switch (state)
            {
                case MochaTestResultParserStateModel.ParsingTestResults:

                    if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassed");
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedUnicode");
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedSummary");
                        return;
                    }

                    break;
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
                if (state == MochaTestResultParserStateModel.ParsingTestRunSummary)
                {
                    StartNewTestResult();
                }

                testRun.PassedTests.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();
                stateContext.LinesWithinWhichMatchIsExpected = 2;
                stateContext.ExpectedMatch = "Failed tests summary";

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
                    }

                    return true;
                }

                // Also since this is an action performed in context of a state should there be a separate function?
                // Should this intelligence come from the caller?
                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (state == MochaTestResultParserStateModel.ParsingTestRunSummary)
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

        private bool MatchPassedSummary(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestsSummaryMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                // Unexpected matches
                if (state == MochaTestResultParserStateModel.ParsingTestRunSummary)
                {
                    if (testRun.FailedTests.Count != 0)
                    {
                        diagnosticDataCollector.Error("TODO");
                        telemetryDataCollector.AddProperty("TODO", "TODO");
                    }

                    StartNewTestResult();
                }
                else if (state == MochaTestResultParserStateModel.PostSummaryParsing)
                {
                    if (stateContext.FailedTestsToSkipParsingPostSummary != 0)
                    {
                        diagnosticDataCollector.Error("TODO");
                        telemetryDataCollector.AddProperty("TODO", "TODO");
                    }
                    StartNewTestResult();
                }

                state = MochaTestResultParserStateModel.ParsingTestRunSummary;
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

        private bool MatchFailedSummary(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestsSummaryMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                stateContext.LinesWithinWhichMatchIsExpected = -1;

                if (!int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed))
                {
                    diagnosticDataCollector.Error("TODO");
                    telemetryDataCollector.AddProperty("TODO", "TODO");
                }

                testRun.TestRunSummary.TotalFailed = totalFailed;
                stateContext.FailedTestsToSkipParsingPostSummary = totalFailed;

                if (stateContext.FailedTestsToSkipParsingPostSummary == 0)
                {
                    state = MochaTestResultParserStateModel.ParsingTestResults;
                    StartNewTestResult();
                }
                else
                {
                    state = MochaTestResultParserStateModel.PostSummaryParsing;
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
    }
}
