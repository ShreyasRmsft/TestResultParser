// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public interface ITestResultParser
    {
        /// <summary>
        /// Parse task output line by line to detect the test result
        /// </summary>
        /// <param name="line">Data to be parsed.</param>
        void Parse(LogData line);

        /// <summary>
        /// Name of the parser
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Version number of the parser
        /// </summary>
        string Version { get; }
    }
}
