using JellySearch.Jobs;
using JellySearch.Services;
using Meilisearch;
using Quartz;
using Quartz.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000"); // Listen on every IP
builder.Services.AddControllers();

var meilisearch = new MeilisearchClient(Environment.GetEnvironmentVariable("MEILI_URL"), Environment.GetEnvironmentVariable("MEILI_MASTER_KEY"));
var index = meilisearch.Index("items");

builder.Services.AddSingleton<Meilisearch.Index>(index); // Add Meilisearch index as service

builder.Services.AddSingleton<JellyfinProxyService, JellyfinProxyService>(); // Add proxy service
builder.Services.AddHostedService<JellyfinProxyService>(provider => provider.GetService<JellyfinProxyService>());

var app = builder.Build();

app.MapControllers();

var factory = new StdSchedulerFactory();
var scheduler = await factory.GetScheduler();

await scheduler.Start();

var indexJob = JobBuilder.Create<IndexJob>()
    .WithIdentity("indexJob", "jellysearch")
    .Build();

var indexTrigger = TriggerBuilder.Create()
    .WithIdentity("indexTrigger", "jellysearch")
    .StartNow()
    .Build();


//await scheduler.ScheduleJob(indexJob, indexTrigger);

app.Run();
