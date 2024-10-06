# Running the sample

For running the sample all you need is to have a mockapi endpoint.

## File structure for this demo

The file structure I use in this example is as follows:

```
aspnetcore-proxy-stringcontent/
    |_ AspNetCore.Proxy/
    |_ ProxyAndSignError/

```

## Clone the repo

```bash
mkdir aspnetcore-proxy-stringcontent
cd aspnetcore-proxy-stringcontent
git clone https://github.com/arale61/ProxyAndSignError.git
cd ProxyAndSignError
```

## MockAPI
You can use [MockApi](https://www.mockapi.io/) for this purpose.

I have created a **Todos** mocked API using defaults with the schema for a **todo** resource as follows:

```json
{
    "createdAt":"2024-10-05T21:58:34.741Z",
    "name":"name 1",
    "description":"description 1",
    "id":"1"
}
```
**MockApi** can generate CRUD operations for you.

## Configure the sample

Open the *appsettings.Development.json* file and enter your **MockAPI** endpoint as the value for **ApiEndpoint** as follows:

```json
"AwsApiSettings":{
    "AccessKey": "AKIARANDOMACESSKEYID",
    "SecretKey": "AWSSECRETACCESSKEYRANDOMSECRETFORTESTING",
    "ApiEndpoint": "https://67025a80bd7c8c1ccd3ea24e.mockapi.io"
}
```

## Running the sample

Just run the sample with **dotnet run** or with favorite IDE.

### Interacting with the proxy
The sample will start an instance of the aspnet core mvc and the AspNetCore.Proxy exposes a **CatchAll** endpoint for **system_api/** calls where it will proxy them to the **AwsApiSettings:ApiEndpoint**.

In this case, it will make use of a custom http client that uses **AwsSignatureVersion4** package for signing the request before sending it according AWS signing process.

### My local configuration

My local configuration is as follows:

- MockAPI endpoint: https://67025a80bd7c8c1ccd3ea24e.mockapi.io/
- ApiCallController: CatchAll controller for system_api/** calls
- local instace running at: http://localhost:5000/

My MockAPI configuration is as follows:

- Api name: Todos
- routes: 
    - /todos
        - GET -> <all todos>
        - POST <new todo>
    - /todos/{id}
        - GET <todo by id>
        - PUT <update todo>
        - DELETE <delete todo>

### Get all Todos

Sample of the call:

```
GET /system_api/todos HTTP/1.1
Host: localhost:5000
User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:109.0) Gecko/20100101 Firefox/115.0
Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8
Accept-Language: en-US,en;q=0.5
Accept-Encoding: gzip, deflate, br
Connection: keep-alive
Upgrade-Insecure-Requests: 1


```

The request that mockapi receives must contain the appropiate aws signature authorization header as shown in the captured sample:

```
GET /todos HTTP/1.1
Accept: text/html, application/xhtml+xml, application/xml; q=0.9, image/avif, image/webp, */*; q=0.8
Connection: keep-alive
Host: localhost:6161
User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:109.0) Gecko/20100101 Firefox/115.0
Accept-Encoding: gzip, deflate, br
Accept-Language: en-US, en; q=0.5
Upgrade-Insecure-Requests: 1
Sec-Fetch-Dest: document
Sec-Fetch-Mode: navigate
Sec-Fetch-Site: none
Sec-Fetch-User: ?1
X-Amz-Date: 20241006T140956Z
Authorization: AWS4-HMAC-SHA256 Credential=AKIARANDOMACESSKEYID/20241006/eu-central-1/execute-api/aws4_request, SignedHeaders=accept;accept-encoding;accept-language;connection;host;sec-fetch-dest;sec-fetch-mode;sec-fetch-site;sec-fetch-user;upgrade-insecure-requests;user-agent;x-amz-date, Signature=a0dcb96ae6c952c6ce870f117759d0f1b7a0acfdd366c4878822f3918ddb12c9
traceparent: 00-24e2c5a4fc2ef8e1ec88f5111d73ab04-ec3d19ae7cb505aa-00
```

The previous sample is captured using a local request and wireshark with a fake local todos endpoint.

For **get a todo** it will work the same way as well for **DELETE** a todo.

### Create a Todo

Sample of the call:

```
POST /system_api/todos HTTP/1.1
Host: localhost:5000
User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:109.0) Gecko/20100101 Firefox/115.0
Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8
Accept-Language: en-US,en;q=0.5
Accept-Encoding: gzip, deflate, br
Content-Type: application/json
Connection: keep-alive
Upgrade-Insecure-Requests: 1
Content-Length: 96

{"createdAt":"2024-10-05T21:59:34.741Z","name":"name 1","description":"description 1","id":"51"}
```

This request will not hit mockapi, it will fail at our aspnet core mvc instance:

```
HTTP/1.1 502 Bad Gateway
Date: Sun, 06 Oct 2024 14:54:39 GMT
Server: Kestrel
Content-Length: 1140

Request could not be proxied.

