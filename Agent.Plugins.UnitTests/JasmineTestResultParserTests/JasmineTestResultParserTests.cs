// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Agent.Plugins.Log.TestResultParser.Parser;
using System.Collections.Generic;
using System.IO;

namespace Agent.Plugins.UnitTests.JasmineTestResultParserTests
{
    [TestClass]
    public class JasmineTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this.parser = new JasmineTestResultParser(this.testRunManagerMock.Object, this.diagnosticDataCollector.Object, this.telemetryDataCollector.Object);
        }

        #region DataDrivenTests

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void SuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "SuccessScenarios", testCase);
            TestSuccessScenariosWithBasicAssertions(testCase, true, false, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void PartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestsTestCases), DynamicDataSourceType.Method)]
        public void DetailedAssertions(string testCase)
        {
            testCase = Path.Combine("JasmineTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
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

        #endregion
    }
}