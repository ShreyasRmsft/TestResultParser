// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.ParserConfig
{
    using System;
    using Agent.Plugins.TestResultParser.ParserConfig.Interfaces;

    class TestRunnerParserConfiguration : ITestRunnerParserConfiguration
    {
        public string GetSuccessResultPattern()
        {
            throw new NotImplementedException();
        }

        public string GetFailureResultPattern()
        {
            throw new NotImplementedException();
        }

        public string GetTestSummaryPattern()
        {
            throw new NotImplementedException();
        }
    }
}
