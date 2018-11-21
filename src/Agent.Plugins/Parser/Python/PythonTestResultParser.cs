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
        /// <param name="testRunManager"></param>
        public PythonTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TraceLogger.Instance, TelemetryDataCollector.Instance)
        {
        }

        /// <summary>
        /// Detailed constructor where specified logger and telemetry data collector are initialized along with test run manager
        /// </summary>
        /// <param name="testRunPublisher"></param>
        /// <param name="diagnosticDataCollector"></param>
        /// <param name="telemetryDataCollector"></param>
        public PythonTestResultParser(ITestRunManager testRunManager, ITraceLogger traceLogger, ITelemetryDataCollector telemetryCollector)
        {
            traceLogger.Info("MochaTestResultParser.MochaTestResultParser : Starting mocha test result parser.");
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
            if (!this.IsValidInput(logData.Line)) return;

            // Switch to proper parser state
            this.logger.Verbose($"Current state: {nameof(this.state)}");

            switch (state)
            {
                case ParserState.ExpectingSummary:
                    if (ParseSummary(logData)) return;

                    if (ParseTestResult(logData)) return;
                    if(ParseForFailedResult(logData))
                    {
                        state = ParserState.ExpectingFailedResults;
                        return;
                    }
                    break;

                case ParserState.ExpectingFailedResults:
                    if (ParseForFailedResult(logData)) return;
                    if (ParseSummary(logData))
                    {
                        state = ParserState.ExpectingSummary;
                        return;
                    }

                    if (ParseTestResult(logData)) return;
                    break;

                default:
                    if (ParseTestResult(logData)) return;
                    if (ParseForFailedResult(logData))
                    {
                        partialTestResult = null;
                        state = ParserState.ExpectingFailedResults;
                        return;
                    }
                    if (ParseSummary(logData))
                    {
                        partialTestResult = null;
                        state = ParserState.ExpectingSummary;
                        return;
                    }
                    break;
            }
        }

        private void Reset()
        {
            this.partialTestResult = null;
            this.currentTestRun = new TestRun($"{Name}/{Version}", this.currentTestRunId);
            this.state = ParserState.ExpectingTestResults;
        }
        
        private bool ParseTestResult(LogData logData)
        {
            var resultMatch = PythonRegularExpressions.ResultPattern.Match(logData.Line);

            // This could be a partial result
            if (!resultMatch.Success && partialTestResult != null)
            {
                var partialResultMatch = PythonRegularExpressions.PassedOutcomePattern.Match(logData.Line);
                if (partialResultMatch.Success)
                {
                    partialTestResult.Outcome = TestOutcome.Passed;
                    currentTestRun.PassedTests.Add(partialTestResult);
                }
                return false;
            }
            else
            {
                partialTestResult = null;
            }

            // Test result name
            var resultNameIdentifier = resultMatch.Groups[1].Value.Trim();
            string resultName = GetResultName(logData, resultNameIdentifier);

            if (resultName == null)
            {
                return false;
            }
            
            if(state != ParserState.ExpectingTestResults)
            {
                Reset();
            }

            var result = new TestResult();
            result.Name = resultName;

            // Check for outcome if it is passed.
            var testOutcomeIdentifier = resultMatch.Groups[2].Value.Trim();
            var passedResultMatch = PythonRegularExpressions.PassedOutcomePattern.Match(testOutcomeIdentifier);

            if (passedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Passed;
                currentTestRun.PassedTests.Add(result);
            }

            // Ignore the other results
            // Possible that a partial result was received
            if (result.Outcome == TestOutcome.None)
            {
                partialTestResult = result;
            }

            return true;
        }
                     
        private bool ParseForFailedResult(LogData logData)
        {
            // Parse
            var failedResultMatch = PythonRegularExpressions.FailedResultPattern.Match(logData.Line);
            if (!failedResultMatch.Success) { return false; }

            if (state == ParserState.ExpectingSummary)
            {
                Reset();
            }

            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[1].Value.Trim();

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

        private bool ParseSummary(LogData logData)
        {
            if (currentTestRun.TestRunSummary == null)
            {
                var countAndTimeSummaryMatch = PythonRegularExpressions.SummaryTestCountAndTimePattern.Match(logData.Line);

                if (countAndTimeSummaryMatch.Success)
                {
                    var testcount = int.Parse(countAndTimeSummaryMatch.Groups[1].Value);
                    var secTime = int.Parse(countAndTimeSummaryMatch.Groups[2].Value);
                    var msTime = int.Parse(countAndTimeSummaryMatch.Groups[4].Value);

                    currentTestRun.TestRunSummary = new TestRunSummary();
                    currentTestRun.TestRunSummary.TotalExecutionTime = new TimeSpan(0, 0, 0, secTime, msTime);
                    currentTestRun.TestRunSummary.TotalTests = testcount;
                    return true;
                }
            }
            else
            {
                var allowedWhiteSpaceLineMatch = PythonRegularExpressions.SummaryAllowedWhiteSpaceLine.Match(logData.Line);
                if (allowedWhiteSpaceLineMatch.Success)
                {
                    return true;
                }

                var resultSummaryMatch = PythonRegularExpressions.SummaryOutcomePattern.Match(logData.Line);
                if (resultSummaryMatch.Success)
                {
                    var resultIdentifer = resultSummaryMatch.Groups[3].Value;
                    var failureCountPatternMatch = PythonRegularExpressions.SummaryFailurePattern.Match(resultIdentifer);
                    if (failureCountPatternMatch.Success)
                    {
                        currentTestRun.TestRunSummary.TotalFailed = int.Parse(failureCountPatternMatch.Groups[RegexCaptureGroups.FailedTests].Value);
                    }

                    // TODO: Probably should have a separate bucket for errors
                    var errorCountPatternMatch = PythonRegularExpressions.SummaryErrorsPattern.Match(resultIdentifer);
                    if(errorCountPatternMatch.Success)
                    {
                        currentTestRun.TestRunSummary.TotalFailed += int.Parse(errorCountPatternMatch.Groups[RegexCaptureGroups.NumberOfErrors].Value);
                    }

                    //Publish the result
                    runManager.Publish(currentTestRun);
                    Reset();
                    state = ParserState.ExpectingTestResults;
                    return true;
                }
            }
            return false;
        }

        private bool IsValidInput(string data)
        {
            if (data == null)
            {
                //this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedError, "Received null data", aggregate: true);
                this.logger.Error("Received null data"); // TODO: Is it ok to put in error here?
            }

            return data != null;
        }
    }
}
