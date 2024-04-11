using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Easey.Job.Controllers;

public static class SkillExtensions
{
    public static bool IsSpecialized(this TypeDto type)
    {
        return type.Name == "Specialized Skill";
    }
}

[ApiController]
[Route("[controller]")]
public class JobController : ControllerBase
{
    protected WebApiClient WebApiClient;
    private IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public JobController(WebApiClient webApiClient, IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
    {
        WebApiClient = webApiClient;
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
    }
    
    [HttpGet]
    [Route("get-all-skills")]
    public async Task<List<string>> GetAllSkills(string description)
    {
        var accessToken = await Login();
        
        WebApiClient.Headers.Add("Authorization", $"Bearer {accessToken}");
        
        var result = await WebApiClient.PostAsync<ExtractSkillFromDocumentInput, ExtractSkillFromDocumentOutput>(
            new Uri("https://emsiservices.com/skills/versions/latest/extract?language=en"),
            new ExtractSkillFromDocumentInput()
            {
                Text = description,
                ConfidenceThreshold = 0.6
            });

        return result.Result.Data.Select(x => x.Skill).Where(x => x.Type.IsSpecialized()).Select(x => x.Name).ToList();
    }


    private async Task<string> Login()
    {
        var cacheKey = "AccessToken";

        if (!_memoryCache.TryGetValue(cacheKey, out string cachedToken))
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", "8di60yedpmoz897d"),
                new KeyValuePair<string, string>("client_secret", "XRmQoX6Z"),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "emsi_open")
            });

            var response = await httpClient.PostAsync("https://auth.emsicloud.com/connect/token", formContent);

            var dto = JsonSerializer.Deserialize<LoginDto>(await response.Content.ReadAsStringAsync());
            
            // var dto = await WebApiClient.PostAsync<LoginInput, LoginDto>(new Uri("https://auth.emsicloud.com/connect/token"), 
            // new LoginInput(){
            //     ClientId = "8di60yedpmoz897d",
            //     ClientSecret = "XRmQoX6Z",
            //     GrantType = "client_credentials",
            //     Scope = "emsi_open"
            // });

            cachedToken = dto.AccessToken;

            // Set cache options
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Keep in cache for this time, reset time if accessed
                .SetSlidingExpiration(TimeSpan.FromMinutes(59));

            // Save data in cache
            _memoryCache.Set(cacheKey, cachedToken, cacheEntryOptions);
        }

        return cachedToken;
    }
    
}

public class LoginInput
{
    [JsonPropertyName("client_id")] 
    public string ClientId { get; set; }
    [JsonPropertyName("client_secret")] 
    public string ClientSecret { get; set; }
    [JsonPropertyName("grant_type")] 
    public string GrantType { get; set; }
    [JsonPropertyName("scope")] 
    public string Scope { get; set; }
}

public class LoginDto
{
    [JsonPropertyName("access_token")] 
    public string AccessToken { get; set; }
}

public class ExtractSkillFromDocumentInput
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
    [JsonPropertyName("confidenceThreshold")]
    public double ConfidenceThreshold { get; set; }
}

public class ExtractSkillFromDocumentOutput
{
    public List<ExtractSkillDataDto> Data { get; set; } = new List<ExtractSkillDataDto>();
}

public class ExtractSkillDataDto
{
    public SkilLDto Skill { get; set; }
    public double Confidence { get; set; }
}

public class SkilLDto
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public string Description { get; set; }
    public TypeDto Type { get; set; }
}

public class TypeDto
{
    public string Id { get; set; }
    public string Name { get; set; }
}



