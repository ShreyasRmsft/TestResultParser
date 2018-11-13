namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Parser.Node.Mocha;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Agent.Plugins.UnitTests.MochaTestResultParserTests.Resources.DetailedTests;
    using Agent.Plugins.UnitTests.MochaTestResultParserTests.Resources.SuccessScenarios;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MochaTestResultParserTests
    {
        private Mock<ITraceLogger> diagnosticDataCollector;
        private Mock<ITelemetryDataCollector> telemetryDataCollector;

        public MochaTestResultParserTests()
        {
            diagnosticDataCollector = new Mock<ITraceLogger>();
            telemetryDataCollector = new Mock<ITelemetryDataCollector>();
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            var resultFileContents = typeof(DetailedTests).GetProperty(testCase + "Result", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString().Split(Environment.NewLine);
            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRunWithDetails(testRun, resultFileContents);
            });

            string testResultsConsoleOut = typeof(DetailedTests).GetProperty(testCase, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString();
            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);

            foreach (var line in testResultsConsoleOut.Split(Environment.NewLine))
            {
                parser.Parse(new LogLineData() { Line = line });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once, $"Expected a test run to have been published.");
        }

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            var resultFileContents = typeof(SuccessScenarios).GetProperty(testCase + "Result", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString().Split(Environment.NewLine);

            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRun(testRun, resultFileContents, indexOfTestRun++);
            });

            string testResultsConsoleOut = typeof(SuccessScenarios).GetProperty(testCase, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString();
            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);

            foreach (var line in testResultsConsoleOut.Split(Environment.NewLine))
            {
                parser.Parse(new LogLineData() { Line = line });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 3), $"Expected {resultFileContents.Length / 3 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 3, indexOfTestRun, $"Expected {resultFileContents.Length / 3} test runs.");
        }

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            foreach (var property in typeof(SuccessScenarios).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (property.Name.StartsWith("TestCase") && !property.Name.EndsWith("Result"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    //if (property.Name.Contains("TestCase015"))
                    yield return new object[] { property.Name };
                }
            }
        }

        public static IEnumerable<object[]> GetDetailedTestsTestCases()
        {
            foreach (var property in typeof(DetailedTests).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (property.Name.StartsWith("TestCase") && !property.Name.EndsWith("Result"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    //if (property.Name.Contains("TestCase015"))
                    yield return new object[] { property.Name };
                }
            }
        }

        public void ValidateTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun)
        {
            int i = indexOfTestRun * 4;

            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 0]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 2]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 3]);

            Assert.AreEqual(expectedPassedTestsCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");

            Assert.AreEqual(expectedTestRunDuration, testRun.TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
        }

        public void ValidateTestRunWithDetails(TestRun testRun, string[] resultFileContents)
        {
            int currentLine = 0;
            int expectedPassedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            for (int testIndex = 0; testIndex < expectedPassedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.PassedTests[testIndex].Name, "Test Case name does not match.");
            }

            currentLine++;
            int expectedFailedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            for (int testIndex = 0; testIndex < expectedFailedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.FailedTests[testIndex].Name, "Test Case name does not match.");
            }

            currentLine++;
            int expectedSkippedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");
            for (int testIndex = 0; testIndex < expectedFailedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.SkippedTests[testIndex].Name, "Test Case name does not match.");
            }
        }
    }
}
