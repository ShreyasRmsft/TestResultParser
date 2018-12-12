// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.Log.TestResultParser.Parser
{
    using System.Text.RegularExpressions;

    // TODO: Check if logs come prepended with the time stamp and if so have a definitive regex to ignore them to tighten the patterns
    // TODO: Check if merging all or most of the Regexs into a single one gives a perf boost
    // TODO: Verify if tabs (/t) will come from the agent logs

    public class JestRegexs
    {
        // TODO: Optional time at the end of this line
        /// <summary>
        /// Matches lines with the following regex:
        /// ^(( FAIL )|(FAIL)|( PASS )|(PASS)) (.+)$
        /// </summary>
        public static Regex TestRunStart { get; } = new Regex($"^(( FAIL )|(FAIL)|( PASS )|(PASS)) (?<{RegexCaptureGroups.TestSourcesFile}>.+)$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+((✓)|(√)|(ΓêÜ)) (((.+) \\(([0-9]+)(ms|s|m|h)\\)$)|(.+)$)
        /// </summary>
        public static Regex PassedTestCase { get; } = new Regex($"^(  )+((✓)|(√)|(ΓêÜ)) (((?<{RegexCaptureGroups.TestCaseName}>.+) \\((?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\)$)|(?<{RegexCaptureGroups.TestCaseName}>.+)$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^(  )+((✕)|(×)|(├ù)) (((.+) \\(([0-9]+)(ms|s|m|h)\\)$)|(.+)$)
        /// </summary>
        public static Regex FailedTestCase { get; } = new Regex($"^(  )+((✕)|(×)|(├ù)) (((?<{RegexCaptureGroups.TestCaseName}>.+) \\((?<{RegexCaptureGroups.TestRunTime}>[0-9]+)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)\\)$)|(?<{RegexCaptureGroups.TestCaseName}>.+)$)", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^  ((ΓùÅ)|(●)) (.*)$
        /// </summary>
        public static Regex StackTraceStart { get; } = new Regex($"^  ((ΓùÅ)|(●)) (?<{RegexCaptureGroups.TestCaseName}>.*)$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Test Suites: .+
        /// </summary>
        public static Regex SummaryStart { get; } = new Regex($"^Test Suites: .+$", RegexOptions.ExplicitCapture); // Should this be made tighter?

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Tests:[ ]+(([1-9][0-9]*) (failed), )?(([1-9][0-9]*) (skipped), )?(([1-9][0-9]*) (passed), )?(([1-9][0-9]*) (total))
        /// </summary>
        public static Regex TestsSummaryMatcher { get; } = new Regex($"^Tests:[ ]+((?<{RegexCaptureGroups.FailedTests}>[1-9][0-9]*) (failed), )?((?<{RegexCaptureGroups.SkippedTests}>[1-9][0-9]*) (skipped), )?((?<{RegexCaptureGroups.PassedTests}>[1-9][0-9]*) (passed), )?((?<{RegexCaptureGroups.TotalTests}>[1-9][0-9]*) (total))", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Matches lines with the following regex:
        /// ^Time:( )+([0-9]+(\\.[0-9]+){0,1})(ms|s|m|h)
        /// </summary>
        public static Regex TestRunTimeMatcher { get; } = new Regex($"^Time:( )+(?<{RegexCaptureGroups.TestRunTime}>[0-9]+(\\.[0-9]+)?)(?<{RegexCaptureGroups.TestRunTimeUnit}>ms|s|m|h)", RegexOptions.ExplicitCapture);
        // There can be an additonal esitmated time that can be printed hence not using a $

        /// <summary>
        /// This is only printed when a large number of tests were run
        /// Matches lines with the following regex:
        /// ^Summary of all failing tests$
        /// </summary>
        public static Regex FailedTestsSummaryIndicator { get; } = new Regex($"^Summary of all failing tests$", RegexOptions.ExplicitCapture);
    }
}
