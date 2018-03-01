using System;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Airlock.Logging;
using Vostok.Clusterclient.Topology;
using Vostok.Commons.Extensions.UnitConvertions;
using Vostok.Logging;
using Vostok.Logging.Logs;

namespace Vostok.Airlock.Client.Tests
{
    [Ignore("Explicit attribute does not work in VS + Resharper")]
    public class Integration_Tests
    {
        private readonly ConsoleLog log = new ConsoleLog();

        [Test]
        public void PushLogEventsToAirlock()
        {
            var routingKey = RoutingKey.Create("vostok", "ci", "core", RoutingKey.LogsSuffix);
            var events = GenerateLogEvens(count: 1000000);
            PushToAirlock(routingKey, events, e => e.Timestamp);
        }

        private static LogEventData[] GenerateLogEvens(int count)
        {
            var utcNow = DateTimeOffset.UtcNow;
            return Enumerable.Range(0, count)
                             .Select(i => new LogEventData
                             {
                                 Message = "Testing AirlockClient" + i,
                                 Level = LogLevel.Debug,
                                 Timestamp = utcNow.AddSeconds(-i*10)
                             }).ToArray();
        }

        private void PushToAirlock<T>(string routingKey, T[] events, Func<T, DateTimeOffset> getTimestamp)
        {
            log.Debug($"Pushing {events.Length} events to airlock");
            var sw = Stopwatch.StartNew();
            IAirlockClient airlockClient;
            using (airlockClient = CreateAirlockClient())
            {
                foreach (var @event in events)
                    airlockClient.Push(routingKey, @event, getTimestamp(@event));
            }

            var lostItems = airlockClient.Counters.LostItems.GetValue();
            var sentItems = airlockClient.Counters.SentItems.GetValue();
            log.Debug($"SentItemsCount: {sentItems}, LostItemsCount: {lostItems}, Elapsed: {sw.Elapsed}");
            lostItems.Should().Be(0);
            sentItems.Should().Be(events.Length);
        }

        private IAirlockClient CreateAirlockClient()
        {
            var airlockConfig = new AirlockConfig
            {
                ApiKey = "UniversalApiKey",
                ClusterProvider = new FixedClusterProvider(new Uri("http://localhost:6306")),
                SendPeriod = TimeSpan.FromSeconds(2),
                SendPeriodCap = TimeSpan.FromMinutes(5),
                RequestTimeout = TimeSpan.FromSeconds(30),
                MaximumRecordSize = 1.Kilobytes(),
                MaximumBatchSizeToSend = 300.Megabytes(),
                MaximumMemoryConsumption = 300.Megabytes()*10,
                InitialPooledBufferSize = 10.Megabytes(),
                InitialPooledBuffersCount = 10,
                EnableTracing = false,
                EnableMetrics = false,
                Parallelism = 10
            };
            return new AirlockClient(airlockConfig, log.FilterByLevel(LogLevel.Warn));
            //return new ParallelAirlockClient(airlockConfig, 10, log.FilterByLevel(LogLevel.Warn));
        }
    }
}