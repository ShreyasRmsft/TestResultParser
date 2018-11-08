namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System.Collections.ObjectModel;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using TestResult = TestResult.Models.TestResult;

    public class MochaTestResultParser : ITestResultParser
    {
        public TestRun TestRun = new TestRun { };

        private MochaTestResultParserStateModel stateContext = MochaTestResultParserStateModel.ParsingTestResults;

        /// <inheritdoc/>
        public void Parse(LogLineData testResultsLine)
        {
            switch (state)
            {
                case MochaTestResultParserStateModel.ParsingTestResults:

                    if (MatchPassedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassed");
                        return;
                    }
                    else if (MatchFailedTestCase(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedUnicode");
                        return;
                    }
                    else if (MatchPassedSummary(testResultsLine.Line))
                    {
                        //LogIfDebug(testResultsLine, "MatchPassedSummary");
                        return;
                    }

                    break;
            }
        }

        private bool MatchPassedTestCase(string testResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestCaseMatcher.Match(testResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                testResult.Outcome = TestOutcome.Passed;
                testResult.Name = match.Groups[2].Value;

                // TODO: Logic for resetting the run. This should also include a publish step if enough summary data was encountered
                if (SummaryEncountered)
                {
                    StartNewTestResult();
                }

                TestRun.TestResults.Add(testResult);

                return true;
            }

            return false;
        }

        private bool MatchFailedTestCase(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.FailedTestCaseMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                var testResult = new TestResult();

                // handle parse errors everywhere
                int testCaseNumber = int.Parse(match.Groups[1].Value);

                // we can also choose to ignore extra failures post summary if the number is not 1
                if (testCaseNumber != lastFailedTestCaseNumberEncountered + 1)
                {
                    throw new Exception($"Expecting failed test case with number {lastFailedTestCaseNumberEncountered + 1} but found {testCaseNumber} instead.");
                }

                lastFailedTestCaseNumberEncountered++;

                if (ignoreFailedTestsPostSummary)
                {
                    failedTestsToSkipParsingPostSummary--;
                    if (failedTestsToSkipParsingPostSummary == 0)
                    {
                        ignoreFailedTestsPostSummary = false;
                        lastFailedTestCaseNumberEncountered = 0;
                    }

                    return true;
                }

                if (SummaryEncountered)
                {
                    StartNewTestResult();
                }

                failedTestsToSkipParsingPostSummary++;

                testCase.State = "Failed";
                testCase.TestCaseName = match.Groups[2].Value;

                TestResults[TestResults.Count - 1].TestCases.Add(testCase);

                return true;
            }

            return false;
        }

        private bool MatchPassedSummary(string mochaTestResultsLine)
        {
            var match = MochaTestResultParserRegularExpressions.PassedTestsSummaryMatcher.Match(mochaTestResultsLine);

            if (match.Success)
            {
                SummaryEncountered = true;
                lastFailedTestCaseNumberEncountered = 0;

                if (failedTestsToSkipParsingPostSummary == 0)
                {

                }
                else
                {
                    ignoreFailedTestsPostSummary = true;
                }

                TestResults[TestResults.Count - 1].TotalPassed = TestResults[TestResults.Count - 1].TestCases.FindAll(testCase => { return testCase.State == "Passed"; }).Count;

                // handle parse exceptions
                // change all string comparisons to enums or something better
                if (TestResults[TestResults.Count - 1].TotalPassed != int.Parse(match.Groups[1].Value))
                {
                    // make custom exception and handle
                    throw new Exception($"Oops, parsing failed :(, parsing yielded {TestResults[TestResults.Count - 1].TotalPassed} passed tests but summary indicated {match.Groups[1].Value} tests passed.");
                }

                // handle parse exceptions
                switch (match.Groups[3].Value)
                {
                    case "ms":
                        TestResults[TestResults.Count - 1].TestRunDuration = long.Parse(match.Groups[2].Value);
                        break;

                    case "s":
                        TestResults[TestResults.Count - 1].TestRunDuration = long.Parse(match.Groups[2].Value) * 1000;
                        break;

                    case "m":
                        TestResults[TestResults.Count - 1].TestRunDuration = long.Parse(match.Groups[2].Value) * 60 * 1000;
                        break;

                    case "h":
                        TestResults[TestResults.Count - 1].TestRunDuration = long.Parse(match.Groups[2].Value) * 60 * 60 * 1000;
                        break;
                }

                return true;
            }

            return false;
        }

    }
}
