// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.ParserConfig
{
    using System;
    using System.Collections.Generic;
    using Agent.Plugins.TestResultParser.ParserConfig.Interfaces;

    class TestRunnerParserCache : ITestRunnerParserCache
    {
        public IEnumerable<ITestRunnerParserConfiguration> GetConfigurations()
        {
            throw new NotImplementedException();
        }
    }
}
