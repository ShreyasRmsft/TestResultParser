namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using TestResultObject = Agent.Plugins.TestResultParser.TestResult.Models.TestResult;

    public class ResultParser : IResultParser
    {
        private Regex resultPattern;
        private Regex failedResultPattern;
        private Regex passedOutcomePattern;
        private Regex skippedOutcomePattern;
        private Regex expectedFailureOutcomePattern;
        private ITelemetryDataCollector telemetryDataCollector;
        private IDiagnosticDataCollector diagnosticDataCollector;

        public ResultParser() : this(Constants.ResultPattern, Constants.FailedResultPattern, Constants.PassedOutcomePattern,
            Constants.SkippedOutcomePattern, Constants.ExpectedFailureOutcomePattern, TelemetryDataCollector.Instance,
            DiagnosticDataCollector.Instance)
        {
        }

        public ResultParser(Regex resultPattern, Regex failedResultPattern, Regex passedOutcomePattern,
            Regex skippedOutcomePattern, Regex expectedFailureOutcomePattern, ITelemetryDataCollector telemetryDataCollector,
            IDiagnosticDataCollector diagnosticDataCollector)
        {
            this.resultPattern = resultPattern;
            this.failedResultPattern = failedResultPattern;

            this.passedOutcomePattern = passedOutcomePattern;
            this.skippedOutcomePattern = skippedOutcomePattern;
            this.expectedFailureOutcomePattern = expectedFailureOutcomePattern;

            this.telemetryDataCollector = telemetryDataCollector;
            this.diagnosticDataCollector = diagnosticDataCollector;

            this.diagnosticDataCollector.Verbose($"Test result pattern: {this.resultPattern}");
            this.diagnosticDataCollector.Verbose($"Failed result pattern: {this.failedResultPattern}");
            this.diagnosticDataCollector.Verbose($"Passed outcome pattern: {this.passedOutcomePattern}");
            this.diagnosticDataCollector.Verbose($"Skipped result pattern: {this.skippedOutcomePattern}");
            this.diagnosticDataCollector.Verbose($"Expected failure result pattern: {this.expectedFailureOutcomePattern}");
        }

        /// <inheritdoc />
        public TestResultObject Parse(string data, TestResultObject partialTestResult)
        {
            // Validate
            if (data == null) { return null; }

            // Parse
            var partialStartExists = partialTestResult != null;
            TestResultObject result = partialStartExists ? partialTestResult : new TestResultObject();
            var parsed = partialStartExists ?
                ParseForPartialEnd(data, result) :
                ParseForCompleteOrPartialStart(data, result);

            return parsed ? result : null;
        }

        /// <summary>
        /// Parse input data for complete or partial start result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="result">Result object.</param>
        /// <returns>True is parsing successful.</returns>
        private bool ParseForCompleteOrPartialStart(string data, TestResultObject result)
        {
            // Parse
            var resultMatch = this.resultPattern.Match(data);
            if (!resultMatch.Success) {
                // Note: Passed, Skipped, ExpectedFailure are part of test result pattern. But Failed test is not. It has different pattern. Thus checking separately for failed test pattern.
                return this.ParseForFailedResult(data, result);
            }

            // Validate
            if (resultMatch.Groups.Count < Constants.ResultPatternExpectedGroups)
            {
                this.LogUnexpectedGroupCount(Constants.ResultPatternExpectedGroups, resultMatch.Groups.Count, this.resultPattern, data);
                return false;
            }

            // Test result name
            var resultNameIdentifier = resultMatch.Groups[Constants.ResultNameGroupIndex].Value.Trim();
            string resultName = GetResultName(data, resultNameIdentifier);
            if (resultName == null) { return false; }
            result.Name = resultName;

            // Check for outcome if it is passed.
            var testOutcomeIdentifier = resultMatch.Groups[Constants.ResultOutcomeGroupIndex].Value.Trim();
            var parsed = this.ParseForOutcome(this.passedOutcomePattern, TestOutcome.Passed, Constants.PassedOutcomePatternsExpectedGroups, testOutcomeIdentifier, result, data);
            if (parsed) { return true; }

            // Check for outcome if it is expected failrue.
            // Note: Currently we are setting expected failure outcome also as passed.
            parsed = this.ParseForOutcome(this.expectedFailureOutcomePattern, TestOutcome.Passed, Constants.PassedOutcomePatternsExpectedGroups, testOutcomeIdentifier, result, data);
            if (parsed) { return true; }

            // Check for outcome if it is skipped.
            parsed = this.ParseForOutcome(this.skippedOutcomePattern, TestOutcome.Skipped, Constants.SkippedOutcomePatternsExpectedGroups, testOutcomeIdentifier, result, data);

            return true; // Always set to true as we are able to get the result name.
        }

        /// <summary>
        /// Parse input data for parital end result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="result">Result object.</param>
        /// <returns>True is parsing successful.</returns>
        private bool ParseForPartialEnd(string data, TestResultObject result)
        {
            // Note: In partial end, we don't check for skipped and failed result. The reason is skipped and failed occurs in one go and are not partial.

            // Parse for passed test outcome.
            var testOutcomeIdentifier = data;
            var parsed = this.ParseForOutcome(this.passedOutcomePattern, TestOutcome.Passed, Constants.PassedOutcomePatternsExpectedGroups, testOutcomeIdentifier, result, data);
            if (parsed) { return true; }

            // Parse for expected failure test outcome.
            // Note: Currently we are setting expected failure outcome also as passed.
            parsed = this.ParseForOutcome(this.expectedFailureOutcomePattern, TestOutcome.Passed, Constants.PassedOutcomePatternsExpectedGroups, testOutcomeIdentifier, result, data);
            if (parsed) { return true; }

            return false;
        }

        /// <summary>
        /// Parse for failed result.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="result">Result object.</param>
        /// <returns>True if parse successful.</returns>
        private bool ParseForFailedResult(string data, TestResultObject result)
        {
            // Parse
            var failedResultMatch = this.failedResultPattern.Match(data);
            if (!failedResultMatch.Success) { return false; }

            // Validate.
            if (failedResultMatch.Groups.Count < Constants.FailedResultPatternExpectedGroups)
            {
                this.LogUnexpectedGroupCount(Constants.FailedResultPatternExpectedGroups, failedResultMatch.Groups.Count, failedResultPattern, data);
                return false;
            }

            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[Constants.ResultNameGroupIndex].Value.Trim();
            result.Name = GetResultName(data, resultNameIdentifier);

            // Set outcome
            result.Outcome = TestOutcome.Failed;

            return true;
        }

        /// <summary>
        /// Logs unexpected group count.
        /// </summary>
        /// <param name="expectedGroupsCount">Expected groups count.</param>
        /// <param name="actualGroupsCount">Actual groups count.</param>
        /// <param name="pattern">Pattern.</param>
        /// <param name="data">Data.</param>
        private void LogUnexpectedGroupCount(int expectedGroupsCount, int actualGroupsCount, Regex pattern, string data)
        {
            var message = $"Expected groups: {expectedGroupsCount}. Found: {actualGroupsCount} in pattern: {pattern} for data: {data}";
            this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedParsingError, message, aggregate: true);
            this.diagnosticDataCollector.Error(message);
        }
        
        /// <summary>
        /// Parse for outcome.
        /// </summary>
        /// <param name="outcomePattern">Outcome pattern.</param>
        /// <param name="testOutcome">Test outcome.</param>
        /// <param name="expectedGroups">Expected groups.</param>
        /// <param name="testOutcomeIdentifier">Test outcome identifier.</param>
        /// <param name="result">Result.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if parse successful.</returns>
        private bool ParseForOutcome(Regex outcomePattern, TestOutcome testOutcome, int expectedGroups, string testOutcomeIdentifier, TestResultObject result, string data)
        {
            // Parse
            var outcomeMatch = outcomePattern.Match(testOutcomeIdentifier);
            if (!outcomeMatch.Success) { return false; }

            // Validate.
            if (outcomeMatch.Groups.Count < expectedGroups)
            {
                this.LogUnexpectedGroupCount(expectedGroups, outcomeMatch.Groups.Count, outcomePattern, data);
            }

            // Set outcome
            result.Outcome = testOutcome;

            return true;
        }

        /// <summary>
        /// Gets result name.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="testResultNameIdentifier">Test result name identifier.</param>
        /// <returns>Result name.</returns>
        private string GetResultName(string data, string testResultNameIdentifier)
        {
            if (string.IsNullOrWhiteSpace(testResultNameIdentifier))
            {
                this.diagnosticDataCollector.Verbose($"Test result name is null or whitespace in data: {data}");
                return null;
            }

            return testResultNameIdentifier;
        }
    }
}
