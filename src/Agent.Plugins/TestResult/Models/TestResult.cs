﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestResult.Models
{
    using System;

    /// <summary>
    /// Result for the Test case
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Name of the Test case
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Result associated with the Test Case
        /// </summary>
        TestOutcome Outcome { get; set; }

        /// <summary>
        /// Time taken by the test case to run
        /// </summary>
        TimeSpan ExecutionTime { get; set; }
    }
}
