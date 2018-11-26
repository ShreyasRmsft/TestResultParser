// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Python
{
    public class TelemetryConstants
    {
        public const string EventArea = "Python";

        public const string Initialize = "Initialize";

        public const string ParseException = "ParseException";

        public const string InvalidInput = "InvalidInput";

        public const string SummaryOrFailedTestsNotFound = "SummaryOrFailedTestsNotFound";

        public const string TestRunSummaryCorrupted = "TestRunSummaryCorrupted";

        public const string TestOutcomeSummaryNotFound = "TestOutcomeSummaryNotFound";
    }
}
