using Microsoft.Extensions.DependencyInjection;
using RestEase.HttpClientFactory;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.Encoding;
using SFA.DAS.Http.Configuration;
using SFA.DAS.Http.MessageHandlers;
using System.Diagnostics.CodeAnalysis;

namespace SFA.DAS.EmployerFeedback.Jobs.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class AddServiceRegistrationExtensions
    {
        public static IServiceCollection AddOuterApi(this IServiceCollection services)
        {
            services.AddScoped<DefaultHeadersHandler>();
            services.AddScoped<LoggingMessageHandler>();
            services.AddScoped<ApimHeadersHandler>();

            var configuration = services
                .BuildServiceProvider()
                .GetRequiredService<EmployerFeedbackOuterApiConfiguration>();

            services
                .AddRestEaseClient<IEmployerFeedbackOuterApi>(configuration.ApiBaseUrl)
                .AddHttpMessageHandler<DefaultHeadersHandler>()
                .AddHttpMessageHandler<ApimHeadersHandler>()
                .AddHttpMessageHandler<LoggingMessageHandler>();

            services.AddTransient<IApimClientConfiguration>((_) => configuration);

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddTransient<IEncodingService, EncodingService>();
            return services;
        }
    }
}
