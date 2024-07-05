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

var factory = new StdSchedulerFactory();
var scheduler = await factory.GetScheduler();

builder.Services.AddSingleton<IScheduler>(scheduler); // Add Quartz scheduler as service

var app = builder.Build();

app.MapControllers();

await scheduler.Start();

var indexJobData = new JobDataMap
{
    { "index", index },
    { "logFactory", app.Services.GetRequiredService<ILoggerFactory>() },
};


var indexJob = JobBuilder.Create<IndexJob>()
    .WithIdentity("indexJob")
    .UsingJobData(indexJobData)
    .StoreDurably()
    .DisallowConcurrentExecution()
    .Build();

var indexCron = Environment.GetEnvironmentVariable("INDEX_CRON");

if (indexCron != null)
{
    var indexTrigger = TriggerBuilder.Create()
        .WithIdentity("indexTrigger")
        .WithCronSchedule(indexCron)
        .Build();

    await scheduler.ScheduleJob(indexJob, indexTrigger); // Schedule job with the given cron string
}
else
{
    await scheduler.AddJob(indexJob, true); // Add job but don't schedule
}

await scheduler.TriggerJob(new JobKey("indexJob")); // Run sync on start

app.Run();
