using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SFA.DAS.Configuration.AzureTableStorage;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.Encoding;
using System.Diagnostics.CodeAnalysis;

namespace SFA.DAS.EmployerFeedback.Jobs.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class AddConfigurationExtensions
    {
        public static void AddConfiguration(this IConfigurationBuilder builder)
        {
            builder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true);

            var config = builder.Build();

            builder.AddAzureTableStorage(options =>
            {
                options.ConfigurationKeys = config["ConfigNames"]?.Split(",") ?? [];
                options.StorageConnectionString = config["ConfigurationStorageConnectionString"];
                options.EnvironmentName = config["EnvironmentName"];
                options.PreFixConfigurationKeys = false;
            });
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services)
        {
            services
                .AddOptions<ApplicationConfiguration>()
                .Configure<IConfiguration>((settings, configuration) =>
                    configuration.Bind(settings));

            services.AddSingleton(s => s.GetRequiredService<IOptions<ApplicationConfiguration>>().Value);

            services.AddSingleton(s =>
            {
                var configuration = s.GetRequiredService<IConfiguration>();
                var dasEncodingConfig = new EncodingConfig { Encodings = [] };
                configuration.GetSection(nameof(dasEncodingConfig.Encodings)).Bind(dasEncodingConfig.Encodings);
                return dasEncodingConfig;
            });

            return services;
        }

        public static IServiceCollection ConfigureFromOptions<TOptions>(this IServiceCollection services, Func<ApplicationConfiguration, TOptions> func)
            where TOptions : class, new()
        {
            services.AddSingleton(s =>
                func(s.GetRequiredService<IOptions<ApplicationConfiguration>>().Value));

            return services;
        }
    }
}