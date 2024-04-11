using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Easey.Job;

public class WebApiWrapper<T>
{
    public T Result { get; set; }
    public string TargetUrl { get; set; }
    public bool Success { get; set; }
}

public class WebApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient _httpClient = null;
    public Dictionary<string, string> Headers { get; set; }
    public JsonSerializerOptions Options { get; set; }
    public ILogger<WebApiClient> Logger { get; set; }
    
    public WebApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        Headers = new Dictionary<string, string>();
        Logger = NullLogger<WebApiClient>.Instance;
        Options = null;
    }
    
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private void AddHeaders()
    {
        foreach (var header in Headers)
        {
            if (_httpClient.DefaultRequestHeaders.Contains(header.Key))
            {
                if(header.Key == "Authorization")
                {
                    _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(header.Value);
                }
                else
                {
                    _httpClient.DefaultRequestHeaders.Remove(header.Key);
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
                
        }
    }
    
    public async Task<WebApiWrapper<T>> GetAsync<T>(Uri requestUrl)
    {
        GetClient(requestUrl.AbsoluteUri);
        
        AddHeaders(); 
        
        var result = await _httpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
        
        var output = await DeserializeAsync<T>(result);

        return output;
    }
    
    public async Task PostAsync<T>(Uri requestUrl, T content)
    {
        GetClient(requestUrl.AbsoluteUri);
        
        AddHeaders();
        
        using var httpContent = await JsonSerializeAsStream(content);

        var result = await _httpClient.PostAsync(requestUrl, httpContent);

        result.EnsureSuccessStatusCode();
    }
    
    public async Task<WebApiWrapper<TOutput>> PostAsync<TInput, TOutput>(Uri requestUrl, TInput content, bool ignoreResponse = false)
    {
        GetClient(requestUrl.AbsoluteUri);
        
        AddHeaders();
#if DEBUG
        var contentString = JsonSerializer.Serialize(content, Options ?? SerializerOptions);
#endif
        using var httpContent = await JsonSerializeAsStream(content);

        var result = await _httpClient.PostAsync(requestUrl, httpContent);

        if(ignoreResponse)
        {
            return null;
        }
        
        var output = await DeserializeAsync<TOutput>(result);

        return output;
    }

    private async Task<StreamContent> JsonSerializeAsStream<T>(T obj)
    {
        //memory stream disposal will be handled by streamcontent. 
        var modelStream = new MemoryStream();

        var httpContent = new StreamContent(modelStream);

        await JsonSerializer.SerializeAsync(modelStream, obj, Options ?? SerializerOptions);
        modelStream.Position = 0;
        modelStream.Seek(0, SeekOrigin.Begin);
        await modelStream.FlushAsync();

        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return httpContent;
    }
    
    private async Task<WebApiWrapper<T>> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await response.Content.LoadIntoBufferAsync(); // load into buffer to prevent stream already consumed exception
        var contentStream = await response.Content.ReadAsStreamAsync();
        try
        {
            response.EnsureSuccessStatusCode();
            var result = await JsonSerializer.DeserializeAsync<T>(contentStream, Options ?? SerializerOptions);
            
            var resultWrapper = new WebApiWrapper<T>()
            {
                Result = result,
                Success = true
            };
            return resultWrapper;
        }
        catch (Exception ex)
        {
            var error = await response.Content.ReadAsStringAsync();
            Logger.LogError(ex, error);
            return new WebApiWrapper<T>()
            {
                Success = false
            };
        }
    }
    
    private void GetClient(string uri)
    {
        if (_httpClient != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new SystemException("Invalid RemoteServiceBaseUrl Value");
        }

        _httpClient = _httpClientFactory.CreateClient(uri);
    }
}