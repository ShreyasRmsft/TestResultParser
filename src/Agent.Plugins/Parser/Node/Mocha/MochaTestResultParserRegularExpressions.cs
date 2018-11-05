namespace Agent.Plugins.TestResultParser.Parser.Node.Mocha
{
    using System.Text.RegularExpressions;

    public class MochaTestResultParserRegularExpressions
    {
        // Check if logs come prepended with the time stamp and if so have a definitive regex to ignore them to tighten the patterns

        public static Regex PassedTestCaseMatcher { get; } = new Regex("  (ΓêÜ)|✓ (.*)");

        public static Regex FailedTestCaseMatcher { get; } = new Regex("  ([1-9][0-9]*)\\) (.*)");

        public static Regex PassedTestsSummaryMatcher { get; } = new Regex("  (0|[1-9][0-9]*) passing \\(([1-9][0-9]*)(ms|s|m|h)\\)$");

        public static Regex FailedTestsSummaryMatcher { get; } = new Regex("  (0|[1-9][0-9]*) failing$");
    }
}
