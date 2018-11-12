// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestResult.Models
{
    using System;

    // Represents the summary of the test run
    public class TestRunSummary
    {
        /// <summary>
        /// Total number of tests that are part of the run
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Total number of tests that failed
        /// </summary>
        public int TotalFailed { get; set; }

        /// <summary>
        /// Total number of tests that were passed
        /// </summary>
        public int TotalPassed { get; set; }

        /// <summary>
        /// Total number of tests that were skipped
        /// </summary>
        public int TotalSkipped { get; set; }

        /// <summary>
        /// Total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }
    }
}
