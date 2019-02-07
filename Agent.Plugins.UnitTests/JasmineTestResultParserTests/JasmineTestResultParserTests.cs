// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.JasmineTestResultParserTests
{
    [TestClass]
    public class JasmineTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            Parser = new JasmineTestResultParser(TestRunManagerMock.Object, DiagnosticDataCollector.Object, TelemetryDataCollector.Object);
        }

        #region DataDrivenTests

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void JasmineTestResultParserSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "SuccessScenarios", testCase);
            TestSuccessScenariosWithBasicAssertions(testCase, true, false, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void JasmineTestResultParserPartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void JasmineTestResultParserDetailedAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetStackTraceTestCases), DynamicDataSourceType.Method)]
        public void JasmineParserStackTraceTests(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "StackTraceTests", testCase);
            TestWithStackTraceAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetCommonNegativeTestsTestCases), DynamicDataSourceType.Method)]
        public void JasmineTestResultParserCommonNegativeTests(string testCase)
        {
            testCase = Path.Combine("CommonTestResources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }


        #endregion

        #region Data Drivers

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JasmineTestResultParserTests", "Resources", "SuccessScenarios"));
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JasmineTestResultParserTests", "Resources", "PartialSuccess"));
        }
        public static IEnumerable<object[]> GetDetailedTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JasmineTestResultParserTests", "Resources", "DetailedTests"));
        }
        public static IEnumerable<object[]> GetStackTraceTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("JasmineTestResultParserTests", "Resources", "StackTraceTests"));
        }

        public static IEnumerable<object[]> GetCommonNegativeTestsTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("CommonTestResources", "NegativeTests"));
        }

        #endregion
    }
}