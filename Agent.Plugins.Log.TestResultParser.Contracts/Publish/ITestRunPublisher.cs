// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    /// <summary>
    /// Interface for the publishing the TestRun Data
    /// </summary>
    public interface ITestRunPublisher
    {
        /// <summary>
        /// Publishs the given the test run
        /// </summary>
        /// <param name="testRun">Test run to be published</param>
        /// <returns>Test run id of the published test run</returns>
        Task<int> PublishAsync(TestRun testRun);
    }
}
