// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    /// <summary>
    /// Result for the Test case
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Name of the Test case
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Result associated with the Test Case
        /// </summary>
        public TestOutcome Outcome { get; set; }

        /// <summary>
        /// Time taken by the test case to run
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
    }
}
