using DevOpsProject.HiveMind.API.DI;
using DevOpsProject.HiveMind.API.Middleware;
using DevOpsProject.Shared.Clients;
using DevOpsProject.Shared.Configuration;
using Polly;
using Polly.Extensions.Http;
using Serilog;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, services, loggerConfig) =>
            loggerConfig.ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext());

        // TODO: double check following approach
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHiveMindLogic();

        builder.Services.Configure<HiveCommunicationConfig>(builder.Configuration.GetSection("CommunicationConfiguration"));

        var communicationControlRetryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        builder.Services.AddHttpClient<HiveMindHttpClient>()
            .AddPolicyHandler(communicationControlRetryPolicy);

        string corsPolicyName = "HiveMindCorsPolicy";
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: corsPolicyName,
                policy =>
                {
                    policy.AllowAnyOrigin() //SECURITY WARNING ! Never allow all origins
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });

        builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();
        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseExceptionHandler();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(corsPolicyName);

        //app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}