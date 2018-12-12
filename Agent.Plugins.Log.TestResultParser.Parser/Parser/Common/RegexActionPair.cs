// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System;
    using System.Text.RegularExpressions;

    public delegate Enum MatchAction(Match match, AbstractParserStateContext stateContext);

    public class RegexActionPair
    {
        public RegexActionPair(Regex regex, MatchAction matchAction)
        {
            Regex = regex;
            MatchAction = matchAction;
        }

        public Regex Regex { get; }

        public MatchAction MatchAction { get; }
    }
}
