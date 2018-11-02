namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public interface IRunSummaryParser
    {
        /// <summary>
        /// Parses data to output run summary.
        /// </summary>
        /// <param name="data">Data to be passed.</param>
        /// <param name="partialTestRunSummary">Partial test run summary.</param>
        /// <returns>Test run summary.</returns>
        TestRunSummary Parse(string data, TestRunSummary partialTestRunSummary);
    }
}
