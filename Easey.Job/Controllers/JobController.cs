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
    [Route("get-all-specialized-skills")]
    public async Task<List<string>> GetAllSpecializedSkills(string description)
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
    
    [HttpGet]
    [Route("get-all-common-skills")]
    public async Task<List<string>> GetAllCommonSkills(string description)
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

        return result.Result.Data.Select(x => x.Skill).Where(x => !x.Type.IsSpecialized()).Select(x => x.Name).ToList();
    }

    [HttpGet]
    [Route("test-prompt")]
    public async Task<string> TestPrompt(string prompt)
    {
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri("http://localhost:11434/api/generate"),
            new OllamaGenerateInput()
            {
                Prompt = prompt
            });

        return result.Result.Response;
    }

    [HttpPost]
    [Route("improve-resume")]
    public async Task<string> ImproveResume(string employmentHistory, string jobDescription)
    {
        var systemMd = @"
# IDENTITY AND GOALS

You are an expert recruitment consultant. You specialize in assessing the quality of resume and determine how good of a match to a job description.

You are able to improve the resume to match the job description based on your knowledge.

Take a step back and think step by step about how to accomplish this task using the steps below.

# STEPS

- Included in the input should be job description and resume text, which are telling the AI what to do to generate the output. 

- Think deeply and read through the job description and resume and what they're trying to describe.

- Also included in the input should be the AI's output that was created from that prompt.

- Deeply analyze the job description and resume and provide the improved version of the employment history.

## JOB DESCRIPTION
{{jobDescription}}

## EMPLOYMENT HISTORY
{{employmentHistory}}

# OUTPUT

Output 3 improved version of the employment history in array inside a json object.

output only the json object

## EXAMPLE
{ 'suggestion': [
    'improved 1...', 'improved 2...', 'improved 3...'
]}


";
        systemMd = systemMd.Replace("{{jobDescription}}", jobDescription);
        systemMd = systemMd.Replace("{{employmentHistory}}", employmentHistory);
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri("http://localhost:11434/api/generate"),
            new OllamaGenerateInput()
            {
                Prompt = systemMd
            });

        return result.Result.Response;
    }

    [HttpPost]
    [Route("get-resume-summary")]
    public async Task<string> GetResumeSummary(OllamaGetResumeSummaryInput input)
    {
        var systemMd = @"
# IDENTITY and PURPOSE

You are an expert content summarizer. You take content in with is the resume employment history and user skills and provide a summary.

Take a deep breath and think step by step about how to best accomplish this goal using the following steps.

# STEPS

- Included in the input should be employment history and user skills, which are telling the AI what to do to generate the output. 

- Think deeply and read through the employment history and user skills and what they're trying to describe.

- Also included in the input should be the AI's output that was created from that prompt.

- Deeply analyze the employment history and user skills and provide the 3 different variation of the summary from first person point of view.

## EMPLOYMENT HISTORY
{{employmentHistory}}

## SKILLS
{{userSkills}}

# OUTPUT

Output the 3 version of summary in a json object with parameter name 'result' as an array containing the 3 summary

output only the json object

## EXAMPLE
{ 'result': [
'summary 1...', 'summary 2...', 'summary 3...'
]}


";
        
        systemMd = systemMd.Replace("{{employmentHistory}}", input.EmploymentHistory);
        systemMd = systemMd.Replace("{{userSkills}}", input.UserSkills);
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri("http://localhost:11434/api/generate"),
            new OllamaGenerateInput()
            {
                Prompt = systemMd
            });

        return result.Result.Response;
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


public class OllamaGenerateInput
{
    public string Model { get; set; } = "Mistral";
    public string Prompt { get; set; }
    public bool Stream { get; set; }
}

public class OllamaGenerateOutput
{
    public bool Done { get; set; }
    public string Response { get; set; }
}