namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using TestResultObject = Agent.Plugins.TestResultParser.TestResult.Models.TestResult;

    public interface IResultParser
    {
        /// <summary>
        /// Parses data to output test result object.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="partialTestResult">Partial test result.</param>
        /// <returns>Test result.</returns>
        TestResultObject Parse(string data, TestResultObject partialTestResult);
    }
}
