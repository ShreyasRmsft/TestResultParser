// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Agent.Plugins.Log.TestResultParser.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Plugins.UnitTests.JestTestResultParserTests
{
    [TestClass]
    public class JestTestResultParserRegexTests
    {
        [DataTestMethod]
        [DynamicData(nameof(GetRegexPatterns), DynamicDataSourceType.Method)]
        public void RegexPatternTest(string regexPattern)
        {
            var postiveTestCases = File.ReadAllLines(Path.Combine("JestTestResultParserTests", "Resources", "RegexTests", "PositiveMatches", $"{regexPattern}.txt"));
            var regex = typeof(JestRegexs).GetProperty(regexPattern).GetValue(null);
            foreach (var testCase in postiveTestCases)
            {
                Assert.IsTrue(((Regex)regex).Match(testCase).Success, $"Should have matched:{testCase}");
            }

            var negativeTestCases = File.ReadAllLines(Path.Combine("JestTestResultParserTests", "Resources", "RegexTests", "NegativeMatches", $"{regexPattern}.txt"));

            foreach (var testCase in negativeTestCases)
            {
                Assert.IsFalse(((Regex)regex).Match(testCase).Success, $"Should not have matched:{testCase}");
            }
        }

        public static IEnumerable<object[]> GetRegexPatterns()
        {
            foreach (var property in typeof(JestRegexs).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                // Uncomment the below line to debug a particular set of test/s
                // if(property.Name.Contains("FailedTestsSummary"))
                yield return new object[] { property.Name };
            }
        }
    }
}