Specified method is not supported.

   at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpRequestStream.get_Position()
   at AwsSignatureVersion4.Private.ContentHash.CalculateAsync(HttpContent content)
   at AwsSignatureVersion4.Private.Signer.SignAsync(HttpRequestMessage request, Uri baseAddress, IEnumerable`1 defaultRequestHeaders, DateTime now, String regionName, String serviceName, ImmutableCredentials credentials)
   at AwsSignatureVersion4.AwsSignatureHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
   at Microsoft.Extensions.Http.Logging.LoggingScopeHttpMessageHandler.<SendCoreAsync>g__Core|5_0(HttpRequestMessage request, Boolean useAsync, CancellationToken cancellationToken)
   at System.Net.Http.HttpClient.<SendAsync>g__Core|83_0(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationTokenSource cts, Boolean disposeCts, CancellationTokenSource pendingRequestsCts, CancellationToken originalCancellationToken)
   at AspNetCore.Proxy.HttpExtensions.ExecuteHttpProxyOperationAsync(HttpContext context, HttpProxy httpProxy)
```

## Test with StringContent

Clone or download the fork repo:

```bash
cd ../aspnetcore-proxy-stringcontent
git clone https://github.com/arale61/AspNetCore.Proxy.git
cd AspNetCore.Proxy
```

Build the project:

```bash
dotnet build
```

Add the project reference to your aspnet core mvc project:

```bash
cd ../aspnetcore-proxy-stringcontent/ProxyAndSignError
dotnet remove package AspNetCore.Proxy
dotnet add reference ../AspNetCore.Proxy/src/Core/AspNetCore.Proxy.csproj
dotnet build
```

Run the project (inside the ProxyAndSignError folder):

```bash
dotnet run
```

Now the instance uses the forked version.

### Create a Todo

Sample of the call:

```
POST /system_api/todos HTTP/1.1
Host: localhost:5000
User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:109.0) Gecko/20100101 Firefox/115.0
Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8
Accept-Language: en-US,en;q=0.5
Accept-Encoding: gzip, deflate, br
Content-Type: application/json
Connection: keep-alive
Upgrade-Insecure-Requests: 1
Content-Length: 96

{"createdAt":"2024-10-05T21:59:34.741Z","name":"name 1","description":"description 1","id":"51"}
```

And the success response back:

```
HTTP/1.1 201 Created
Content-Length: 98
Connection: keep-alive
Content-Type: application/json
Date: Sun, 06 Oct 2024 15:17:52 GMT
Server: Cowboy
Access-Control-Allow-Headers: X-Requested-With,Content-Type,Cache-Control,access_token
Access-Control-Allow-Methods: GET,PUT,POST,DELETE,OPTIONS
Access-Control-Allow-Origin: *
Via: 1.1 vegur
Report-To: {"group":"heroku-nel","max_age":3600,"endpoints":[{"url":"https://nel.heroku.com/reports?ts=1728227872&sid=1b10b0ff-8a76-4548-befa-353fc6c6c045&s=kYCaQGs%2FWdBp3X1pJdLOyVxk3o1b4Fh1If0j1KH3xPQ%3D"}]}
Reporting-Endpoints: heroku-nel=https://nel.heroku.com/reports?ts=1728227872&sid=1b10b0ff-8a76-4548-befa-353fc6c6c045&s=kYCaQGs%2FWdBp3X1pJdLOyVxk3o1b4Fh1If0j1KH3xPQ%3D
Nel: {"report_to":"heroku-nel","max_age":3600,"success_fraction":0.005,"failure_fraction":0.05,"response_headers":["Via"]}
X-Powered-By: Express

{"createdAt":"2024-10-06T02:20:38.068Z","name":"name 51","description":"description 51","id":"51"}

```

The local endpoint also shows the aws authorization header being added after calculating the content hash and continuing the signing process:

```
POST /todos HTTP/1.1
Accept: text/html, application/xhtml+xml, application/xml; q=0.9, image/avif, image/webp, */*; q=0.8
Connection: keep-alive
Host: localhost:6161
User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:109.0) Gecko/20100101 Firefox/115.0
Accept-Encoding: gzip, deflate, br
Accept-Language: en-US, en; q=0.5
Upgrade-Insecure-Requests: 1
X-Amz-Date: 20241006T163400Z
Authorization: AWS4-HMAC-SHA256 Credential=AKIARANDOMACESSKEYID/20241006/eu-central-1/execute-api/aws4_request, SignedHeaders=accept;accept-encoding;accept-language;connection;host;upgrade-insecure-requests;user-agent;x-amz-date, Signature=a4994dfa5c7c7a43e5450161bd9591dc2f030c4d00e309ec89ae59e22be34ff5
traceparent: 00-9a3d28759626af81bef3a83db745f70d-a978b69288b9b08a-00
Content-Type: text/plain; charset=utf-8
Content-Length: 101

{"createdAt":"2024-10-05T21:59:34.741Z","name":"name 1","description":"description 6767","id":"6767"}
```

In similar ways you can test for the PUT and PATCH methods working.

For testing the API I used **Burp Community** but any http client can do the job.
