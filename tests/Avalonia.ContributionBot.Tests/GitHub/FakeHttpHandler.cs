using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Avalonia.ContributionBot.Tests.GitHub;

/// <summary>
/// An HTTP handler answering requests from a list of canned responses, matched on method and path with query,
/// and recording every request it receives.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly List<Route> _routes = [];

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHttpHandler On(
        HttpMethod method,
        string pathAndQuery,
        HttpStatusCode status,
        string? json = null,
        Action<HttpResponseMessage>? configure = null)
    {
        _routes.Add(new Route(method, pathAndQuery, status, json, configure));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var uri = request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");
        Requests.Add(new RecordedRequest(request.Method, uri, body, request.Headers));

        var pathAndQuery = uri.PathAndQuery;
        var route = _routes.FirstOrDefault(r => r.Method == request.Method && r.PathAndQuery == pathAndQuery)
            ?? throw new InvalidOperationException($"Unexpected request {request.Method} {pathAndQuery}");

        var response = new HttpResponseMessage(route.Status) { RequestMessage = request };
        if (route.Json is not null)
            response.Content = new StringContent(route.Json, Encoding.UTF8, "application/json");

        route.Configure?.Invoke(response);
        return response;
    }

    private sealed class Route(
        HttpMethod method,
        string pathAndQuery,
        HttpStatusCode status,
        string? json,
        Action<HttpResponseMessage>? configure)
    {
        public HttpMethod Method { get; } = method;
        public string PathAndQuery { get; } = pathAndQuery;
        public HttpStatusCode Status { get; } = status;
        public string? Json { get; } = json;
        public Action<HttpResponseMessage>? Configure { get; } = configure;
    }

    internal sealed class RecordedRequest(HttpMethod method, Uri uri, string? body, HttpRequestHeaders headers)
    {
        public HttpMethod Method { get; } = method;
        public Uri Uri { get; } = uri;
        public string? Body { get; } = body;
        public HttpRequestHeaders Headers { get; } = headers;

        public JsonElement GetJsonBody()
        {
            Assert.NotNull(Body);
            return JsonElement.Parse(Body);
        }
    }
}
