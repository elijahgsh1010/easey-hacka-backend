using System.Text.Json.Serialization;

namespace Easey.Job;

public class LightCastModel
{
    
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


