// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System.Text.RegularExpressions;

    // TODO: Check if logs come prepended with the time stamp and if so have a definitive regex to ignore them to tighten the patterns
    // TODO: Check if mergin all or most of the regexes into a single one gives a perf boost
    public class MochaTestResultParserRegularExpressions
    {
        public static Regex PassedTestCaseMatcher { get; } = new Regex($"  ((ΓêÜ)|✓) (((?<{RegexCaptureGroups.TestCaseName}>.*) \\((?<{RegexCaptureGroups.TestRunTime}>[1-9][0-9]*)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\))|(?<{RegexCaptureGroups.TestCaseName}>.*))", RegexOptions.ExplicitCapture);

        public static Regex FailedTestCaseMatcher { get; } = new Regex($"  (?<{RegexCaptureGroups.FailedTestCaseNumber}>[1-9][0-9]*)\\) (?<{RegexCaptureGroups.TestCaseName}>.*)", RegexOptions.ExplicitCapture);

        public static Regex PendingTestCaseMatcher { get; } = new Regex($"  - (?<{RegexCaptureGroups.TestCaseName}>.*)", RegexOptions.ExplicitCapture);

        public static Regex PassedTestsSummaryMatcher { get; } = new Regex($"  (?<{RegexCaptureGroups.PassedTests}>0|[1-9][0-9]*) passing \\((?<{RegexCaptureGroups.TestRunTime}>[1-9][0-9]*)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\)$", RegexOptions.ExplicitCapture);

        public static Regex FailedTestsSummaryMatcher { get; } = new Regex($"  (?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*) failing$", RegexOptions.ExplicitCapture);

        public static Regex PendingTestsSummaryMatcher { get; } = new Regex($"  (?<{RegexCaptureGroups.PendingTests}>[1-9][0-9]*) pending", RegexOptions.ExplicitCapture);
    }
}
