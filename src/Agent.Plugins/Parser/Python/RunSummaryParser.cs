namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using Agent.Plugins.TestResultParser.TestResult.Models;

    internal class RunSummaryParser : IRunSummaryParser
    {
        /// <inheritdoc />
        public TestRunSummary Parse(string data, TestRunSummary partialTestRunSummary)
        {
            // Validate
            if (data == null) { return null; }

            // Parse
            var partialSummaryExists = partialTestRunSummary != null;
            TestRunSummary runSummary = partialSummaryExists ? partialTestRunSummary : new TestRunSummary();
            var parsed = partialSummaryExists ?
                ParseForSummaryEnd(data, runSummary) :
                ParseForSummaryStart(data, runSummary);

            return parsed ? runSummary : null;
        }

        /// <summary>
        /// Parse input data for summary start.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="runSummary">Run summary.</param>
        /// <returns>True if parsing successful.</returns>
        private bool ParseForSummaryStart(string data, TestRunSummary runSummary)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parse input data for summary end.
        /// </summary>
        /// <param name="data">Data to be parsed.</param>
        /// <param name="runSummary">Run summary.</param>
        /// <returns>True if parsing successful.</returns>
        private bool ParseForSummaryEnd(string data, TestRunSummary runSummary)
        {
            throw new NotImplementedException();
        }
    }
}