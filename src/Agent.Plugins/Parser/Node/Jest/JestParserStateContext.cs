using Agent.Plugins.TestResultParser.TestResult.Models;

namespace Agent.Plugins.TestResultParser.Parser.Node.Jest
{
    public class JestParserStateContext : TestResultParserStateContext
    {
        public JestParserStateContext(TestRun testRun)
        {
            Initialize(testRun);
        }

        public override void Initialize(TestRun testRun)
        {
            TestRun = testRun;
        }
    }
}
