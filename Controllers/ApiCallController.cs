using System.Reflection.Metadata;
using AspNetCore.Proxy;
using AspNetCore.Proxy.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;


namespace ProxyAndSignError.Controllers;


[ApiController]
public class SystemApiCallController : ControllerBase
{
    // TODO: Add your own mockapi base address here:
    private readonly string _awsUrl;
    private readonly ILogger<SystemApiCallController> _logger;
    private HttpProxyOptions _httpOptions = HttpProxyOptionsBuilder.Instance
        .WithShouldAddForwardedHeaders(false)
        .WithHttpClientName("SystemAwsClient")
        .Build();

    public SystemApiCallController(IOptionsMonitor<AwsApiSettings> awsApiSettings, ILogger<SystemApiCallController> logger)
    {
        _logger = logger;
        _awsUrl = awsApiSettings.CurrentValue.ApiEndpoint;
    }

    /**
    * ProxyCatchAll is the main entry point for proxying requests to the API
    * It takes all the path and query string parameters and appends them to the URL
    * It then uses the AspNetCore.Proxy library to proxy the request to the API
    **/
    [HttpGet]
    [HttpPost]
    [HttpPut]
    [HttpDelete]
    [HttpPatch]
    [Route("system_api/{**rest}")]
    public Task ProxyCatchAll(string rest)
    {
        var queryString = this.Request.QueryString.Value;
        // api url for testing
        var url = $"{_awsUrl}/{rest}{queryString}";

        _logger.LogInformation($"Proxying authorized request to: {url}");
        return this.HttpProxyAsync(url, _httpOptions);
    }
}
