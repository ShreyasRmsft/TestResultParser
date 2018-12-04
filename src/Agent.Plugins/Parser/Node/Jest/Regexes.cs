// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest
{
    using System.Text.RegularExpressions;

    // TODO: Check if logs come prepended with the time stamp and if so have a definitive regex to ignore them to tighten the patterns
    // TODO: Check if merging all or most of the regexes into a single one gives a perf boost
    // TODO: Verify if tabs (/t) will come from the agent logs

    public class Regexes
    {
        public static Regex VerbosePassedTestCaseMatcher { get; } = new Regex("  (✓)|(√) (.*)");

        public static Regex VerboseFailedTestCaseMatcher { get; } = new Regex("  ((✕)|(×)) (.*)");

        public static Regex FailedTestCaseMatcher { get; } = new Regex("((ΓùÅ)|(●)) ((.* › ){0,1}.*)");

        public static Regex VerboseFailedTestSummaryIndicator { get; } = new Regex("Summary of all failing tests$");

        public static Regex TestsSummaryMatcher { get; } = new Regex("Tests:[ ]+([1-9][0-9]*) (failed|passed|skipped)(.*, ([1-9][0-9]*) (failed|passed)){0,1}");

        // there can be an additonal esitmated time that can be printed hence not using a $
        public static Regex TestRunTimeMatcher { get; } = new Regex("Time:[ ]+([0-9]+(\\.[0-9]+){0,1})(ms|s|m|h)");
    }
}
