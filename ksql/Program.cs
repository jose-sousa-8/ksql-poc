using KafkaFlow;
using ksql;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using KafkaFlow.Producers;
using KafkaFlow.Serializer;
using Prometheus;

using AutoOffsetReset = ksqlDB.RestApi.Client.KSql.Query.Options.AutoOffsetReset;

var ksqlDbUrl = @"http://localhost:8088";

var contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
{
    ShouldPluralizeFromItemName = true
};

await using var context = new KSqlDBContext(contextOptions);
var tweetCounter = Metrics.CreateCounter("ksql_poc_n_tweets", "Number of processed tweets.");
using var subscription = context.CreateQueryStream<Tweet>()
    .WithOffsetResetPolicy(AutoOffsetReset.Earliest)
    .Subscribe(tweetMessage =>
    {
        tweetCounter.Inc();
        Console.WriteLine($"{nameof(Tweet)}: {tweetMessage.Id} - {tweetMessage.Message}");
    }, error => { Console.WriteLine($"Exception: {error.Message}"); }, () => Console.WriteLine("Completed"));

var builder = WebApplication.CreateBuilder(args);

const string topicName = "Tweet";

builder.Services.AddKafka(kafkaConfigurationBuilder =>
{
    kafkaConfigurationBuilder.UseConsoleLog()
        .AddCluster(
            cluster => cluster
                .WithBrokers(new[] { "127.0.0.1:9092" })
                .CreateTopicIfNotExists(topicName, 1, 1)
                .AddProducer<Tweet>(
                    producer => producer
                        .DefaultTopic(topicName)
                        .AddMiddlewares(m =>
                            m.AddSerializer<JsonCoreSerializer>()
                        )
                )
        );
});

var app = builder.Build();

app.UseMetricServer();
app.UseHttpMetrics(options => options.CaptureMetricsUrl = false);

var producer = app.Services
    .GetRequiredService<IProducerAccessor>()
    .GetProducer<Tweet>();

app.MapGet("/", () => "Hello World!");
app.MapPost("/tweet", async (Tweet tweet) =>
{
    var deliveryResult = await producer.ProduceAsync(Guid.NewGuid().ToString(), tweet);
    return deliveryResult.Status.ToString();
});

app.Run();