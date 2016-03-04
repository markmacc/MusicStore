using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using E2ETests.Common;
using Microsoft.AspNetCore.Server.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests
{
    // Uses ports ranging 5050 - 5060.
    public class NtlmAuthenticationTests : IDisposable
    {
        private readonly XunitLogger _logger;

        public NtlmAuthenticationTests(ITestOutputHelper output)
        {
            _logger = new XunitLogger(output, LogLevel.Information);
        }

        [ConditionalTheory, Trait("E2Etests", "E2Etests")]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [InlineData(ServerType.WebListener, RuntimeFlavor.CoreClr, RuntimeArchitecture.x64, "http://localhost:5050/")]
        public async Task NtlmAuthenticationTest(ServerType serverType, RuntimeFlavor runtimeFlavor, RuntimeArchitecture architecture, string applicationBaseUrl)
        {
            using (_logger.BeginScope("NtlmAuthenticationTest"))
            {
                var musicStoreDbName = DbUtils.GetUniqueName();

                var deploymentParameters = new DeploymentParameters(Helpers.GetApplicationPath(), serverType, runtimeFlavor, architecture)
                {
                    PublishApplicationBeforeDeployment = true,
                    PublishTargetFramework = runtimeFlavor == RuntimeFlavor.Clr ? "dnx451" : "netstandardapp1.5",
                    ApplicationBaseUriHint = applicationBaseUrl,
                    EnvironmentName = "NtlmAuthentication", //Will pick the Start class named 'StartupNtlmAuthentication'
                    ApplicationHostConfigTemplateContent = (serverType == ServerType.IISExpress) ? File.ReadAllText("NtlmAuthentation.config") : null,
                    SiteName = "MusicStoreNtlmAuthentication", //This is configured in the NtlmAuthentication.config
                    UserAdditionalCleanup = parameters =>
                    {
                        DbUtils.DropDatabase(musicStoreDbName, _logger);
                    }
                };

                // Override the connection strings using environment based configuration
                deploymentParameters.EnvironmentVariables
                    .Add(new KeyValuePair<string, string>(
                        MusicStore.StoreConfig.ConnectionStringKey,
                        DbUtils.CreateConnectionString(musicStoreDbName)));

                using (var deployer = ApplicationDeployerFactory.Create(deploymentParameters, _logger))
                {
                    var deploymentResult = deployer.Deploy();
                    var httpClientHandler = new HttpClientHandler() { UseDefaultCredentials = true, AllowAutoRedirect = false };
                    var httpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri(deploymentResult.ApplicationBaseUri) };

                    // Request to base address and check if various parts of the body are rendered & measure the cold startup time.
                    var response = await RetryHelper.RetryRequest(async () =>
                    {
                        return await httpClient.GetAsync(string.Empty);
                    }, logger: _logger, cancellationToken: deploymentResult.HostShutdownToken);

                    Assert.False(response == null, "Response object is null because the client could not " +
                        "connect to the server after multiple retries");

                    var validator = new Validator(httpClient, httpClientHandler, _logger, deploymentResult);
                    await validator.VerifyNtlmHomePage(response);

                    //Should be able to access the store as the Startup adds necessary permissions for the current user
                    await validator.AccessStoreWithPermissions();

                    _logger.LogInformation("Variation completed successfully.");
                }
            }
        }

        public void Dispose()
        {
            _logger.Dispose();
        }
    }
}
