﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    /// <summary>
    /// Contains the Test results and Test Summary for the Test run
    /// </summary>
    public class TestRun
    {
        public TestRun(string parserUri, string runNamePrefix, int testRunId)
        {
            if (parserUri.Split('/').Length != 2)
            {
                throw new ArgumentException("The parser Uri should be of the format {ParserName}/{ParserVersion}");
            }

            ParserUri = parserUri;
            RunNamePrefix = runNamePrefix;
            TestRunId = testRunId;
        }

        /// <summary>
        /// Uri of the parser class that parsed the test run. Uri contains name and version number of the parser
        /// </summary>
        public string ParserUri { get; }

        /// <summary>
        /// Name to be used while publishing the test run
        /// </summary>
        public string TestRunName
        {
            get
            {
                return $"{RunNamePrefix} test run {TestRunId} - automatically inferred results";
            }
        }

        /// <summary>
        /// Test run id relative to the parser. This along with the parser name will uniquely identify a run
        /// </summary>
        public int TestRunId { get; }

        /// <summary>
        /// The prefix to be addded to the test run title
        /// </summary>
        public string RunNamePrefix { get; }

        /// <summary>
        /// Collection of passed tests
        /// </summary>
        public List<TestResult> PassedTests { get; set; } = new List<TestResult>();

        /// <summary>
        /// Collection of failed tests
        /// </summary>
        public List<TestResult> FailedTests { get; set; } = new List<TestResult>();

        /// <summary>
        /// Collection of skipped tests
        /// </summary>
        public List<TestResult> SkippedTests { get; set; } = new List<TestResult>();

        /// <summary>
        /// Summary for the test run
        /// </summary>
        public TestRunSummary TestRunSummary { get; set; } = new TestRunSummary();
    }
}
