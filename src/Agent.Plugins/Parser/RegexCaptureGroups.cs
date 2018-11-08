// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser
{
    public class RegexCaptureGroups
    {
        public static string TestCaseName { get; } = "TestCaseName";

        public static string FailedTestCaseNumber { get; } = "FailedTestCaseNumber";

        public static string PassedTests { get; } = "PassedTests";

        public static string FailedTests { get; } = "FailedTests";

        public static string TestRunTime { get; } = "TestRunTime";

        public static string TestRunTimeUnit { get; } = "TestRunTimeUnit";
    }
}
