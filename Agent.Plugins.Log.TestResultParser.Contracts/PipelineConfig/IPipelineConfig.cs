﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Agent.Plugins.Log.TestResultParser.Contracts
{
    public interface IPipelineConfig
    {
        Guid Project { get; set; }
        int BuildId { get; set; }
        String StageName { get; set; }
        String PhaseName { get; set; }
        String JobName { get; set; }
        int StageAttempt { get; set; }
        int PhaseAttempt { get; set; }        
        int JobAttempt { get; set; }
    }
}
