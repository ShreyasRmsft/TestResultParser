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
        private Mock<ITelemetryDataCollector> _telemetry;

        public SimpleTimerTests()
        {
            // Mock logger to log to console for easy debugging
            _logger = new Mock<ITraceLogger>();

            _logger.Setup(x => x.Info(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Info: {data}"); });
            _logger.Setup(x => x.Verbose(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Verbose: {data}"); });
            _logger.Setup(x => x.Error(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Error: {data}"); });
            _logger.Setup(x => x.Warning(It.IsAny<string>())).Callback<string>(data => { TestContext.WriteLine($"Warning: {data}"); });

            _telemetry = new Mock<ITelemetryDataCollector>();
        }

        [TestMethod]
        public void SimpleTimerShouldWarnIfThresholdExceeded()
        {
            using (var simpleTimer = new SimpleTimer("someTimer", "someArea", "someEvent", 1, _logger.Object, _telemetry.Object, TimeSpan.FromMilliseconds(1)))
            {
                Thread.Sleep(10);
            }

            _logger.Verify(x => x.Warning(It.IsAny<string>()), Times.Once, "Expected SimpleTimer to have logged warning for time exceeded.");
        }

        [TestMethod]
        public void SimpleTimerShouldntWarnIfThresholdNotExceeded()
        {
            using (var simpleTimer = new SimpleTimer("someTimer", "someArea", "someEvent", 1, _logger.Object, _telemetry.Object, TimeSpan.FromMilliseconds(1000)))
            {
                Thread.Sleep(10);
            }

            _logger.Verify(x => x.Warning(It.IsAny<string>()), Times.Never, "Expected SimpleTimer to have logged warning for time exceeded.");
        }
    }
}
