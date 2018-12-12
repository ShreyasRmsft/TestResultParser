// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{

    /// <summary>
    /// Manages the test run
    /// </summary>
    public interface ITestRunManager
    {
        /// <summary>
        /// Validates and publishes the test run
        /// </summary>
        /// <param name="testRun"></param>
        Task PublishAsync(TestRun testRun);
    }
}