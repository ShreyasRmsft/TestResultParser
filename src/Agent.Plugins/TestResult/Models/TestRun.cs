// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestResult.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains the Test results and Test Summary for the Test run
    /// </summary>
    public class TestRun
    {
        /// <summary>
        /// Collection of passed tests
        /// </summary>
        public List<TestResult> PassedTests { get; set; }

        /// <summary>
        /// Collection of failed tests
        /// </summary>
        public List<TestResult> FailedTests { get; set; }
        
        /// <summary>
        /// Summary for the test run
        /// </summary>
        public TestRunSummary TestRunSummary { get; set; }
    }
}
