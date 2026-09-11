using Monitra.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<AggregationWorker>();

var host = builder.Build();
host.Run();
