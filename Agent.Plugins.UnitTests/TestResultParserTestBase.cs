// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Agent.Plugins.Log.TestResultParser.Contracts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    public abstract class TestResultParserTestBase
    {
        public TestContext TestContext { get; set; }

        protected Mock<ITraceLogger> _diagnosticDataCollector;
        protected Mock<ITelemetryDataCollector> _telemetryDataCollector;
        protected Mock<ITestRunManager> _testRunManagerMock;
        protected ITestResultParser _parser;
        protected bool _isPythonParser = false;

        private bool _perfRegressed = false;
        private int _timesPerfTelemtryFired = 0;
        private double _totalTime = 0;
        private double perLineParseThresholdTimeInMilliseconds = 1;

        public TestResultParserTestBase()
        {
            this._testRunManagerMock = new Mock<ITestRunManager>();

            // Mock logger to log to console for easy debugging
            this._diagnosticDataCollector = new Mock<ITraceLogger>();

            this._diagnosticDataCollector.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Info: {data}"); });
            this._diagnosticDataCollector.Setup(x => x.Verbose(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Verbose: {data}"); });
            this._diagnosticDataCollector.Setup(x => x.Error(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Error: {data}"); });

            this._diagnosticDataCollector.Setup(x => x.Warning(It.IsAny<string>())).Callback<string>(data =>
            {
                TestContext.WriteLine($"Warning: {data}");
                if (data.StartsWith("PERF :"))
                {
                    this._perfRegressed = true;
                }
            });

            this._telemetryDataCollector = new Mock<ITelemetryDataCollector>();

            this._telemetryDataCollector.Setup(x => x.AddToCumulativeTelemetry(It.IsAny<string>(), It.IsRegex(".*ParserTotalTime"), It.IsAny<object>(), It.IsAny<bool>()))
                .Callback<string, string, object, bool>((string eventArea, string eventName, object value, bool aggregate) =>
                    {
                        ++this._timesPerfTelemtryFired;
                        this._totalTime += (double)value;
                    });
        }

        public void TestSuccessScenariosWithBasicAssertions(string testCase, bool assertFailedCount = true, bool assertPassedCount = true, bool assertSkippedCount = true)
        {
            int indexOfTestRun = 0;
            int lastTestRunId = 0;
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            this._testRunManagerMock.Setup(x => x.PublishAsync(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRun(testRun, resultFileContents, indexOfTestRun++, lastTestRunId, assertFailedCount, assertPassedCount, assertSkippedCount);
                lastTestRunId = testRun.TestRunId;
            });

            var inputLines = GetLines(testCase).ToList();
            foreach (var line in inputLines)
            {
                this._parser.Parse(line);
            }

            this._testRunManagerMock.Verify(x => x.PublishAsync(It.IsAny<TestRun>()), Times.Exactly(Math.Max(1, resultFileContents.Length / 5)), $"Expected {Math.Max(1, resultFileContents.Length / 5)} test runs.");

            ValidatePerf(inputLines.Count, _isPythonParser)
;        }

        public void TestPartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            int indexOfTestRun = 0;
            int lastTestRunId = 0;
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            this._testRunManagerMock.Setup(x => x.PublishAsync(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidatePartialSuccessTestRun(testRun, resultFileContents, indexOfTestRun++, lastTestRunId);
                lastTestRunId = testRun.TestRunId;
            });

            var inputLines = GetLines(testCase).ToList();
            foreach (var line in inputLines)
            {
                this._parser.Parse(line);
            }

            // TODO: fix assertions
            this._testRunManagerMock.Verify(x => x.PublishAsync(It.IsAny<TestRun>()), Times.Exactly(resultFileContents.Length / 7), $"Expected {resultFileContents.Length / 7 } test runs.");
            Assert.AreEqual(resultFileContents.Length / 8, indexOfTestRun, $"Expected {resultFileContents.Length / 7} test runs.");

            ValidatePerf(inputLines.Count, _isPythonParser);
        }

        public void TestWithDetailedAssertions(string testCase)
        {
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            this._testRunManagerMock.Setup(x => x.PublishAsync(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRunWithDetails(testRun, resultFileContents);
            });

            var inputLines = GetLines(testCase).ToList();
            foreach (var line in inputLines)
            {
                this._parser.Parse(line);
            }

            this._testRunManagerMock.Verify(x => x.PublishAsync(It.IsAny<TestRun>()), Times.Once, $"Expected a test run to have been Published.");

            ValidatePerf(inputLines.Count, _isPythonParser);
        }

        public void TestWithStackTraceAssertions(string testCase)
        {
            var resultFileContents = File.ReadAllLines($"{testCase}Result.txt");

            this._testRunManagerMock.Setup(x => x.PublishAsync(It.IsAny<TestRun>())).Callback<TestRun>(testRun =>
            {
                ValidateTestRunWithStackTraces(testRun, resultFileContents);
            });

            var inputLines = GetLines(testCase).ToList();
            foreach (var line in inputLines)
            {
                this._parser.Parse(line);
            }

            this._testRunManagerMock.Verify(x => x.PublishAsync(It.IsAny<TestRun>()), Times.Once, $"Expected a test run to have been Published.");

            ValidatePerf(inputLines.Count, _isPythonParser);
        }

        public void TestNegativeTestsScenarios(string testCase)
        {
            var inputLines = GetLines(testCase).ToList();
            foreach (var line in inputLines)
            {
                this._parser.Parse(line);
            }

            this._testRunManagerMock.Verify(x => x.PublishAsync(It.IsAny<TestRun>()), Times.Never, $"Expected no test run to have been Published.");

            ValidatePerf(inputLines.Count, _isPythonParser);
        }

        #region Data Drivers

        public static IEnumerable<object[]> GetTestCasesFromRelativePath(string relativePathToTestCase)
        {
            foreach (var testCase in new DirectoryInfo(relativePathToTestCase).GetFiles("TestCase*.txt"))
            {
                if (!testCase.Name.EndsWith("Result.txt"))
                {
                    // Uncomment the below line to run for a particular test case for debugging 
                    // if (testCase.Name.Contains("TestCase002"))
                    yield return new object[] { testCase.Name.Split(".txt")[0] };
                }
            }
        }

        #endregion

        #region Log Data Utilities

        public IEnumerable<LogData> GetLines(string testCase)
        {
            // Pre-pad the test case with initiazization logs to simulate the targeted scenario
            // meant mainly to get more accurate perf readings
            var linesToBePrependedToTheLogs = File.ReadAllLines(Path.Combine("CommonTestResources", "LogPadding", "PrePadding.txt"));

            // Do not assign actual line numbers to these bogus lines as debugging 
            // the actual test case file will become difficult

            // The prepended logs will have a negative line number 
            // and the actual line numebers will start at 1
            int lineNumber = 1 - linesToBePrependedToTheLogs.Length;

            var testResultsConsoleOut = linesToBePrependedToTheLogs.Concat(File.ReadAllLines($"{testCase}.txt"));
            foreach (var line in testResultsConsoleOut)
            {
                yield return new LogData() { Line = RemoveTimeStampFromLogLineIfPresent(line), LineNumber = lineNumber++ };
            }
        }

        public string RemoveTimeStampFromLogLineIfPresent(string line)
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

        #region ValidationHelpers

        public void ValidateTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun,
            int lastTestRunId, bool assertFailedCount = true, bool assertPassedCount = true, bool assertSkippedCount = true)
        {
            int i = indexOfTestRun * 5;

            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 0]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 2]);
            int expectedTotalTestsCount = int.Parse(resultFileContents[i + 3]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 4]);

            Assert.AreEqual(expectedPassedTestsCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");
            Assert.AreEqual(expectedTotalTestsCount, testRun.TestRunSummary.TotalTests, "Total tests summary does not match.");

            if (assertFailedCount)
            {
                Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            }

            if (assertPassedCount)
            {
                Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            }

            if (assertSkippedCount)
            {
                Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");
            }

            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            Assert.IsTrue(testRun.TestRunId > lastTestRunId, $"Expected test run id greater than {lastTestRunId} but found {testRun.TestRunId} instead.");
            Assert.AreEqual(expectedTestRunDuration, testRun.TestRunSummary.TotalExecutionTime.TotalMilliseconds, "Test run duration did not match.");
        }

        public void ValidatePartialSuccessTestRun(TestRun testRun, string[] resultFileContents, int indexOfTestRun, int lastTestRunId)
        {
            int i = indexOfTestRun * 8;

            int expectedPassedTestsSummaryCount = int.Parse(resultFileContents[i + 0]);
            int expectedPassedTestsCount = int.Parse(resultFileContents[i + 1]);
            int expectedFailedTestsSummary = int.Parse(resultFileContents[i + 2]);
            int expectedFailedTestsCount = int.Parse(resultFileContents[i + 3]);
            int expectedSkippedTestsSummaryCount = int.Parse(resultFileContents[i + 4]);
            int expectedSkippedTestsCount = int.Parse(resultFileContents[i + 5]);
            int expectedTotalTestsCount = int.Parse(resultFileContents[i + 6]);
            long expectedTestRunDuration = long.Parse(resultFileContents[i + 7]);

            Assert.AreEqual(expectedPassedTestsSummaryCount, testRun.TestRunSummary.TotalPassed, "Passed tests summary does not match.");
            Assert.AreEqual(expectedFailedTestsSummary, testRun.TestRunSummary.TotalFailed, "Failed tests summary does not match.");
            Assert.AreEqual(expectedSkippedTestsSummaryCount, testRun.TestRunSummary.TotalSkipped, "Skipped tests summary does not match.");
            Assert.AreEqual(expectedTotalTestsCount, testRun.TestRunSummary.TotalTests, "Total tests summary does not match.");

            Assert.AreEqual(expectedPassedTestsCount, testRun.PassedTests.Count, "Passed tests count does not match.");
            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");
            Assert.AreEqual(expectedSkippedTestsCount, testRun.SkippedTests.Count, "Skipped tests count does not match.");

            Assert.IsTrue(testRun.TestRunId > lastTestRunId, $"Expected test run id greater than {lastTestRunId} but found {testRun.TestRunId} instead.");
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

        public void ValidateTestRunWithStackTraces(TestRun testRun, string[] resultFileContents)
        {
            int currentLine = 0;
            int expectedFailedTestsCount = int.Parse(resultFileContents[currentLine].Split(" ")[1]);
            currentLine++;

            Assert.AreEqual(expectedFailedTestsCount, testRun.FailedTests.Count, "Failed tests count does not match.");

            for (int testIndex = 0; testIndex < expectedFailedTestsCount; testIndex++)
            {
                string expectedStackTrace = null;

                while (resultFileContents[currentLine] != "-----EndOfStackTrace-----")
                {
                    if (expectedStackTrace == null)
                    {
                        expectedStackTrace = resultFileContents[currentLine];
                    }
                    else
                    {
                        expectedStackTrace += Environment.NewLine + resultFileContents[currentLine];
                    }

                    currentLine++;
                }

                currentLine++;

                try
                {
                    Assert.AreEqual(expectedStackTrace, testRun.FailedTests[testIndex].StackTrace, "Stack trace contents do not match.");
                }
                catch
                {
                    if (expectedStackTrace == null && testRun.FailedTests[testIndex].StackTrace == string.Empty)
                    {
                        return;
                    }

                    throw;
                }
            }
        }

        #endregion

        #region PerfValidationHelpers

        private void ValidatePerf(int inputLinesCount, bool isPythonParser = false)
        {
            // Perf related asserts
            Assert.IsFalse(this._perfRegressed, "Regression in perf, please check logs for more details.");

            if (isPythonParser)
            {
                // The python parser is peculiar in the aspect that it recalls Parse after a reset for certain lines
                Assert.IsTrue(inputLinesCount <= this._timesPerfTelemtryFired, $"Perf telemetry not fired for every line, " +
                    $" expected atleast {inputLinesCount} events but got {this._timesPerfTelemtryFired}");
            }
            else
            {
                Assert.AreEqual(inputLinesCount, this._timesPerfTelemtryFired, "Perf telemetry not fired for every line");
            }

            Assert.IsTrue(this._totalTime / inputLinesCount < this.perLineParseThresholdTimeInMilliseconds, $"The average allowed parse time per line is {this.perLineParseThresholdTimeInMilliseconds} but in this" +
                $" case it was {this._totalTime / inputLinesCount}");
        }

        #endregion
    }
}
