namespace Agent.Plugins.UnitTests.Parser.Python
{
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Parser.Python;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    
    [TestClass]
    public class PythonTestResultParserTests
    {
        private Mock<ITraceLogger> diagnosticDataCollector;
        private Mock<ITelemetryDataCollector> telemetryDataCollector;

        [TestInitialize]
        public void TestInit()
        {
            diagnosticDataCollector = new Mock<ITraceLogger>();
            telemetryDataCollector = new Mock<ITelemetryDataCollector>();

            diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data =>
            {
                Console.WriteLine($"Info: {data}");
            });

            diagnosticDataCollector.Setup(x => x.Verbose(It.IsAny<string>())).Callback<string>(data =>
            {
                Console.WriteLine($"Verbose: {data}");
            });

            diagnosticDataCollector.Setup(x => x.Warning(It.IsAny<string>())).Callback<string>(data =>
            {
                Console.WriteLine($"Warning: {data}");
            });

            diagnosticDataCollector.Setup(x => x.Error(It.IsAny<string>())).Callback<string>(data =>
            {
                Console.WriteLine($"Error: {data}");
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            var resultFileContents = File.ReadAllLines(Path.Combine("PythonTestResultParserTests", "Resources", "SuccessScenarios", $"{testCase}Result.txt"));
            var testResultsConsoleOut = File.ReadAllLines(Path.Combine("PythonTestResultParserTests", "Resources", "SuccessScenarios", $"{testCase}.txt"));

            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRun(testRun, resultFileContents, indexOfTestRun++);
            });

            var parser = new PythonTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);

            int lineNumber = 0;

            foreach (var line in testResultsConsoleOut)
            {
                parser.Parse(new LogData() { Line = line, LineNumber = lineNumber++ });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 4), $"Expected {resultFileContents.Length / 4 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 4, indexOfTestRun, $"Expected {resultFileContents.Length / 4} test runs.");
        }

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            foreach (var testCase in new DirectoryInfo(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios")).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    if (testCase.Name.Contains("TestCase004"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        public void ValidateTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun)
        {
            int i = indexOfTestRun * 4;

            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 0]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedTotalTestsCount = int.Parse(resultFileContents[i + 2]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 3]);

            Assert.AreEqual(expectedFailedTestsCount, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedTotalTestsCount, testRun.TestRunSummary.TotalTests, "Total tests summary does not match.");

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            
            Assert.AreEqual(expectedTestRunDuration, testRun.TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
        }
    }
}
