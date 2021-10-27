using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Events;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Weasel.Postgresql;

namespace MartenRepro
{
    class Program
    {
        const string ConnectionString =
            "User ID=test-user;Password=secretpassword1234;Host=localhost;Port=5432;" +
            "Database=test-db;Pooling=true;Connection Lifetime=0;";

        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, _) => cts.Cancel();

            Console.WriteLine("Initialising store...");

            var store = DocumentStore.For(opts => {
                opts.Connection(ConnectionString);
                opts.AutoCreateSchemaObjects = AutoCreate.All;

                opts.Projections.AsyncMode = DaemonMode.HotCold;
                opts.Projections.Add(new TestProjection(), ProjectionLifecycle.Async);
            });

            await using var session = store.OpenSession();
            session.Events.StartStream<TestEntity>(new TestEntity());
            await session.SaveChangesAsync();
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);

            Console.WriteLine("Starting the daemon...");
            var daemon = store.BuildProjectionDaemon();
            
            await daemon.StartAllShards();

            await daemon.WaitForNonStaleData(TimeSpan.FromMinutes(5));
            
        }
    }

    public class TestEntity
    {
        public TestEntity()
        {
            Numbers = new List<int>();
        }

        public List<int> Numbers { get; set; }
    }

    public class TestProjection : IProjection
    {
        public void Apply(IDocumentOperations operations, IReadOnlyList<StreamAction> actions)
        {
            foreach(var action in actions) {
                Console.WriteLine($"StreamEvent has aggregate type {action.AggregateTypeName ?? "Unknown"}");
            }
        }

        public Task ApplyAsync(IDocumentOperations operations,
            IReadOnlyList<StreamAction> actions,
            CancellationToken cancellation)
        {
            foreach(var action in actions) {
                Console.WriteLine($"StreamEvent has aggregate type {action.AggregateTypeName ?? "Unknown"}");
            }

            return Task.CompletedTask;
        }
    }
}