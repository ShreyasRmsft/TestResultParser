// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.Loggers;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using TestResult = TestResult.Models.TestResult;

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : ITestResultParser
    {
        private ParserState state;
        private TestResult partialTestResult;
        private TestRun currentTestRun;
        private int currentTestRunId = 1;

        private ITestRunManager runManager;
        private ITelemetryDataCollector telemetryDataCollector;
        private ITraceLogger logger;

        public string Name => "Python";
        public string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        /// <param name="testRunManager"></param>
        public PythonTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {
        }

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunManager">Manager for the test run</param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public PythonTestResultParser(ITestRunManager testRunManager, ITraceLogger traceLogger, ITelemetryDataCollector telemetryCollector)
        {
            traceLogger.Info("PythonTestResultParser : Starting python test result parser.");
            telemetryCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.Initialize, true);

            this.runManager = testRunManager;
            this.telemetryDataCollector = telemetryCollector;
            this.logger = traceLogger;

            this.state = ParserState.ExpectingTestResults;
            this.currentTestRun = new TestRun($"{Name}/{Version}", this.currentTestRunId);
        }

        /// <summary>
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="logData">Data to be parsed.</param>
        public void Parse(LogData logData)
        {
            // Validate data input
            if (!this.IsValidInput(logData.Line) || string.IsNullOrWhiteSpace(logData.Line)) return;

            try
            {
                switch (state)
                {
                    case ParserState.ExpectingSummary:

                        // Summary Test count and total time should have already been parsed
                        // Try to parse test outcome, number of tests for each outcome
                        if (TryParseSummaryOutcome(logData))
                        {
                            PublishAndReset();
                            return;
                        }

                        // Summary was not parsed, reset the parser and try parse again.
                        Reset();
                        Parse(logData);
                        break;

                    case ParserState.ExpectingFailedResults:

                        // Try to parse for failed results and summary
                        // If summary is parsed, change the state
                        if (TryParseForFailedResult(logData)) return;
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            state = ParserState.ExpectingSummary;
                            return;
                        }

                        // Not expected, as Summary has not been encountered yet
                        // If a new TestResult is found, reset the parser and Parse again
                        if (TryParseTestResult(logData))
                        {
                            this.logger.Error("PythonTestResultParser:Parse Expecting failed result or summary but found new test result.");
                            this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.SummaryOrFailedTestsNotFound, new List<int> { this.currentTestRunId }, true);
                            Reset();
                            Parse(logData);
                        }
                        break;

                    case ParserState.ExpectingTestResults:
                    default:
                        if (TryParseTestResult(logData)) return;

                        // Change the state and clear the partial result if failed result or summary is found
                        if (TryParseForFailedResult(logData))
                        {
                            partialTestResult = null;
                            state = ParserState.ExpectingFailedResults;
                            return;
                        }
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            partialTestResult = null;
                            state = ParserState.ExpectingSummary;
                            return;
                        }
                        break;
                }
            }
            catch(Exception ex)
            {
                this.logger.Error($"PythonTestResultParser.Parse : Unable to parse the log line {logData.Line} with exception {ex.ToString()}");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.ParseException, ex.Message);

                Reset();
            }
        }

        /// <summary>
        /// Reset the parser to original state
        /// </summary>
        private void Reset()
        {
            this.logger.Info("PythonTestResultParser.Reset");
            this.partialTestResult = null;
            this.currentTestRun = new TestRun($"{Name}/{Version}", ++this.currentTestRunId);
            this.state = ParserState.ExpectingTestResults;
        }

        /// <summary>
        /// Publish the current test run and reset the parser
        /// </summary>
        private void PublishAndReset()
        {
            this.logger.Info($"PythonTestResultParser:PublishAndReset : Publishing TestRun {currentTestRunId}");
            runManager.Publish(currentTestRun);
            Reset();
        }
        
        private bool TryParseTestResult(LogData logData)
        {
            var resultMatch = PythonRegularExpressions.TestResult.Match(logData.Line);
 
            if (!resultMatch.Success)
            {
                return this.partialTestResult == null ? false : TryParseForPartialResult(logData);
            }

            this.partialTestResult = null;
            
            var testCaseNameIdentifier = resultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();
            string testCaseName = GetResultName(logData, testCaseNameIdentifier);

            if (testCaseName == null) return false;
            
            var result = new TestResult() { Name = testCaseName };
            
            // Determine the outcome of the Test result
            var testOutcomeIdentifier = resultMatch.Groups[RegexCaptureGroups.TestOutcome].Value.Trim();
            var passedResultMatch = PythonRegularExpressions.PassedOutcome.Match(testOutcomeIdentifier);
            if (passedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Passed;
                currentTestRun.PassedTests.Add(result);
                return true;
            }

            var skippedResultMatch = PythonRegularExpressions.SkippedOutcome.Match(testOutcomeIdentifier);
            if (skippedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Skipped;
                currentTestRun.SkippedTests.Add(result);
                return true;
            }
            
            // The outcome for this result could not be determined, adding to partial result
            partialTestResult = result;
            return true;
        }

        private bool TryParseForPartialResult(LogData logData)
        {
            var partialResultMatch = PythonRegularExpressions.PassedOutcome.Match(logData.Line);
            if (partialResultMatch.Success)
            {
                this.partialTestResult.Outcome = TestOutcome.Passed;
                this.currentTestRun.PassedTests.Add(partialTestResult);
                return true;
            }
            return false;
        }

        private bool TryParseForFailedResult(LogData logData)
        {
            // Parse
            var failedResultMatch = PythonRegularExpressions.FailedResult.Match(logData.Line);
            if (!failedResultMatch.Success) { return false; }

            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();

            var result = new TestResult();
            result.Name = GetResultName(logData, resultNameIdentifier);
            result.Outcome = TestOutcome.Failed;

            currentTestRun.FailedTests.Add(result);
            return true;
        }

        private string GetResultName(LogData logData, string testResultNameIdentifier)
        {
            if (string.IsNullOrWhiteSpace(testResultNameIdentifier))
            {
                this.logger.Verbose($"Test result name is null or whitespace in logData: {logData.Line}");
                return null;
            }

            return testResultNameIdentifier;
        }

        private bool TryParseSummaryTestAndTime(LogData logData)
        {
            var countAndTimeSummaryMatch = PythonRegularExpressions.TestCountAndTimeSummary.Match(logData.Line);
            if (countAndTimeSummaryMatch.Success)
            {
                var testcount = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TotalTests].Value);
                var secTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTime].Value);
                var msTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTimeMs].Value);

                currentTestRun.TestRunSummary = new TestRunSummary
                {
                    TotalExecutionTime = new TimeSpan(0, 0, 0, secTime, msTime),
                    TotalTests = testcount
                };
                this.logger.Info("PythonTestResultParser:TryParseSummaryTestAndTime : TestRunSummary with total time and tests created.");
                return true;
            }

            return false;
        }

        private bool TryParseSummaryOutcome(LogData logData)
        {
            if(currentTestRun.TestRunSummary == null)
            {
                // This is safe check, if must be true always because parsers will try to parse for Outcome if Test and Time Summary already parsed.
                this.logger.Error("PythonTestResultParser:TryParseSummaryOutcome : TestRunSummary is null");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.TestRunSummaryCorrupted, new List<int> { this.currentTestRunId }, true);
                return false;
            }

            var resultSummaryMatch = PythonRegularExpressions.TestOutcomeSummary.Match(logData.Line);
            if (resultSummaryMatch.Success)
            {
                var resultIdentifer = resultSummaryMatch.Groups[RegexCaptureGroups.TestOutcome].Value;

                var failureCountPatternMatch = PythonRegularExpressions.SummaryFailure.Match(resultIdentifer);
                if (failureCountPatternMatch.Success)
                {
                    currentTestRun.TestRunSummary.TotalFailed = int.Parse(failureCountPatternMatch.Groups[RegexCaptureGroups.FailedTests].Value);
                }

                // TODO: We should have a separate bucket for errors
                var errorCountPatternMatch = PythonRegularExpressions.SummaryErrors.Match(resultIdentifer);
                if (errorCountPatternMatch.Success)
                {
                    currentTestRun.TestRunSummary.TotalFailed += int.Parse(errorCountPatternMatch.Groups[RegexCaptureGroups.Errors].Value);
                }

                var skippedCountPatternMatch = PythonRegularExpressions.SummarySkipped.Match(resultIdentifer);
                if (skippedCountPatternMatch.Success)
                {
                    currentTestRun.TestRunSummary.TotalSkipped = int.Parse(skippedCountPatternMatch.Groups[RegexCaptureGroups.SkippedTests].Value);
                }

                // Since total passed count is not available, calculate the count based on available statistics.
                currentTestRun.TestRunSummary.TotalPassed = currentTestRun.TestRunSummary.TotalTests - (currentTestRun.TestRunSummary.TotalFailed + currentTestRun.TestRunSummary.TotalSkipped);
                return true;
            }

            this.logger.Error("PythonTestResultParser:TryParseSummaryOutcome : Expected match for SummaryTestOutcome was not found");
            this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.TestOutcomeSummaryNotFound, new List<int> { this.currentTestRunId }, true);
            return false;
        }

        /// <summary>
        /// Validate the input data
        /// </summary>
        /// <param name="data">Log line that was passed to the parser</param>
        /// <returns>True if valid</returns>
        private bool IsValidInput(string data)
        {
            if (data == null)
            {
                this.logger.Error("PythonTestResultParser.IsValidInput : Received null data");
                this.telemetryDataCollector.AddToCumulativeTelemtery(TelemetryConstants.EventArea, TelemetryConstants.InvalidInput, new List<int> { this.currentTestRunId }, true);
            }

            return data != null;
        }
    }
}
