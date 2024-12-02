using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Data;
using SearchService.Models;
using SearchService.Services;
using System.Net;
using MassTransit;
using Polly;
using Polly.Extensions.Http;
using SearchService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddHttpClient<AuctionSvcHttpClient>().AddPolicyHandler(GetPolicy());

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumersFromNamespaceContaining<AuctionCreatedConsumer>();

    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("search", false));

    x.UsingRabbitMq((context, cfg) => 
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", host =>
        {
            host.Username(builder.Configuration.GetValue("RabbitMq:Username", "guest"));
            host.Password(builder.Configuration.GetValue("RabbitMq:Password", "guest"));
        });

        cfg.ReceiveEndpoint("search-auction-created", e =>
        {
            e.UseMessageRetry(r => r.Intervals(5,5));

            e.ConfigureConsumer<AuctionCreatedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseAuthorization();

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
{
    await DbInitializer.InitDb(app);
}
catch (Exception e)
{
    
    Console.WriteLine(e);
}
});


app.Run();

static IAsyncPolicy<HttpResponseMessage> GetPolicy() 
     => HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)       
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(3));


