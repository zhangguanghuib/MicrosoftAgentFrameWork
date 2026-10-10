
using Azure.Core;
using Azure.Identity;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI;

namespace Shared;

public static class ClientHelper
{
    public static OpenAIClient GetOpenAIClient(bool showRawCall, RawCallOptions? rawCall = null)
    {
        string apiKey = SecretsManager.GetOpenAICredentials();
        if (!showRawCall)
        {
            return new OpenAIClient(apiKey);
        }

        return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(new CustomClientHttpHandler(rawCall ?? new RawCallOptions())))
        });
    }

    public static OpenAIClient GetAzureOpenAIClient(bool showRawCall = false, RawCallOptions? rawCall = null)
        => GetAzureOpenAIClient(
            new Uri(SecretsManager.GetSecrets().AzureOpenAiEndpoint),
            showRawCall,
            rawCall);

    public static OpenAIClient GetAzureOpenAIClient(
        Uri endpoint,
        bool showRawCall = false,
        RawCallOptions? rawCall = null)
    {
        endpoint = GetOpenAICompatibleEndpoint(endpoint);
        OpenAIClientOptions options = new()
        {
            Endpoint = endpoint
        };

        if (showRawCall)
        {
            options.Transport = new HttpClientPipelineTransport(
                new HttpClient(new CustomClientHttpHandler(rawCall ?? new RawCallOptions())));
        }

        Secrets secrets = SecretsManager.GetSecrets();
        if (!string.IsNullOrWhiteSpace(secrets.AzureOpenAiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(secrets.AzureOpenAiKey), options);
        }

        string scope = endpoint.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
            ? "https://ai.azure.com/.default"
            : "https://cognitiveservices.azure.com/.default";

        DefaultAzureCredentialOptions credentialOptions = new();
        if (!string.IsNullOrWhiteSpace(secrets.AzureTenantId))
        {
            credentialOptions.TenantId = secrets.AzureTenantId;
        }

        BearerTokenPolicy authenticationPolicy = new(
            new AzureTokenProvider(new DefaultAzureCredential(credentialOptions), scope),
            scope);

#pragma warning disable OPENAI001
        return new OpenAIClient(authenticationPolicy, options);
#pragma warning restore OPENAI001
    }

    private static Uri GetOpenAICompatibleEndpoint(Uri endpoint)
    {
        const string responsesPath = "/responses";
        const string openAIV1Path = "/openai/v1";

        string normalizedEndpoint = endpoint.AbsoluteUri.TrimEnd('/');
        if (normalizedEndpoint.EndsWith(responsesPath, StringComparison.OrdinalIgnoreCase))
        {
            normalizedEndpoint = normalizedEndpoint[..^responsesPath.Length];
        }

        if (!normalizedEndpoint.EndsWith(openAIV1Path, StringComparison.OrdinalIgnoreCase))
        {
            normalizedEndpoint += openAIV1Path;
        }

        return new Uri(normalizedEndpoint + "/");
    }

    public class RawCallOptions
    {
        public bool ShowUrl { get; set; }
        public bool ShowRequest { get; set; } = true;
        public bool ShowResponse { get; set; }
    }

    class CustomClientHttpHandler(RawCallOptions rawCallOptions) : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string requestString = await request.Content?.ReadAsStringAsync(cancellationToken)!;
            if (rawCallOptions.ShowUrl)
            {
                Utils.Green($"Raw Request ({request.RequestUri})");
            }

            if (rawCallOptions.ShowRequest)
            {
                Utils.Gray(MakePretty(requestString));
                Utils.Separator();
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (rawCallOptions.ShowResponse)
            {
                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                Utils.Green("Raw Response");
                Utils.Gray(MakePretty(responseString));
                Utils.Separator();
            }

            return response;
        }

        private string MakePretty(string input)
        {
            try
            {
                JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(input);
                return JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return input;
            }
        }
    }

    private sealed class AzureTokenProvider(TokenCredential credential, string scope) : AuthenticationTokenProvider
    {
        public override GetTokenOptions CreateTokenOptions(IReadOnlyDictionary<string, object> properties)
            => new(properties);

        public override AuthenticationToken GetToken(GetTokenOptions options, CancellationToken cancellationToken)
        {
            AccessToken token = credential.GetToken(new TokenRequestContext([scope]), cancellationToken);
            return new AuthenticationToken(token.Token, "Bearer", token.ExpiresOn, null);
        }

        public override async ValueTask<AuthenticationToken> GetTokenAsync(
            GetTokenOptions options,
            CancellationToken cancellationToken)
        {
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext([scope]),
                cancellationToken);
            return new AuthenticationToken(token.Token, "Bearer", token.ExpiresOn, null);
        }
    }

}