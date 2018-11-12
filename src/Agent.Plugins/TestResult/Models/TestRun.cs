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
        /// Name of the parser class that parsed the test run
        /// </summary>
        public string Parser { get; set; }

        /// <summary>
        /// Test run id relative to the parser. This along with the parser name will uniquely identify a run
        /// </summary>
        public int TestRunId { get; set; }

        /// <summary>
        /// Collection of passed tests
        /// </summary>
        public List<TestResult> PassedTests { get; set; }

        /// <summary>
        /// Collection of failed tests
        /// </summary>
        public List<TestResult> FailedTests { get; set; }

        /// <summary>
        /// Collection of skipped tests
        /// </summary>
        public List<TestResult> SkippedTests { get; set; }
        
        /// <summary>
        /// Summary for the test run
        /// </summary>
        public TestRunSummary TestRunSummary { get; set; }
    }
}
