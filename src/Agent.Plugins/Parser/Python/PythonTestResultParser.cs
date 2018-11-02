namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System.Collections.ObjectModel;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Telemetry;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using TestResultObject = Agent.Plugins.TestResultParser.TestResult.Models.TestResult;

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : ITestResultParser
    {
        private ParserState state;
        private TestRunSummary runSummary;
        private IResultParser resultParser;
        private ITestRunManager testRunManager;
        private IRunSummaryParser runSummaryParser;
        private Collection<TestResultObject> results;
        private ITelemetryDataCollector telemetryDataCollector;
        private IDiagnosticDataCollector diagnosticDataCollector;

        public PythonTestResultParser(ITestRunManager testRunManager) : this(testRunManager, new ResultParser(), new RunSummaryParser(), TelemetryDataCollector.Instance, DiagnosticDataCollector.Instance)
        {
        }

        internal PythonTestResultParser(ITestRunManager testRunManager, IResultParser resultParser, IRunSummaryParser runSummaryParser, ITelemetryDataCollector telemetryDataCollector, IDiagnosticDataCollector diagnosticDataCollector)
        {
            // TODO: What happens when stream ends and we haven't yet received the test run summary but there are few test results present. In this case, we should report this inconsistency to telemetry and diagnostics. But we are not getting any call on unsubscribe.
            this.testRunManager = testRunManager;
            this.telemetryDataCollector = telemetryDataCollector;
            this.diagnosticDataCollector = diagnosticDataCollector;
            this.resultParser = resultParser;
            this.runSummaryParser = runSummaryParser;
            this.state = ParserState.WaitingForResultOrSummary;
            this.results = new Collection<TestResultObject>();
        }

        /// <summary>
        /// Entry point for python test result parser.
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        public void ParseData(string data)
        {
            // Validate data input
            bool isValid = this.ValidateInput(data);
            if (!isValid) { return; }

            // Switch to proper parser state
            this.diagnosticDataCollector.Verbose($"Current state: {nameof(this.state)}");
            switch (state)
            {
                case ParserState.WaitingForPartialResultOrSummary:
                case ParserState.WaitingForResultOrSummary:
                    this.HandleWaitingForResultOrSummaryState(data);
                    break;
                case ParserState.WaitingForPartialSummary:
                case ParserState.WaitingForSummary:
                    this.HandleWaitingForSummaryState(data);
                    break;
            }

            // If test run is completed, flush the data and rest the parser
            if (state == ParserState.Completed)
            {
                this.diagnosticDataCollector.Verbose($"Current state: {nameof(this.state)}");
                this.HandleCompletedState();
            }
        }

        /// <summary>
        /// Handle completed state.
        /// </summary>
        private void HandleCompletedState()
        {
            this.FlushTestRun();
            this.Reset();
        }

        /// <summary>
        /// Handle waiting for summary state
        /// </summary>
        /// <param name="data">Data.</param>
        private void HandleWaitingForSummaryState(string data)
        {
            // Run summary parser
            var parsed = this.ParseTestRunSummary(data, this.runSummary);
        }

        /// <summary>
        /// Handle waiting for result or summary state.
        /// </summary>
        /// <param name="data">Data.</param>
        private void HandleWaitingForResultOrSummaryState(string data)
        {
            // Result parser
            var partialTestResult = this.GetPartialTestResult();
            var parsed = this.ParseTestResult(data, partialTestResult);
            if (parsed) { return; }

            // Run summary parser
            parsed = this.ParseTestRunSummary(data, this.runSummary);
        }

        /// <summary>
        /// Reset the parser to start parsing new test run.
        /// </summary>
        private void Reset()
        {
            this.results = new Collection<TestResultObject>();
            this.runSummary = null;
            this.state = ParserState.WaitingForResultOrSummary;
            this.diagnosticDataCollector.Info("Resetting the test run.");
        }

        /// <summary>
        /// Flush test run.
        /// </summary>
        private void FlushTestRun()
        {
            var testRun = new TestRun(results, runSummary);
            testRunManager.Publish(testRun);
            this.diagnosticDataCollector.Info($"Flushed the test run: {testRun}");
        }

        /// <summary>
        /// Parses test run summary.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="partialTestRunSummary">Partial test run summary.</param>
        /// <returns>True if parse successful.</returns>
        private bool ParseTestRunSummary(string data, TestRunSummary partialTestRunSummary)
        {
            // Validate. Partial test run summary should not be null if current state is WaitingForPartialSummary.
            if (state == ParserState.WaitingForPartialSummary && partialTestRunSummary == null)
            {
                HandlePartialRunSummaryInconsistentState(partialTestRunSummary, data);
                return false;
            }

            // Parse
            var runSummary = runSummaryParser.Parse(data, partialTestRunSummary);

            // Unable to parse
            if (runSummary == null) {
                this.diagnosticDataCollector.Verbose($"No summary pattern match for: {data}");
                return false;
            }

            // Run summary (start)
            if (this.state != ParserState.WaitingForPartialSummary)
            {
                this.diagnosticDataCollector.Info($"Test summary pattern (start) matched for: {data}");
                this.runSummary = runSummary;
                this.state = ParserState.WaitingForPartialSummary;
            }

            // Run summary (end)
            else // (this.state == ParserState.WaitingForPartialSummary)
            {
                this.diagnosticDataCollector.Info($"Test summary pattern (end) matched for: {data}");
                this.runSummary = runSummary;
                this.state = ParserState.Completed;
            }

            return true;
        }

        /// <summary>
        /// Parse test result.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="partialTestResult">Partial test result.</param>
        /// <returns>True if parse successful.</returns>
        private bool ParseTestResult(string data, TestResultObject partialTestResult)
        {
            // Validate. Partial test result should not be null if current state is WaitingForPartialResultOrSummary.
            if (state == ParserState.WaitingForPartialSummary && partialTestResult == null)
            {
                HandlePartialTestResultInconsistentState(null, null, data);
                return false;
            }

            // Parse
            var result = this.resultParser.Parse(data, partialTestResult);

            // Unable to parse
            if (result == null) {
                this.diagnosticDataCollector.Verbose($"No test result pattern match for: {data}");
                return false;
            }

            // Result is complete
            else if (this.state != ParserState.WaitingForPartialSummary && result.Outcome != TestOutcome.None)
            {
                this.diagnosticDataCollector.Info($"Test result pattern matched for: {data}");
                this.state = ParserState.WaitingForResultOrSummary;
                this.results.Add(result);
            }

            // Result is partial (starting portion of test result)
            else if (this.state != ParserState.WaitingForPartialSummary && result.Outcome == TestOutcome.None)
            {
                this.diagnosticDataCollector.Info($"Partial test result (start) pattern matched for: {data}");
                this.results.Add(result);
                this.state = ParserState.WaitingForPartialResultOrSummary;
            }

            // Result is partial (ending portion of test result).
            else if (this.state == ParserState.WaitingForPartialSummary && result.Outcome != TestOutcome.None && this.GetPartialTestResult() == partialTestResult)
            {
                this.diagnosticDataCollector.Info($"Partial test result (end) pattern matched for: {data}");
                this.results[this.GetPartialTestResultIndex()] = result;
                this.state = ParserState.WaitingForResultOrSummary;
            }

            // Inconsistent state. Skip reading test results. Listen only test run summary now.
            else // (this.state == ParserState.WaitingForPartialSummary && (result.Outcome == TestOutcome.None || this.GetPartialTestResult() != partialTestResult))
            {
                HandlePartialTestResultInconsistentState(partialTestResult, result, data);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Gets partial test result.
        /// </summary>
        /// <returns>Partial test result.</returns>
        private TestResultObject GetPartialTestResult()
        {
            return (this.state == ParserState.WaitingForPartialResultOrSummary) ?
                this.results[this.GetPartialTestResultIndex()] : null;
        }

        /// <summary>
        /// Gets partial test result index.
        /// </summary>
        /// <returns>Partial test result index.</returns>
        private int GetPartialTestResultIndex()
        {
            return this.results.Count - 1;
        }

        /// <summary>
        /// Handles partial test result's inconsistent state.
        /// </summary>
        /// <param name="partialTestResultStart">Partial test result start.</param>
        /// <param name="partialTestResultEnd">Partial test result end.</param>
        /// <param name="data">Data.</param>
        private void HandlePartialTestResultInconsistentState(TestResultObject partialTestResultStart, TestResultObject partialTestResultEnd, string data)
        {
            // TODO: override toString method of TestResult, TestRunSummary, TestRun model?
            string message = $"Inconsistent state.\nPartial test result start: {partialTestResultStart}.\nPartial test result end: {partialTestResultEnd}.\nActual data: {data}";
            this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedParsingError, message, aggregate: true);
            this.Reset();
            this.diagnosticDataCollector.Error(message);
            this.diagnosticDataCollector.Info("Skipping test results matching due to inconsistency. Matching only test run summary pattern now.");
            this.state = ParserState.WaitingForSummary;
        }

        /// <summary>
        /// Handles partial test run summary's inconsistent state.
        /// </summary>
        /// <param name="partialTestRunSummary">Partial test run summary.</param>
        /// <param name="data">Data.</param>
        private void HandlePartialRunSummaryInconsistentState(TestRunSummary partialTestRunSummary, string data)
        {
            // TODO: As inconsitency is while test run summary, should we throw here? (as we are expected to get test run summary correctly) OR is it ok to just output error and move ahead?
            string message = $"Inconsistent state.\nPartial test run summary: {partialTestRunSummary}.\nActual data: {data}";
            this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedParsingError, message, aggregate: true);
            this.Reset();
            this.diagnosticDataCollector.Error(message);
        }

        /// <summary>
        /// Validates the input data.
        /// </summary>
        /// <param name="data">Data to be validated.</param>
        /// <returns>True if data is valid.</returns>
        private bool ValidateInput(string data)
        {
            if (data == null)
            {
                this.telemetryDataCollector.AddProperty(TelemetryConstants.UnexpectedError, "Received null data", aggregate: true);
                this.diagnosticDataCollector.Error("Received null data"); // TODO: Is it ok to put in error here?
            }

            return data == null;
        }
    }
}
