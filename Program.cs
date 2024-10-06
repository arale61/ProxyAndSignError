using Amazon.Runtime;
using AspNetCore.Proxy;
using AwsSignatureVersion4;
using ProxyAndSignError.Controllers;

// using ProxyAndSignError.Config;

var builder = WebApplication.CreateBuilder(args);

var awsApiSettings = new AwsApiSettings();
builder.Configuration.GetSection("AwsApiSettings").Bind(awsApiSettings);

var accessKeyDevelopment = awsApiSettings.AccessKey; // TODO: remove, only for demo
var accessKeySecretDevelopment = awsApiSettings.SecretKey; // TODO: remove, only for demo
var credentials = new ImmutableCredentials(accessKeyDevelopment, accessKeySecretDevelopment, null); // TODO: remove, only for demo
var aws_region = "eu-central-1"; // TODO: no hardcoding
var aws_service = "execute-api"; // TODO: no hardcoding

builder.Services.Configure<AwsApiSettings>(builder.Configuration.GetSection("AwsApiSettings"));

// Don't specify credentials in source code, this is for demo only! See next chapter for more
// information.
// TODO: Check how to gather credentials for docker containers running in app runner.
builder.Services
    .AddTransient<AwsSignatureHandler>()
    .AddTransient(_ => new AwsSignatureHandlerSettings(aws_region, aws_service, credentials));

builder.Services
    .AddHttpClient("SystemAwsClient")
    .AddHttpMessageHandler<AwsSignatureHandler>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddProxies();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
