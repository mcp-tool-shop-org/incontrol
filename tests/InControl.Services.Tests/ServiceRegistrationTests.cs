using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Services.Extensions;
using Xunit;

namespace InControl.Services.Tests;

public class ServiceRegistrationTests
{
    [Theory]
    [InlineData("http://localhost:11434", "http://127.0.0.1:11434")]
    [InlineData("", "http://127.0.0.1:11434")]
    [InlineData("http://127.0.0.1:11500", "http://127.0.0.1:11500")]
    public void ComputeSession_StartsOnTheNumericLoopbackAddress(string configured, string expected)
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        var descriptor = services.Single(d => d.ServiceType == typeof(ComputeSession));

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IOptions<OllamaOptions>)))
            .Returns(Options.Create(new OllamaOptions { BaseUrl = configured }));
        provider.Setup(p => p.GetService(typeof(ISshSessionFactory))).Returns(new Mock<ISshSessionFactory>().Object);
        provider.Setup(p => p.GetService(typeof(ITcpProbe))).Returns(new Mock<ITcpProbe>().Object);
        provider.Setup(p => p.GetService(typeof(IOllamaReadyProbe))).Returns(new Mock<IOllamaReadyProbe>().Object);

        var session = (ComputeSession)descriptor.ImplementationFactory!(provider.Object);

        session.BaseUrl.Should().Be(expected);
    }
}
