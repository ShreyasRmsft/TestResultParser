// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System.Text.RegularExpressions;

    // TODO: Check if logs come prepended with the time stamp and if so have a definitive regex to ignore them to tighten the patterns
    // TODO: Check if merging all or most of the regexes into a single one gives a perf boost
    // TODO: Verify if tabs (/t) will come from the agent logs
    public class MochaTestResultParserRegexes
    {
        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+((ΓêÜ)|✓) (((.+) \\(([0-9]+)(ms|s|m|h)\\)$)|(.+)$)
        /// </summary>
        public static Regex PassedTestCase { get; } = new Regex($"^(  )+((ΓêÜ)|✓) (((?<{RegexCaptureGroups.TestCaseName}>.+) \\((?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\)$)|(?<{RegexCaptureGroups.TestCaseName}>.+)$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+([1-9][0-9]*)\\) (.+$)
        /// </summary>
        public static Regex FailedTestCase { get; } = new Regex($"^(  )+(?<{RegexCaptureGroups.FailedTestCaseNumber}>[1-9][0-9]*)\\) (?<{RegexCaptureGroups.TestCaseName}>.+$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+- (.+$)
        /// </summary>
        public static Regex PendingTestCase { get; } = new Regex($"^(  )+- (?<{RegexCaptureGroups.TestCaseName}>.+$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+(0|[1-9][0-9]*) passing \\(([0-9]+)(ms|s|m|h)\\)$
        /// </summary>
        public static Regex PassedTestsSummary { get; } = new Regex($"^(  )+(?<{RegexCaptureGroups.PassedTests}>0|[1-9][0-9]*) passing \\((?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\)$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+([1-9][0-9]*) failing$
        /// </summary>
        public static Regex FailedTestsSummary { get; } = new Regex($"^(  )+(?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*) failing$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+([1-9][0-9]*) pending$
        /// </summary>
        public static Regex PendingTestsSummary { get; } = new Regex($"^(  )+(?<{RegexCaptureGroups.PendingTests}>[1-9][0-9]*) pending$", RegexOptions.ExplicitCapture);
    }
}
