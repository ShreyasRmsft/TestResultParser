// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Agent.Plugins.TestResultParser.Loggers.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;
    using Agent.Plugins.TestResultParser.Parser.Node.Mocha;
    using Agent.Plugins.TestResultParser.Telemetry.Interfaces;
    using Agent.Plugins.TestResultParser.TestResult.Models;
    using Agent.Plugins.TestResultParser.TestRunManger;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MochaTestResultParserTests
    {
        private Mock<ITraceLogger> diagnosticDataCollector;
        private Mock<ITelemetryDataCollector> telemetryDataCollector;

        [TestInitialize]
        public void TestInitialize()
        {
            // Mock logger to log to console for easy debugging
            diagnosticDataCollector = new Mock<ITraceLogger>();

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

            // No-op for telemetry
            telemetryDataCollector = new Mock<ITelemetryDataCollector>();
        }

        #region DataDrivenTests

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            var resultFileContents = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios", $"{testCase}Result.txt"));
            var testResultsConsoleOut = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios", $"{testCase}.txt"));

            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRun(testRun, resultFileContents, indexOfTestRun++);
            });

            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);

            int lineNumber = 0;

            foreach (var line in testResultsConsoleOut)
            {
                parser.Parse(new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 4), $"Expected {resultFileContents.Length / 4 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 4, indexOfTestRun, $"Expected {resultFileContents.Length / 4} test runs.");
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            var resultFileContents = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess", $"{testCase}Result.txt"));
            var testResultsConsoleOut = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess", $"{testCase}.txt"));

            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidatePartialSuccessTestRun(testRun, resultFileContents, indexOfTestRun++);
            });

            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);

            int lineNumber = 0;

            foreach (var line in testResultsConsoleOut)
            {
                parser.Parse(new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 7), $"Expected {resultFileContents.Length / 7 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 7, indexOfTestRun, $"Expected {resultFileContents.Length / 7} test runs.");
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            var resultFileContents = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests", $"{testCase}Result.txt"));
            var testResultsConsoleOut = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests", $"{testCase}.txt"));
            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRunWithDetails(testRun, resultFileContents);
            });

            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);
            int lineNumber = 0;

            foreach (var line in testResultsConsoleOut)
            {
                parser.Parse(new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Once, $"Expected a test run to have been published.");
        }

        #endregion

        #region DataDrivers

        [DataTestMethod]
        [DynamicData(nameof(GetNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void NegativeTests(string testCase)
        {
            var testResultsConsoleOut = File.ReadAllLines(Path.Combine("MochaTestResultParserTests", "Resources", "NegativeTests", $"{testCase}.txt"));
            var testRunManagerMock = new Mock<ITestRunManager>();

            testRunManagerMock.Setup(x => x.Publish(It.IsAny<TestRun>()));

            var parser = new MochaTestResultParser(testRunManagerMock.Object, diagnosticDataCollector.Object, telemetryDataCollector.Object);
            int lineNumber = 0;

            foreach (var line in testResultsConsoleOut)
            {
                parser.Parse(new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ });
            }

            testRunManagerMock.Verify(x => x.Publish(It.IsAny<TestRun>()), Times.Never, $"Expected no test run to have been published.");
        }

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            foreach (var testCase in new DirectoryInfo(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios")).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase007"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            foreach (var testCase in new DirectoryInfo(Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess")).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase009"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        public static IEnumerable<object[]> GetDetailedTestsTestCases()
        {
            foreach (var testCase in new DirectoryInfo(Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests")).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase007"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        public static IEnumerable<object[]> GetNegativeTestsTestCases()
        {
            foreach (var testCase in new DirectoryInfo(Path.Combine("MochaTestResultParserTests", "Resources", "NegativeTests")).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase007"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        #endregion

        #region ValidationHelpers

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

        public void ValidatePartialSuccessTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun)
        {
            int i = indexOfTestRun * 7;

            int expectedPassedTestsSummaryCount = int.Parse(resultFileContents[i + 0]);
            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedFailedTestsSummary = int.Parse(resultFileContents[i + 2]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 3]);
            int expectedSkippedTestsSummaryCount = int.Parse(resultFileContents[i + 4]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 5]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 6]);

            Assert.AreEqual(expectedPassedTestsSummaryCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsSummary, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsSummaryCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");

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
            for (int testIndex = 0; testIndex < expectedSkippedTestsCount; currentLine++, testIndex++)
            {
                Assert.AreEqual(resultFileContents[currentLine], testRun.SkippedTests[testIndex].Name, "Test Case name does not match.");
            }
        }

        #endregion

        #region utilites

        private string RemoveTimeStampFromLogLineIfPresent(string line)
        {
            // Remove the preceding timestamp if present.
            var trimTimeStamp = new Regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{7}Z (?<TrimmedLog>.*)", RegexOptions.ExplicitCapture);
            var match = trimTimeStamp.Match(line);

            if (match.Success)
            {
                return match.Groups["TrimmedLog"].Value;
            }

            return line;
        }

        #endregion
    }
}
