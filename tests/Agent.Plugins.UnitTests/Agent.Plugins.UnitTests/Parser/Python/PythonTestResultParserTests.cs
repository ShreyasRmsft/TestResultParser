namespace Agent.Plugins.UnitTests.Parser.Python
{
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Parser.Python;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Agent.Plugins.UnitTests.Parser.Python.Resources.SuccessScenarios;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    
    [TestClass]
    public class PythonTestResultParserTests
    {
        private Mock<ITestRunManager> testRunManager;

        public PythonTestResultParserTests()
        {
            testRunManager = new Mock<ITestRunManager>();
        }

        [DataTestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void ParseResultsSuccessTestCase(string testCase)
        {
            string testResultsConsoleOut = typeof(PythonSuccessTestCases).GetProperty(testCase, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString();
            var strings = testResultsConsoleOut.Split("\r\n");

            TestRun resultTestRun = null;
            testRunManager.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>((r) =>  resultTestRun = r);

            var parser = new PythonTestResultParser(testRunManager.Object);
            foreach (var line in strings)
            {
                parser.Parse(new LogLineData() { Line = line });
            }

            TestResultAssertUtility(new List<TestRun> { resultTestRun }, testCase + "Result");
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            foreach (var property in typeof(PythonSuccessTestCases).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (property.Name.StartsWith("TestCase") && !property.Name.EndsWith("Result"))
                {
                    yield return new object[] { property.Name };
                }
            }
        }

        public void TestResultAssertUtility(List<TestRun> testResults, string testResultFileName)
        {
            var resultFileContents = typeof(PythonSuccessTestCases).GetProperty(testResultFileName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString().Split(Environment.NewLine);

            int i;

            for (i = 0; i < resultFileContents.Length; i += 4)
            {
                int expectedPassedTestsCount = int.Parse(resultFileContents[i + 0]);
                int expectedFailedTestsCount = int.Parse(resultFileContents[i + 1]);
                int expectedTotalTestsCount = int.Parse(resultFileContents[i + 2]);
                long expectedTestRunDuration = long.Parse(resultFileContents[i + 3]);

                Assert.AreEqual(expectedPassedTestsCount, testResults[i / 4].PassedTests.Count, "Passed tests count does not match.");
                Assert.AreEqual(expectedFailedTestsCount, testResults[i / 4].TestRunSummary.TotalFailed, "Failed tests count does not match.");
                Assert.AreEqual(expectedFailedTestsCount, testResults[i / 4].FailedTests.Count, "Failed tests count does not match.");

                Assert.AreEqual(expectedTotalTestsCount, testResults[i / 4].TestRunSummary.TotalTests, "Failed tests count does not match.");
                Assert.AreEqual(expectedTestRunDuration, testResults[i / 4].TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
            }

            Assert.AreEqual(i / 3, testResults.Count, $"Expected {i / 3} test results.");
        }
    }
}
