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
