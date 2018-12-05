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
        //  ● Console - pattern to ignore for failed tests

        // ○ | Γùï - skipped

        // passed verbose - ΓêÜ

        // verbose failed - ├ù

        public static Regex TestRunStart { get; } = new Regex($"(( FAIL )|( PASS )) (?<{RegexCaptureGroups.TestSourcesFile}>.+)", RegexOptions.ExplicitCapture);

        public static Regex PassedTestCase { get; } = new Regex("  ((✓)|(√)|(ΓêÜ)) (.*)");

        public static Regex FailedTestCase { get; } = new Regex("  ((✕)|(×)|(├ù)) (.*)");

        public static Regex SkippedTestCase { get; } = new Regex("  ((○)|(Γùï)) (.*)");

        public static Regex StackTraceStart { get; } = new Regex("((ΓùÅ)|(●)) ((.* › ){0,1}.*)");

        public static Regex VerboseFailedTestSummaryIndicator { get; } = new Regex("Summary of all failing tests$");

        public static Regex SummaryStart { get; } = new Regex($"Test Suites: .+"); // Should this be made tighter?

        public static Regex TestsSummaryMatcher { get; } = new Regex("Tests:[ ]+([1-9][0-9]*) (failed|passed|skipped)(.*, ([1-9][0-9]*) (failed|passed)){0,1}");

        // there can be an additonal esitmated time that can be printed hence not using a $
        public static Regex TestRunTimeMatcher { get; } = new Regex("Time:[ ]+([0-9]+(\\.[0-9]+){0,1})(ms|s|m|h)");
    }
}
