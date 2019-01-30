// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// TODO: Add test cases containing nested test suites

namespace Agent.Plugins.UnitTests.MochaTestResultParserTests
{
    [TestClass]
    public class MochaTestResultParserTests : TestResultParserTestBase
    {
        [TestInitialize]
        public void TestInit()
        {
            this._parser = new MochaTestResultParser(this._testRunManagerMock.Object, this._diagnosticDataCollector.Object, this._telemetryDataCollector.Object);
        }

        #region DataDrivenTests

        [DataTestMethod]
        [DynamicData(nameof(GetSuccessScenariosTestCases), DynamicDataSourceType.Method)]
        public void MochaParserSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios", testCase);
            TestSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPartialSuccessTestCases), DynamicDataSourceType.Method)]
        public void MochaParserPartialSuccessScenariosWithBasicAssertions(string testCase)
        {
            testCase = Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess", testCase);
            TestPartialSuccessScenariosWithBasicAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetDetailedTestCases), DynamicDataSourceType.Method)]
        public void MochaParserDetailedAssertions(string testCase)
        {
            testCase = Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests", testCase);
            TestWithDetailedAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetNegativeTestCases), DynamicDataSourceType.Method)]
        public void MochaParserNegativeTests(string testCase)
        {
            testCase = Path.Combine("MochaTestResultParserTests", "Resources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetStackTraceTestCases), DynamicDataSourceType.Method)]
        public void MochaParserStackTraceTests(string testCase)
        {
            testCase = Path.Combine("MochaTestResultParserTests", "Resources", "StackTraceTests", testCase);
            TestWithStackTraceAssertions(testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetCommonNegativeTestCases), DynamicDataSourceType.Method)]
        public void MochaParserCommonNegativeTests(string testCase)
        {
            testCase = Path.Combine("CommonTestResources", "NegativeTests", testCase);
            TestNegativeTestsScenarios(testCase);
        }

        #endregion

        #region Data Drivers

        public static IEnumerable<object[]> GetSuccessScenariosTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "SuccessScenarios"));
        }

        public static IEnumerable<object[]> GetPartialSuccessTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "PartialSuccess"));
        }

        public static IEnumerable<object[]> GetDetailedTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "DetailedTests"));
        }

        public static IEnumerable<object[]> GetNegativeTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "NegativeTests"));
        }

        public static IEnumerable<object[]> GetStackTraceTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("MochaTestResultParserTests", "Resources", "StackTraceTests"));
        }

        public static IEnumerable<object[]> GetCommonNegativeTestCases()
        {
            return GetTestCasesFromRelativePath(Path.Combine("CommonTestResources", "NegativeTests"));
        }

        #endregion
    }
}
