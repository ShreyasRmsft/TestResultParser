// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser
{
    public abstract class ParserStateContextBase
    {
        public bool PassedTestsSummaryEncountered { get; set; }

        public bool FailedTestsSummaryEncountered { get; set; }
    }
}
