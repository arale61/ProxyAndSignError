# ProxyAndSignError
Demo for problems while trying to integrate **AspNetProxy.Core** and **AwsSignatureVersion4** packages for __proxy and sign__ requests.

## Libraries trying to integrate
- [AspNetProxy.Core](https://github.com/davidfowl/AspNetProxy)
- [AwsSignatureVersion4](https://github.com/FantasticFiasco/aws-signature-version-4)

## Motivation
Combining **AspNetProxy.Core** with **AwsSignatureVersion4** enables to sign proxied requests.

The solution offers the capability to offer signed proxied requests in a very simple and elegant way, leveraging both libraries.

The problem comes when trying to proxy text content based requests (e.g. POST/PUT/PATCH) and how AspNetProxy.Core creates the
HttpContent of the request as StreamContent combined with how AwsSignatureVersion4 tries to read the content from the StreamContent HttpContent for doing its work.

### AspNetProxy.Core StreamContent
*AspNetCore.Proxy* uses the **AspNetCore.Proxy.Http** extensions class for proxying http requests.

In the **CreateProxiedHttpRequest** extension method is where it creates the corresponding HttpRequestMessage with the HttpContent of the original request.

Check the [code](https://github.com/twitchax/AspNetCore.Proxy/blob/ef2bf59719c167fa71fa3b234fdb6e397886bc4d/src/Core/Extensions/Http.cs#L82):

```csharp
// Write to request content, when necessary.
if (!HttpMethods.IsGet(requestMethod) &&
    !HttpMethods.IsHead(requestMethod) &&
    !HttpMethods.IsDelete(requestMethod) &&
    !HttpMethods.IsTrace(requestMethod))
{
    if (request.HasFormContentType)
    {
        usesStreamContent = false;
        requestMessage.Content = request.Form.ToHttpContent(request);
    }
    else
    {
        requestMessage.Content = new StreamContent(request.Body);
    }
}
```

### AwsSignatureVersion4 Signed Requests
When **AspNetCore.Proxy** calls the SendAsync or similar methods to the configured and injected **AwsSignatureVersion4HttpClient**, it will try to read the content from the *HttpContent* of the request (for content based requests - POST/PUT/PATCH) to calculate the corresponding hash.

See the [Signer class](https://github.com/FantasticFiasco/aws-signature-version-4/blob/c34851814bc5128cc5544958b2db6a6000dbe457/src/Private/Signer.cs#L26)

```csharp
public static async Task<Result> SignAsync(
    HttpRequestMessage request,
    Uri? baseAddress,
    IEnumerable<KeyValuePair<string, IEnumerable<string>>> defaultRequestHeaders,
    DateTime now,
    string regionName,
    string serviceName,
    ImmutableCredentials credentials)
{
    ValidateArguments(request, regionName, serviceName, credentials);

    UpdateRequestUri(request, baseAddress);

    var contentHash = await ContentHash.CalculateAsync(request.Content).ConfigureAwait(false);

    AddHeaders(request, now, serviceName, credentials, contentHash);
/* commented out for brevity */
```

Inside the class responsible for calculating the hash we find the next problem, that combined fire the exception.

Check the [ContentHash class](https://github.com/FantasticFiasco/aws-signature-version-4/blob/master/src/Private/ContentHash.cs):

```csharp
public static async Task<string> CalculateAsync(HttpContent? content)
{
    // Use a hash (digest) function like SHA256 to create a hashed value from the payload
    // in the body of the HTTP or HTTPS request.
    //
    // If the payload is empty, use an empty string as the input to the hash function.
    if (content == null)
    {
        // Per performance reasons, use the pre-computed hash of an empty string from the
        // AWS SDK
        return AWS4Signer.EmptyBodySha256;
    }

    var contentStream = await content.ReadAsStreamAsync().ConfigureAwait(false);

    // Save current stream position
    var currentPosition = contentStream.Position;
    /* commented out for brevity */
```
When the AwsSignatureVersion4 tries to read the content from the HttpContent, and the HttpContent is of **type StreamContent**, seems that the resultant *contentStream* is **readOnly** and **not seekable**, as the AwsSignatureVersion4 tries to read it and then set the position of the stream to 0 again.


For that the AwsSignatureVersion4.AwsSignatureHandler class when using the AwsSignatureVersion4.Private.Signer for trying to sign the request will fire an exception indicating problem reading the content from the HttpContent.

```
HTTP/1.1 502 Bad Gateway
Date: Sun, 06 Oct 2024 09:46:45 GMT
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

## Proposal
While trying to sort out a solution, I wanted to understand the HttpContent class better and how it works internally.

Also I wanted to see alternative versions for reading the content from AwsSignatureVersion4.

When trying to understand if there was something we could do at proxy level I paied attention on how AspNetProxy.Core was trying to proxy
content based requests for json payloads (I have a use case where I kind of proxy several JSON API calls).

As part of the proxy functionality I want to sign the requests using AWS Credentials in certain scenarios.

If the proxied HttpRequestMessage handles its HttpContent as a StringContent, other libraries can re-read the content for its purposes, such as calculating a hash content based. This enables other libraries that leverage custom http clients to do their work if its content based.

Using StringContent provides enough stream features for third party libraries to use the content and good abstraction for handling text based content requests such as "application/json" and "application/xml", but of course, can be extended to more text based content types.

Some details follow:

### HttpRequestMessage and HttpContent

Doing the first I went to HttpRequestMessage class documentation:
https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage.content?view=net-8.0#system-net-http-httprequestmessage-content

```
The contents of an HTTP message corresponds to the entity body defined in RFC 2616.

A number of classes can be used for HTTP content. These include the following.

    ByteArrayContent - HTTP content based on a byte array.

    FormUrlEncodedContent - HTTP content of name/value tuples encoded using application/x-www-form-urlencoded MIME type.

    MultipartContent - HTTP content that gets serialized using the multipart/* content type specification.

    MultipartFormDataContent - HTTP content encoded using the multipart/form-data MIME type.

    StreamContent - HTTP content based on a stream.

    StringContent - HTTP content based on a string.
```

### StreamContent class info

For [StreamContent class](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Net.Http/src/System/Net/Http/StreamContent.cs#L73) we can see:

```csharp
    private Task SerializeToStreamAsyncCore(Stream stream, CancellationToken cancellationToken)
    {
        Debug.Assert(stream != null);
        PrepareContent();
        return StreamToStreamCopy.CopyAsync(
            _content,
            stream,
            _bufferSize,
            !_content.CanSeek, // If the stream can't be re-read, make sure that it gets disposed once it is consumed.
            cancellationToken);
    }
```

Also that the class handles to be [consumed only once](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Net.Http/src/System/Net/Http/StreamContent.cs#L115) (comments are from original source code at the time writing this post):

```csharp
private void PrepareContent()
{
    if (_contentConsumed)
    {
        // If the content needs to be written to a target stream a 2nd time, then the stream must support
        // seeking (e.g. a FileStream), otherwise the stream can't be copied a second time to a target
        // stream (e.g. a NetworkStream).
        if (_content.CanSeek)
        {
            _content.Position = _start;
        }
        else
        {
            throw new InvalidOperationException(SR.net_http_content_stream_already_read);
        }
    }

    _contentConsumed = true;
}
```
And also you can notice the use of **ReadOnlyStream class** found in the same file.

### StringContent class info

For [StringContent class](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.stringcontent?view=net-8.0) we can also check the [source code](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Net.Http/src/System/Net/Http/StringContent.cs).

There we can see that is kind of base http content class for text based http content.
It doesn't pose the same stream limitations for consumption.

### JsonContent class info
Not directly supported in AspNetCore.Proxy, I believe a new dependency needs to be added to have access to it.

Is a specific HttpContent class that leverages on Json types as body using Json Serializers and Deserializers. This I can only see interesting if the proxy needs to deal with some sort of content transformation or validation, which is not my need and seems also not a need for AspNetCore.Proxy. Also this is not a trivial case to solve.

Rather, using StringContent we merely transport the "content as a string".

## Implementation details for proposal
Changes can be seen in my fork at https://github.com/arale61/AspNetCore.Proxy/

The implementation details affect the [Core/Extensions/Http.cs](https://github.com/arale61/AspNetCore.Proxy/blob/master/src/Core/Extensions/Http.cs) file.

The fundamental changes are located in CreateProxiedHttpRequestAsync extension method from the mentioned file, adding the specific case for the text based content requests, in particular, "application/json" and "application/xml" (but could be extended by convenience):

```csharp
private async static Task<HttpRequestMessage> CreateProxiedHttpRequestAsync(this HttpContext context, string uriString, bool shouldAddForwardedHeaders)
{
    var uri = new Uri(uriString);
    var request = context.Request;

    var requestMessage = new HttpRequestMessage();
    var requestMethod = request.Method;
    var usesStreamContent = true; // When using other content types, they specify the Content-Type header, and may also change the Content-Length.

    // Write to request content, when necessary.
    if (!HttpMethods.IsGet(requestMethod) &&
        !HttpMethods.IsHead(requestMethod) &&
        !HttpMethods.IsDelete(requestMethod) &&
        !HttpMethods.IsTrace(requestMethod))
    {
        if (request.HasFormContentType)
        {
            usesStreamContent = false;
            requestMessage.Content = request.Form.ToHttpContent(request);
        }
        else if(IsTextBasedMimeType(request))
        {
            usesStreamContent = false;
            var bodyString = await ReadRequestBodyAsStringAsync(request);
            requestMessage.Content = new StringContent(bodyString, Encoding.UTF8, GetContentType(request));
        }
        else
        {
            requestMessage.Content = new StreamContent(request.Body);
        }
    }

/*...*/
```

As shown in the snippet below, in case of a POST/PUT/PATCH with text based content types, we read the body as string and use it to create the HttpContent for the request message.

There are few auxiliar functions to help on this:

- The **GetContentType** function is used to get the Content-Type header value from the request.
- The **IsTextBasedMimeType** function is used to check if the content type is "application/json" or "application/xml", which means it can be read as a string.
- The **ReadRequestBodyAsStringAsync** function reads the body of the request and returns it as a string. This method forces to make async the *CreateProxiedHttpRequest* function, now **CreateProxiedHttpRequestAsync**.

In the current implementation the auxiliar function **IsTextBasedMimeType** only checks for the following types:


```csharp
private static bool IsTextBasedMimeType(HttpRequest request)
{
    var textBased = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/xml"
    };
    
    return textBased.Contains(GetContentType(request));
}
```

This enables to proxy and sign POST/PUT/PATCH requests with a body of type "application/json" or "application/xml" with combining both libraries.

## Important notes on proposal
I have limited time for this little proposal.

I am interested on the feedback from the community, so please feel free to comment and suggest improvements or correct errors and issues in this document.


## Using the sample

Check the [RUN_SAMPLE.md](./RUN_SAMPLE.md) for instructions.

## Other tools used

- **Burp Community** for testing proxying requests.
- **Wireshark** for monitoring the aspnetcore proxied request messages containing the expected aws signature.
- **json file** for moking a list of todos.
- **python http server** for having a local endpoint to test and monitor.