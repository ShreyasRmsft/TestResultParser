namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using TestResultObject = Agent.Plugins.TestResultParser.TestResult.Models.TestResult;

    public class ResultParser : IResultParser
    {
        private Regex resultPattern;
        private Regex passedResultPattern;
        private Regex skippedResultPattern;
        private Regex expectedFailureResultPattern;
        private Regex failedResultPattern;
        private ITelemetryDataCollector telemetryDataCollector;
        private IDiagnosticDataCollector diagnosticDataCollector;

        public ResultParser() : this(Constants.ResultPattern, Constants.PassedResultPattern,
            Constants.SkippedResultPattern, Constants.ExpectedFailureResultPattern,
            Constants.FailedResultPattern, TelemetryDataCollector.Instance,
            DiagnosticDataCollector.Instance)
        {
        }

        public ResultParser(Regex resultPattern, Regex passedResultPattern,
            Regex skippedResultPattern, Regex expectedFailureResultPattern,
            Regex failedResultPattern, ITelemetryDataCollector telemetryDataCollector,
            IDiagnosticDataCollector diagnosticDataCollector)
        {
            this.resultPattern = resultPattern;
            this.passedResultPattern = passedResultPattern;
            this.skippedResultPattern = skippedResultPattern;
            this.expectedFailureResultPattern = expectedFailureResultPattern;
            this.failedResultPattern = failedResultPattern;
            this.telemetryDataCollector = telemetryDataCollector;
            this.diagnosticDataCollector = diagnosticDataCollector;

            this.diagnosticDataCollector.Verbose($"Test result pattern: {resultPattern}");
            this.diagnosticDataCollector.Verbose($"Passed result pattern: {passedResultPattern}");
            this.diagnosticDataCollector.Verbose($"Failed result pattern: {failedResultPattern}");
            this.diagnosticDataCollector.Verbose($"Skipped result pattern: {skippedResultPattern}");
            this.diagnosticDataCollector.Verbose($"Expected failure result pattern: {expectedFailureResultPattern}");
        }

        /// <inheritdoc />
        public TestResultObject Parse(string data, TestResultObject partialTestResult)
        {
            // Validate
            if (data == null)
            {
                return null;
            }

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
            if (!resultMatch.Success) { return false; }

            // Validate
            if (resultMatch.Groups.Count < Constants.ResultPatternExpectedGroups)
            {
                var message = $"Expected groups: {Constants.ResultPatternExpectedGroups}. Found: {resultMatch.Groups.Count} in pattern: {Constants.ResultPattern} for data: {data}";
                this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedParsingError, message, aggregate: true);
                this.diagnosticDataCollector.Error(message); // TODO: Is it ok to put in error here?
                return false;
            }

            // Set partial starting result data
            var partialStartIdentifier = resultMatch.Groups[Constants.PartialStartGroupIndex].Value.Trim();
            var parsed = this.SetPartialStartingResultData(partialStartIdentifier, result, data);

            // Set partial ending result data
            // Note: Parsed flag is returned based on partial starting result only. (Partial ending result parsing success or failure doesn't matter).
            var partialEndIdentifier = resultMatch.Groups[Constants.PartialEndGroupIndex].Value.Trim();
            this.SetPartialEndingResultData(partialEndIdentifier, result, data, false);

            return parsed;
        }

        /// <summary>
        /// Parse input data for parital end result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="result">Result object.</param>
        /// <returns>True is parsing successful.</returns>
        private bool ParseForPartialEnd(string data, TestResultObject result)
        {
            return this.SetPartialEndingResultData(data, result, data, true);
        }

        /// <summary>
        /// Check for skipped result match.
        /// </summary>
        /// <param name="skippedResultPattern">Skipped result pattern.</param>
        /// <param name="outcome">Test outcome.</param>
        /// <param name="partialEndIdentifier">Partial end identifier.</param>
        /// <param name="result">Result.</param>
        /// <param name="data">Data.</param>
        /// <returns></returns>
        private bool CheckForSkippedResultMatch(Regex skippedResultPattern, TestOutcome outcome, string partialEndIdentifier, TestResultObject result, string data)
        {
            // Parse
            var skippedResultMatch = this.resultPattern.Match(partialEndIdentifier);
            if (!skippedResultMatch.Success) { return false; }

            // Set outcome
            result.Outcome = outcome;

            return true;
        }

        /// <summary>
        /// Check for outcome result match.
        /// </summary>
        /// <param name="resultPattern">Result pattrn.</param>
        /// <param name="outcome">Outcome</param>
        /// <param name="partialEndIdentifier">Partial end indentifier.</param>
        /// <param name="result">Result.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if result matches.</returns>
        private bool CheckForOutcomeResultMatch(Regex resultPattern, TestOutcome outcome, string partialEndIdentifier, TestResultObject result, string data)
        {
            // Parse
            var outcomeResultMatch = this.resultPattern.Match(partialEndIdentifier);
            if (!outcomeResultMatch.Success) { return false; }

            // Validate.
            if (outcomeResultMatch.Groups.Count < Constants.OutcomePatternsExpectedGroups)
            {
                var message = $"Expected groups: {Constants.OutcomePatternsExpectedGroups}. Found: {outcomeResultMatch.Groups.Count} in pattern: {resultPattern} for data: {data}";
                this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedParsingError, message, aggregate: true);
                this.diagnosticDataCollector.Error(message);
            }

            // Set outcome
            result.Outcome = outcome;

            // Set test time.
            var testTimeIdentifier = outcomeResultMatch.Groups[Constants.TestTimeGroupIndex].Value.Trim();
            var testTime = this.GetTestTime(testTimeIdentifier);
            result.ExecutionTime = testTime;

            return true;
        }

        /// <summary>
        /// Gets test time.
        /// </summary>
        /// <param name="testTimeIdentifier">Test time identifier.</param>
        /// <returns>Test time.</returns>
        private TimeSpan GetTestTime(string testTimeIdentifier)
        {
            var succeed = double.TryParse(testTimeIdentifier, out double testTime);
            return succeed ? TimeSpan.FromSeconds(testTime) : TimeSpan.Zero;
        }

        /// <summary>
        /// Set partial starting result data in result object.
        /// </summary>
        /// <param name="partialStartIdentifier">Partial start identifier.</param>
        /// <param name="result">Result.</param>
        /// <param name="data">Data.</param>
        /// <returns></returns>
        private bool SetPartialStartingResultData(string partialStartIdentifier, TestResultObject result, string data)
        {
            // Partial result (starting) contains only result name.
            string resultName = GetResultName(data, partialStartIdentifier);

            // Set result data in result.
            result.Name = resultName;

            // Set parsing as unsuccessful if result name is null.
            return resultName != null;
        }

        /// <summary>
        /// Set partial ending result data in result object.
        /// </summary>
        /// <param name="partialEndIdentifier">Partial end identifier.</param>
        /// <param name="result">Result.</param>
        /// <param name="data">Data.</param>
        /// <returns></returns>
        private bool SetPartialEndingResultData(string partialEndIdentifier, TestResultObject result, string data, bool isPartialEnd)
        {
            // Check for passed test.
            var parsed = this.CheckForOutcomeResultMatch(this.passedResultPattern, TestOutcome.Passed, partialEndIdentifier, result, data);
            if (parsed) { return true; }

            // Check for failed test
            parsed = CheckForOutcomeResultMatch(this.failedResultPattern, TestOutcome.Failed, partialEndIdentifier, result, data);
            if (parsed) { return true; }

            // Check for expected failure test.
            // Note: Currently we are setting expected failure outcome also as passed.
            parsed = CheckForOutcomeResultMatch(this.expectedFailureResultPattern, TestOutcome.Passed, partialEndIdentifier, result, data);
            if (parsed) { return true; }

            // Check for skipped test.
            // Note: For partial end result, no need to check skipped test as skipped test always comes in one go. (i.e. console output os a test doesn't cause skip outcome to come after few lines)
            if (!isPartialEnd)
            {
                parsed = this.CheckForSkippedResultMatch(this.skippedResultPattern, TestOutcome.Skipped, partialEndIdentifier, result, data);
            }

            return parsed;
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
