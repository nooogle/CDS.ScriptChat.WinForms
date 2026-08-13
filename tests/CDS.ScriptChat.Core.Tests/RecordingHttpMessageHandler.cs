using System.Net;
using System.Text;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every request it sees and replays scripted
/// responses, so the real OpenAI SDK's request/response wire format can be exercised without a
/// network call or a real API key.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<string> _responseBodies;

    public RecordingHttpMessageHandler(params string[] responseBodies)
    {
        _responseBodies = new Queue<string>(responseBodies);
    }

    /// <summary>Gets the JSON body of each request sent, oldest first.</summary>
    public List<string> RequestBodies { get; } = [];

    /// <summary>Gets the URI of each request sent, oldest first.</summary>
    public List<Uri?> RequestUris { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri);

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        RequestBodies.Add(body);

        if (_responseBodies.Count == 0)
        {
            throw new InvalidOperationException(
                "RecordingHttpMessageHandler ran out of scripted responses — the code under test made more calls than expected.");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBodies.Dequeue(), Encoding.UTF8, "application/json"),
        };
    }
}
