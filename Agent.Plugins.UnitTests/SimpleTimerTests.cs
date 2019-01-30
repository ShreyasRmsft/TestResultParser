// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.UnitTests
{
    using System;
    using System.Threading;
    using Agent.Plugins.Log.TestResultParser.Contracts;
    using Agent.Plugins.Log.TestResultParser.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class SimpleTimerTests
    {
        public TestContext TestContext { get; set; }

        private Mock<ITraceLogger> _logger;
        private Mock<ITelemetryDataCollector> telemetry;

        public SimpleTimerTests()
        {
            // Mock logger to log to console for easy debugging
            this._logger = new Mock<ITraceLogger>();

            this._logger.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Info: {data}"); });
            this._logger.Setup(x => x.Verbose(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Verbose: {data}"); });
            this._logger.Setup(x => x.Error(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Error: {data}"); });
            this._logger.Setup(x => x.Warning(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Warning: {data}"); });
        }

        [TestMethod]
        public void SimpleTimerShouldWarnIfThresholdExceeded()
        {
            using (var simpleTimer = new SimpleTimer("someTimer", "someArea", "someEvent", 1, _logger.Object, telemetry.Object, TimeSpan.FromMilliseconds(1)))
            {
                Thread.Sleep(10);
            }

            _logger.Verify(x => x.Warning(It.IsAny<string>()), Times.Once, "Expected SimpleTimer to have logged warning for time exceeded.");
        }
    }
}
