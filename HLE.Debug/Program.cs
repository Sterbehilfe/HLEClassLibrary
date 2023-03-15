﻿using System;
using BenchmarkDotNet.Attributes;
using HLE.Twitch;
using HLE.Twitch.Chatterino;

namespace HLE.Debug;

public static class Program
{
    private static void Main()
    {
        ManualConfig config = new()
        {
            SummaryStyle = new(default, true, SizeUnit.B, TimeUnit.GetBestTimeUnit())
        };
        config.AddLogger(ConsoleLogger.Default);
        config.AddColumn(TargetMethodColumn.Method, StatisticColumn.Mean, StatisticColumn.StdDev);
        config.AddColumnProvider(DefaultColumnProviders.Metrics, DefaultColumnProviders.Params);
        BenchmarkRunner.Run<Bench>(config);
    }
}

/*
ManualConfig config = new()
{
    SummaryStyle = new(default, true, SizeUnit.B, TimeUnit.GetBestTimeUnit())
};
config.AddLogger(ConsoleLogger.Default);
config.AddColumn(TargetMethodColumn.Method, StatisticColumn.Mean, StatisticColumn.StdDev);
config.AddColumnProvider(DefaultColumnProviders.Metrics, DefaultColumnProviders.Params);
BenchmarkRunner.Run<Bench>(config);
*/

[MemoryDiagnoser]
public class Bench
{
}
