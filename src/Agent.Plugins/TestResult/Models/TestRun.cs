// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestResult.Models
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Contains the Test results and Test Summary for the Test run
    /// </summary>
    public class TestRun
    {
        /// <summary>
        /// All the results associated with the test run 
        /// </summary>
        public Collection<TestResult> TestResults { get; set; }
        
        /// <summary>
        /// Summary for the test run
        /// </summary>
        public TestRunSummary TestRunSummary { get; set; }
    }
}
