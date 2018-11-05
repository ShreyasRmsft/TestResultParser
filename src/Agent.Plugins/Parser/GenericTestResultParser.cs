namespace Agent.Plugins.TestResultParser.Parser
{
    using System;
    using Agent.Plugins.TestResultParser.Parser.Interfaces;
    using Agent.Plugins.TestResultParser.Parser.Models;

    public class GenericTestResultParser : ITestResultParser
    {
        public void Parse(LogLineData line)
        {
            throw new NotImplementedException();
        }
    }
}
