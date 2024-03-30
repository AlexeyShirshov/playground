
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Redis.StackExchange;
using learn.masstransit;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

GlobalConfiguration.Configuration.UseRedisStorage();

services.AddHangfire(h =>
{
    h.UseRecommendedSerializerSettings();
    h.UseRedisStorage();
});

services.AddMassTransit(busConfig =>
{
    var currentAssembly = Assembly.GetExecutingAssembly();

    busConfig.AddSagas(currentAssembly);
    busConfig.AddSagaStateMachine<Developer, TheDeveloper>()
        .RedisRepository();

    busConfig.AddRequestClient<IRequestClient<IHowYDoing>>();
    busConfig.AddRequestClient<IRequestClient<IAwakeEvent>>();

    busConfig.UsingInMemory((context, inMemoryConfig) =>
    {
        inMemoryConfig.UseHangfireScheduler();

        inMemoryConfig.ConfigureEndpoints(context);
    });
});

services.AddLogging(config =>
{
    config.SetMinimumLevel(LogLevel.Trace);
    config.AddFilter((ctx, _) => ctx?.StartsWith("Microsoft.AspNetCore.Server.Kestrel") != true
        && ctx?.StartsWith("Microsoft.AspNetCore.Routing.EndpointMiddleware") != true
        && ctx?.StartsWith("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware") != true
        && ctx?.StartsWith("Microsoft.AspNetCore.HostFiltering.HostFilteringMiddleware") != true
        && ctx?.StartsWith("Microsoft.AspNetCore.Routing.Matching.DfaMatcher") != true
    );
    config.AddSimpleConsole(consoleConfig =>
    {
        consoleConfig.SingleLine = true;
        consoleConfig.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
    });
});

var app = builder.Build();

app.MapGet("awake/{id}", async (Guid id, IBus bus, IRequestClient<IAwakeEvent> awake) =>
{
    // await bus.Publish<IAwakeEvent>(new
    // {
    //     Id = id
    // });

    var developer = awake.GetResponse<TheDeveloper>(new
    {
        Id = id
    });

    var options = new JsonSerializerOptions();
    options.Converters.Add(new JsonStringEnumConverter());
    return Results.Json((await developer).Message, options);
});

app.MapGet("newday/{id}", async (Guid id, IBus bus) =>
{
    await bus.Publish<INewDayEvent>(new
    {
        Id = id
    });
});

app.MapGet("howydoing/{id}", async (Guid id, IRequestClient<IHowYDoing> howYDoing) =>
{
    var (developer, _) = await howYDoing.GetResponse<TheDeveloper, UnknownDeveloper>(new
    {
        Id = id
    });

    if (developer.IsCompletedSuccessfully)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return Results.Json((await developer).Message, options);
    }

    return Results.NotFound(id);
});

app.MapGet("fire/{id}", async (Guid id, IBus bus) =>
{
    await bus.Publish<IFire>(new
    {
        Id = id
    });
});

await app.RunAsync();