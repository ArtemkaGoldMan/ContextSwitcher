using System.Net;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Answers every request with whatever <see cref="Respond"/> returns, and keeps the requests.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.Requests.Add(request);
        return Task.FromResult(this.Respond(request));
    }
}
