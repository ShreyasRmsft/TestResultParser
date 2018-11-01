namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using Agent.Plugins.TestResultParser.TestResult.Models;

    public interface IRunSummaryParser
    {
        /// <summary>
        /// Parses data to output run summary.
        /// </summary>
        /// <param name="data">Data to be passed.</param>
        /// <param name="runSummary">Run summary.</param>
        /// <returns>Data to be parsed.</returns>
        TestRunSummary Parse(string data, TestRunSummary runSummary);
    }
}
