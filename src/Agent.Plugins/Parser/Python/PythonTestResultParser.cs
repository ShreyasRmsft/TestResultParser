namespace Agent.Plugins.TestResultParser.Parser.Python
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

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : ITestResultParser
    {
        private ITestRunManager runManager;
        private ITelemetryDataCollector telemetryDataCollector;
        private IDiagnosticDataCollector diagnosticDataCollector;

        private ParserState state;
        private TestResult partialTestResult;

        private TestRun currentTestRun = new TestRun { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>()};

        public PythonTestResultParser(ITestRunManager testRunManager) : this(testRunManager, TelemetryDataCollector.Instance, DiagnosticDataCollector.Instance)
        {
        }

        internal PythonTestResultParser(ITestRunManager testRunManager, ITelemetryDataCollector telemetryCollector, IDiagnosticDataCollector diagnosticsCollector)
        {
            this.runManager = testRunManager;
            this.telemetryDataCollector = telemetryCollector;
            this.diagnosticDataCollector = diagnosticsCollector;

            this.state = ParserState.ExpectingTestResults;
        }

        /// <summary>
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        public void Parse(LogLineData logLine)
        {
            var data = logLine.Line;

            // Validate data input
            if (!this.IsValidInput(data)) return;

            // Switch to proper parser state
            this.diagnosticDataCollector.Verbose($"Current state: {nameof(this.state)}");

            switch (state)
            {
                case ParserState.ExpectingSummary:
                    if (ParseSummary(data)) return;

                    if (ParseTestResult(data))
                    {
                        Reset();
                        return;
                    }
                    if(ParseForFailedResult(data))
                    {
                        // This is not expected, log and reset
                        diagnosticDataCollector.Error("TODO");
                        state = ParserState.ExpectingFailedResults;
                        return;
                    }
                    break;

                case ParserState.ExpectingFailedResults:
                    if (ParseForFailedResult(data)) return;
                    if (ParseSummary(data))
                    {
                        state = ParserState.ExpectingSummary;
                        return;
                    }
                    if(ParseTestResult(data))
                    {
                        // This is not expected, log and reset
                        diagnosticDataCollector.Error("TODO");
                        Reset();
                        return;
                    }
                    break;
                default:
                    if (ParseTestResult(data)) return;
                    if (ParseForFailedResult(data))
                    {
                        partialTestResult = null;
                        state = ParserState.ExpectingFailedResults;
                        return;
                    }
                    if (ParseSummary(data))
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
            partialTestResult = null;
            currentTestRun = new TestRun { FailedTests = new List<TestResult>(), PassedTests = new List<TestResult>() };
            state = ParserState.ExpectingTestResults;
        }
    
        private bool ParseTestResult(string data)
        {
            var resultMatch = PythonRegularExpressions.ResultPattern.Match(data);

            // This could be a partial result
            if (!resultMatch.Success && partialTestResult != null)
            {
                var partialResultMatch = PythonRegularExpressions.PassedOutcomePattern.Match(data);
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
            string resultName = GetResultName(data, resultNameIdentifier);

            if (resultName == null)
            {
                return false;
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
                     
        private bool ParseForFailedResult(string data)
        {
            // Parse
            var failedResultMatch = PythonRegularExpressions.FailedResultPattern.Match(data);
            if (!failedResultMatch.Success) { return false; }
            
            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[1].Value.Trim();

            var result = new TestResult();
            result.Name = GetResultName(data, resultNameIdentifier);
            result.Outcome = TestOutcome.Failed;

            currentTestRun.FailedTests.Add(result);
            return true;
        }

        private string GetResultName(string data, string testResultNameIdentifier)
        {
            if (string.IsNullOrWhiteSpace(testResultNameIdentifier))
            {
                this.diagnosticDataCollector.Verbose($"Test result name is null or whitespace in data: {data}");
                return null;
            }

            return testResultNameIdentifier;
        }

        private bool ParseSummary(string data)
        {
            var countAndTimeSummaryMatch = PythonRegularExpressions.SummaryTestCountAndTimePattern.Match(data);

            if(countAndTimeSummaryMatch.Success)
            {
                var testcount = int.Parse(countAndTimeSummaryMatch.Groups[1].Value);
                var secTime = int.Parse(countAndTimeSummaryMatch.Groups[2].Value);
                var msTime = int.Parse(countAndTimeSummaryMatch.Groups[4].Value);

                currentTestRun.TestRunSummary = new TestRunSummary();
                currentTestRun.TestRunSummary.TotalExecutionTime = new TimeSpan(0,0,0,secTime, msTime);
                currentTestRun.TestRunSummary.TotalTests = testcount;
                return true;
            }

            var resultSummaryMatch = PythonRegularExpressions.SummaryOutcomePattern.Match(data);
            if(resultSummaryMatch.Success && currentTestRun.TestRunSummary != null)
            {
                var resultIdentifer = resultSummaryMatch.Groups[3].Value;
                var failureCountPatternMatch = PythonRegularExpressions.SummaryFailurePattern.Match(resultIdentifer);
                if(failureCountPatternMatch.Success)
                {
                    currentTestRun.TestRunSummary.TotalFailed = int.Parse(failureCountPatternMatch.Groups[1].Value);
                }

                //Publish the result
                runManager.Publish(currentTestRun);
                state = ParserState.ExpectingTestResults;
                return true;
            }

            return false;
        }

        private bool IsValidInput(string data)
        {
            if (data == null)
            {
                //this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedError, "Received null data", aggregate: true);
                this.diagnosticDataCollector.Error("Received null data"); // TODO: Is it ok to put in error here?
            }

            return data != null;
        }
    }
}
