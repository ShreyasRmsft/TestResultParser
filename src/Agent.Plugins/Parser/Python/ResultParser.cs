namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using TestResultObject = Agent.Plugins.TestResultParser.TestResult.Models.TestResult;

    public class ResultParser : IResultParser
    {
        /// <inheritdoc />
        public TestResultObject Parse(string data, TestResultObject partialTestResult)
        {
            throw new NotImplementedException();
        }
    }
}
