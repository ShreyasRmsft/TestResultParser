// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.JasmineTestResultParserTests
{
    [TestClass]
    public class JasmineTestResultParserRegexTests
    {
        [DataTestMethod]
        [DynamicData(nameof(GetRegexPatterns), DynamicDataSourceType.Method)]
        public void RegexPatternTest(string regexPattern)
        {
            var postiveTestCases = File.ReadAllLines(Path.Combine("JasmineTestResultParserTests", "Resources", "RegexTests", "PositiveMatches", $"{regexPattern}.txt"));
            var regex = typeof(JasmineRegexes).GetProperty(regexPattern).GetValue(null);
            foreach (var testCase in postiveTestCases)
            {
                Assert.IsTrue(((Regex)regex).Match(testCase).Success, $"Should have matched:{testCase}");
            }

            var negativeTestCases = File.ReadAllLines(Path.Combine("JasmineTestResultParserTests", "Resources", "RegexTests", "NegativeMatches", $"{regexPattern}.txt"));

            foreach (var testCase in negativeTestCases)
            {
                Assert.IsFalse(((Regex)regex).Match(testCase).Success, $"Should not have matched:{testCase}");
            }
        }

        public static IEnumerable<object[]> GetRegexPatterns()
        {
            foreach (var property in typeof(JasmineRegexes).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                //if(property.Name.Contains("FailedTestsSummary"))
                yield return new object[] { property.Name };
            }
        }
    }
}
