using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using CsvHelper;


namespace Easey.Job.Controllers;

public static class SkillExtensions
{
    public static bool IsSpecialized(this TypeDto type)
    {
        return type.Name == "Specialized Skill";
    }
}

public class JobDto
{
    public string job_title { get; set; }
    public string job_description { get; set; }
    public string company_name { get; set; }
}

public class ResumeJobDto
{
    public string job_title { get; set; }
    public string job_description { get; set; }
    public string company_name { get; set; }
    public string resume { get; set; }
}

public class PredictSimilarityInput
{
    [JsonPropertyName("resume")]
    public string Resume { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
}

public class PredictHardSkillInput
{
    [JsonPropertyName("message")]
    public string message { get; set; }
}

public class PredictSoftSkillInput
{
    [JsonPropertyName("message")]
    public string message { get; set; }
}

public class PredictSimilarityDto
{
    public double Score { get; set; }
}

public class PredictHardSkillDto
{
    public List<string> Skills { get; set; }
}

public class PredictSimilarJobInput
{
    public string Resume { get; set; }
}

public class PredictSimilarJobDto
{
    public List<JobRecommendation> Recommendations { get; set; } = new List<JobRecommendation>();
}

public class JobRecommendation
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Company { get; set; }
}

[ApiController]
[Route("[controller]")]
public class JobController : ControllerBase
{
    protected WebApiClient WebApiClient;
    private IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    public const string GPTURL = "http://localhost:11434/api/generate";
    public const string BaseUrl = "https://6ad0-172-83-13-4.ngrok-free.app";
    public const string PredictUrl = BaseUrl + "/predict";
    public const string PredictHardSkillUrl = BaseUrl + "/predict-hard-skills";
    public const string PredictSoftSkillUrl = BaseUrl + "/predict-soft-skills";
    public const string PredictSimilarJobs = BaseUrl + "predict-similar";
    
    public JobController(WebApiClient webApiClient, IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
    {
        WebApiClient = webApiClient;
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    [Route("predict-similarity")]
    public async Task<string> PredictSimilarity(PredictSimilarityInput input)
    {
        var result = await WebApiClient.PostAsync<PredictSimilarityInput, JsonElement>(
            new Uri(PredictUrl),
            new PredictSimilarityInput()
            {
                Resume = input.Resume,
                Description = input.Description
            });

        var res = JsonSerializer.Deserialize<PredictSimilarityDto>(result.Result);
        return res.Score.ToString();
    }

    [HttpPost]
    [Route("predict-hard-skills")]
    public async Task<List<string>> PredictHardSkills(PredictHardSkillInput input)
    {
        var result = await WebApiClient.PostAsync<PredictHardSkillInput, JsonElement>(new Uri(PredictHardSkillUrl), input);

        var res = JsonSerializer.Deserialize<PredictHardSkillDto>(result.Result);
        return res.Skills;
        return new List<string>()
        {
            "c#",
            ".net framework",
            "javascript"
        };
    }

    [HttpPost]
    [Route("predict-soft-skills")]
    public async Task<List<string>> PredictSoftSkills(PredictSoftSkillInput input)
    {
        var result = await WebApiClient.PostAsync<PredictSoftSkillInput, JsonElement>(new Uri(PredictSoftSkillUrl), input);

        var res = JsonSerializer.Deserialize<PredictHardSkillDto>(result.Result);
        return res.Skills;
        return new List<string>()
        {
            "problem-solving", "communication"
        };
    }

    [HttpPost]
    [Route("get-similar-jobs")]
    public async Task GetSimilarJobs(PredictSimilarJobInput input)
    {
        var result = await WebApiClient.PostAsync<PredictSimilarJobInput, JsonElement>(new Uri(PredictSimilarJobs), input);
    }

    [HttpPost]
    [Route("process-resume-with-job-description")]
    public async Task ProcessResumeJobDescription()
    {
        using var reader = new StreamReader(Path.Combine(Environment.CurrentDirectory, "jobstreet_scrap_result_combined.csv"));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        List<JobDto> jobs = csv.GetRecords<JobDto>().ToList();
        var result = new List<ResumeJobDto>();
        foreach (var job in jobs)
        {
            var resume = await GenerateResume(job.job_description);
            result.Add(new ResumeJobDto()
            {
                resume = resume,
                job_title = job.job_title,
                job_description = job.job_description,
                company_name = job.company_name
            });
        }
        
        using (var writer = new StreamWriter(Path.Combine(Environment.CurrentDirectory, "jobstreet_scrap_result_combined_resume.csv")))
        using (var writeCsv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await writeCsv.WriteRecordsAsync(jobs);
        }
        
    }
    
    [HttpPost]
    [Route("process-common-skill")]
    public async Task ProcessRecordToFastTextCommonSkills()
    {
        string currentDirectoryPath = Environment.CurrentDirectory;
        List<JobDto> jobs;
        List<string> processedText = new List<string>();
        Dictionary<string, string> skillDictionary = new Dictionary<string, string>();
        using var reader = new StreamReader(Path.Combine(currentDirectoryPath, "jobstreet_scrap_result.csv"));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        jobs = csv.GetRecords<JobDto>().ToList();
        foreach (var job in jobs)
        {
            var stringBuilder = new StringBuilder();
            var skills = await GetAllCommonSkills(job.job_description);
            foreach (var skill in skills)
            {
                var formattedSkill = "__LABEL__" + skill.Replace(" ", "-").ToUpper();
                stringBuilder.Append(formattedSkill);
                stringBuilder.Append(' ');
                skillDictionary.TryAdd(skill, skill);
            }

            if (string.IsNullOrEmpty(stringBuilder.ToString()))
            {
                foreach (var skill in skills.Take(5))
                {
                    var formattedSkill = "__LABEL__" + skill.Replace(" ", "-").ToUpper();
                    stringBuilder.Append(formattedSkill);
                    stringBuilder.Append(' ');
                }
            }
            
            stringBuilder.Append(job.job_title);
            stringBuilder.Append(' ');
            stringBuilder.Append(job.job_description);
            processedText.Add(stringBuilder.ToString().ToLower());
        }

        using (var writer = new StreamWriter(Path.Combine(currentDirectoryPath, "processed-common-skills.txt")))
        {
            foreach (var str in processedText)
            {
                await writer.WriteLineAsync(str);
            }
        }
        
        using (var writer = new StreamWriter(Path.Combine(currentDirectoryPath, "common-skills.txt")))
        {
            foreach (var sd in skillDictionary)
            {
                await writer.WriteLineAsync(sd.Key);
            }
        }
    }
    
    [HttpPost]
    [Route("process")]
    public async Task ProcessRecordToFastText()
    {
        Dictionary<string, string> filteredSkills = new Dictionary<string, string>()
        {
            {"TypeScript", "TypeScript"},
            {"Angular (Web Framework)", "Angular (Web Framework)"},
            {"Bootstrap (Front-End Framework)", "Bootstrap (Front-End Framework)"},
            {".NET Framework", ".NET Framework"},
            {"User Acceptance Testing (UAT)", "User Acceptance Testing (UAT)"},
            {"SQL (Programming Language)", "SQL (Programming Language)"},
            {"C# (Programming Language)", "C# (Programming Language)"},
            {"HyperText Markup Language (HTML)", "HyperText Markup Language (HTML)"},
            {"Database Design", "Database Design"},
            {"RESTful API", "RESTful API"},
            {"Penetration Testing", "Penetration Testing"},
            {"Marketing", "Marketing"},
            {"Account Management", "Account Management"},
            {"DevOps", "DevOps"},
            {"Sales Management", "Sales Management"},
            {"C (Programming Language)", "C (Programming Language)"},
            {"C++ (Programming Language)", "C++ (Programming Language)"},
            {"Machine Vision", "Machine Vision"},
            {"Sales Prospecting", "Sales Prospecting"},
            {"Web 3.0", "Web 3.0"},
            {"Docker (Software)", "Docker (Software)"},
            {"CI/CD", "CI/CD"},
            {"PostgreSQL", "PostgreSQL"},
            {"gRPC", "gRPC"},
            {"Go (Programming Language)", "Go (Programming Language)"},
            {"React.js (Javascript Library)", "React.js (Javascript Library)"},
            {"Flutter (Software)", "Flutter (Software)"},
            {"Flask (Web Framework)", "TypeSFlask (Web Framework)cript"},
            {"NoSQL", "NoSQL"},
            {"JavaScript (Programming Language)", "JavaScript (Programming Language)"},
            {"Vue.js (Javascript Library)", "Vue.js (Javascript Library)"},
            {"Vuex", "Vuex"},
            {"Cascading Style Sheets (CSS)", "Cascading Style Sheets (CSS)"},
            {"Java (Programming Language)", "Java (Programming Language)"},
            {"Spring Framework", "Spring Framework"},
            {"Mocha (JavaScript Framework)", "Mocha (JavaScript Framework)"},
            {"Jest (JavaScript Testing Framework)", "Jest (JavaScript Testing Framework)"},
            {"Next.js (Javascript Library)", "Next.js (Javascript Library)"},
            {"RabbitMQ", "RabbitMQ"},
            {"Visual C++ (Programming Language)", "Visual C++ (Programming Language)"},
            {"React Native", "React Native"},
            {"Kotlin", "Kotlin"},
            {"Node.js (Javascript Library)", "Node.js (Javascript Library)"},
            {"Microsoft SQL Servers", "Microsoft SQL Servers"},
            {"PHP Development", "PHP Development"},
            {"HTML5", "HTML5"},
            {"ASP.NET", "ASP.NET"},
            {"JQuery", "JQuery"},
            {"Objective-C (Programming Language)", "Objective-C (Programming Language)"},
            {"Swift (Programming Language)", "Swift (Programming Language)"},
            {"Kubernetes", "Kubernetes"},
            {"Open Web Application Security Project (OWASP)", "Open Web Application Security Project (OWASP)"},
            {"Property Management", "Property Management"},
            {"Compliance Management", "Compliance Management"},
            {"Bash (Scripting Language)", "Bash (Scripting Language)"},
            {"Terraform", "Terraform"},
            {"Selenium (Software)", "Selenium (Software)"},
            {"Project Planning", "Project Planning"},
            {"Business Analysis", "Business Analysis"},
            {"Scala (Programming Language)", "Scala (Programming Language)"},
            {"D3.js (Javascript Library)", "D3.js (Javascript Library)"},
            {"Thought Leadership", "Thought Leadership"},
            {"Value Propositions", "Value Propositions"},
            {"Strategic Decision Making", "Strategic Decision Making"},
            {"Accounting Systems", "Accounting Systems"},
            {"Cost Estimation", "Cost Estimation"},
            {"Budget Management", "Budget Management"},
            {"Stakeholder Engagement", "Stakeholder Engagement"},
            {"Financial Management", "Financial Management"},
            {"Perl (Programming Language)", "Perl (Programming Language)"},
            {"Industrial Automation", "Industrial Automation"},
            {"Predictive Modeling", "Predictive Modeling"},
            {"Network Planning And Design", "Network Planning And Design"},
            {"Banking", "Banking"},
            {"Threat Management", "Threat Management"},
            {"Direct Selling", "Direct Selling"},
            {"Microsoft Power Platform", "Microsoft Power Platform"},
            {"Video Processing", "Video Processing"},
            {"Post-Production", "Post-Production"},
            {"Profit And Loss (P&L) Management", "Profit And Loss (P&L) Management"},
            {"Environment Management", "Environment Management"},
            {"Climate Engineering", "Climate Engineering"},
            {"Process Management", "Process Management"},
            {"Tableau (Business Intelligence Software)", "Tableau (Business Intelligence Software)"},
            {"Business Valuation", "Business Valuation"},
            {"Social Media Monitoring", "Social Media Monitoring"},
            {"Crisis Management", "Crisis Management"},
            {"Recruitment Planning", "Recruitment Planning"},
            {"System Administration", "System Administration"},
            {"Sales Process", "Sales Process"},
            {"Market Analysis", "Market Analysis"},
            {"Talent Management", "Talent Management"},
            {"Brand Loyalty", "Brand Loyalty"},
            {"Proposal Writing", "Proposal Writing"},
            {"Business Development", "Business Development"},
            {"Statistical Analysis", "Statistical Analysis"},
            {"Product Marketing", "Product Marketing"},
            {"Strategic Partnership", "Strategic Partnership"},
            {"Marketing Strategies", "Marketing Strategies"},
            {"Audit Planning", "Audit Planning"},
            {"Quality Improvement", "Quality Improvement"},
            {"Financial Planning", "Financial Planning"},
            {"Management Reporting", "Management Reporting"},
            {"Operations Management", "Operations Management"},
            {"Employee Engagement", "Employee Engagement"},
            {"Cash Flow Forecasting", "Cash Flow Forecasting"},
            {"Accounting", "Accounting"},
            {"Issue Management", "Issue Management"},
            {"Management Consulting", "Management Consulting"},
            {"Crisis Communications", "Crisis Communications"},
            {"Brand Awareness", "Brand Awareness"},
            {"Digital Marketing", "Digital Marketing"},
            {"Customer Retention", "Customer Retention"},
            {"Marketing Planning", "Marketing Planning"},
            {"Online Marketing", "Online Marketing"},
            {"Strategic Management", "Strategic Management"},
            {"Corporate Strategy", "Corporate Strategy"},
            {"Lead Generation", "Lead Generation"},
            {"Sales Planning", "Sales Planning"},
            {"Freight Forwarding", "Freight Forwarding"},
            {"Supply Chain", "Supply Chain"},
            {"System Center Virtual Machine Management", "System Center Virtual Machine Management"},
            {"Marketing Automation", "Marketing Automation"},
            {"Digital Customer Strategy", "Digital Customer Strategy"},
            {"Commercial Banking", "Commercial Banking"},
            {"Product Engineering", "Product Engineering"},
            {"Curriculum Development", "Curriculum Development"},
            {"Product Improvement", "Product Improvement"},
            {"Environmental Protocols", "Environmental Protocols"},
            {"Impact Assessment", "Impact Assessment"},
            {"Contextual Image Classification", "Contextual Image Classification"},
            {"Image Segmentation", "Image Segmentation"},
            {"Chemical Engineering", "Chemical Engineering"},
            {"Auditor's Report", "Auditor's Report"},
            {"Health Education", "Health Education"},
            {"Supplier Management", "Supplier Management"},
            {"Strategic Procurement", "Strategic Procurement"},
            {"Quantity Surveying", "Quantity Surveying"},
            {"Strategic Prioritization", "Strategic Prioritization"},
            {"Payment Processing", "Payment Processing"},
            {"Affiliate Marketing", "Affiliate Marketing"},
            {"Copywriting", "Copywriting"},
            {"Financial Analysis", "Financial Analysis"},
            {"Credit Risk Management", "Credit Risk Management"},
            {"Credit Risk Analysis", "Credit Risk Analysis"},
            {"Content Management", "Content Management"},
            {"Talent Recruitment", "Talent Recruitment"},
            {"Digital Content Creation", "Digital Content Creation"},
            {"Event Planning", "Event Planning"},
            {"Corporate Communications", "Corporate Communications"},
            {"Media Relations", "Media Relations"},
            {"Social Media Management", "Social Media Management"},
            {"Expense Forecasting", "Expense Forecasting"},
            {"Cost Management", "Cost Management"},
            {"Global Procurement", "Global Procurement"},
            {"Search Engine Optimization", "Search Engine Optimization"},
            {"General Surgery", "General Surgery"},
            {"Environmental Laws", "Environmental Laws"},
            {"Marketing Communications", "Marketing Communications"},
            {"Asset Management", "Asset Management"},
            {"Accounts Payable", "Accounts Payable"},
            {"Bank Reconciliations", "Bank Reconciliations"},
            {"Corporate Branding", "Corporate Branding"},
            {"Email Marketing", "Email Marketing"},
            {"Microsoft 365", "Microsoft 365"},
            {"Order Management", "Order Management"},
            {"Market Positioning", "Market Positioning"},
            {"Fraud Risk Management", "Fraud Risk Management"},
            {"Fraud Prevention", "Fraud Prevention"},
            {"Revenue Management", "Revenue Management"},
            {"Billing Systems", "Billing Systems"},
            {"Partner Relationship Management", "Partner Relationship Management"},
            {"Audit Processes", "Audit Processes"},
            {"Budget Development", "Budget Development"},
            {"Forecasting Management", "Forecasting Management"},
            {"Talent Acquisition", "Talent Acquisition"},
            {"Credit Risk Modeling", "Credit Risk Modeling"},
            {"Cost Allocation", "Cost Allocation"},
            {"Content Development", "Content Development"},
            {"Market Intelligence", "Market Intelligence"},
            {"Media Planning", "Media Planning"},
            {"Project Risk Management", "Project Risk Management"},
            {"Event Operations", "Event Operations"},
            {"Digital Media Strategy", "Digital Media Strategy"},
            {"Clinical Practices", "Clinical Practices"},
            {"Medical Device Manufacturing", "Medical Device Manufacturing"},
            {"Petroleum Industry", "Petroleum Industry"},
            {"Digital Banking", "Digital Banking"},
            {"Institutional Investing", "TypInstitutional InvestingeScript"},
            {"Financial Accounting", "Financial Accounting"},
            {"Financial Services", "Financial Services"},
            {"Open Banking", "Open Banking"},
            {"Customer Engagement", "Customer Engagement"},
            {"Primavera (Software)", "Primavera (Software)"},
            {"Geneious (Bioinformatics Software)", "Geneious (Bioinformatics Software)"},
            {"Inventory Planning", "Inventory Planning"},
            {"Merchandise Planning", "Merchandise Planning"},
            {"Business Acquisition", "Business Acquisition"},
            {"Child Development", "Child Development"},
            {"Psychology", "Psychology"},
            {"Political Sciences", "Political Sciences"},
            {"Investor Relations", "Investor Relations"},
            {"Tax Laws", "Tax Laws"},
            {"Behavioral Science", "Behavioral Science"},
            {"Animal Health", "Animal Health"},
            {"Clinic Management Systems", "Clinic Management Systems"},
            {"Video Editing", "Video Editing"},
            {"Trend Analysis", "Trend Analysis"},
            {"Project Management", "Project Management"},
            {"Product Management", "Product Management"},
            {"Brand Management", "Brand Management"},
            {"Strategic Marketing", "Strategic Marketing"},
            {"Content Marketing", "Content Marketing"},
            {"Wealth Management", "Wealth Management"},
            {"Environmental Science", "Environmental Science"},
            {"Business Reporting", "Business Reporting"},
            {"Revenue Forecasting", "Revenue Forecasting"},
            {"Cloud Administration", "Cloud Administration"},
            {"Pandas (Python Package)", "Pandas (Python Package)"},
            {"Credit Risk", "Credit Risk"},
            {"Audit Scheduling", "Audit Scheduling"},
            {"Security Management", "Security Management"},
            {"Project Portfolio Management", "Project Portfolio Management"},
            {"Network Forensics", "Network Forensics"},
            {"Salesforce", "Salesforce"},
            {"Communications Training", "Communications Training"},
            {"Malware Analysis", "Malware Analysis"},
            {"Ecosystem Management", "Ecosystem Management"},
            {"Aviation Technology", "Aviation Technology"},
            {"Regression Analysis", "Regression Analysis"},
            {"Mobile UX Design", "Mobile UX Design"},
            {"Tax Compliance", "Tax Compliance"},
            {"Legal Risk", "Legal Risk"},
            {"Tax Consulting", "Tax Consulting"},
            {"Statutory Reporting", "Statutory Reporting"},
            {"Account Development", "Account Development"},
            {"Financial Auditing", "Financial Auditing"},
            {"Property Leasing", "Property Leasing"},
            {"Contract Negotiation", "Contract Negotiation"},
            {"Payroll Processing", "Payroll Processing"},
            {"Sales Engineering", "Sales Engineering"},
            {"Xero (Accounting Software)", "Xero (Accounting Software)"},
            {"Aircraft Maintenance", "Aircraft Maintenance"},
            {"Accounting Software", "Accounting Software"},
            {"Food Manufacturing", "Food Manufacturing"},
            {"Account Reconciliation", "Account Reconciliation"},
            {"Consolidation", "Consolidation"},
            {"Mortgage Loans", "Mortgage Loans"},
            {"Loss Prevention", "Loss Prevention"},
            {"Private Banking", "Private Banking"},
            {"Private Equity", "Private Equity"},
            {"Expense Analysis", "Expense Analysis"},
            {"Commercial Real Estate", "Commercial Real Estate"},
            {"Ship Management", "Ship Management"},
            {"Loans", "Loans"},
            {"Application Design", "Application Design"},
            {"Electronic Engineering", "Electronic Engineering"},
            {"Credit Control", "Credit Control"},
            {"Tax Management", "Tax Management"},
            {"Tourism Management", "Tourism Management"},
            {"Talent Attraction", "Talent Attraction"},
            {"Tax Risk Management", "Tax Risk Management"},
            {"Pharmaceutical Marketing", "Pharmaceutical Marketing"},
            {"Retail Management", "Retail Management"},
            {"Billing", "Billing"},
            {"Banking Relationship Management", "Banking Relationship Management"},
            {"Inventory Valuation", "Inventory Valuation"},
            {"Cost Accounting", "Cost Accounting"},
            {"Sales Optimization", "Sales Optimization"},
            {"Environmental Studies", "Environmental Studies"},
            {"Mentoring Youth", "Mentoring Youth"},
            {"Client Onboarding", "Client Onboarding"},
            {"Internal Auditing", "Internal Auditing"},
        };
        string currentDirectoryPath = Environment.CurrentDirectory;
        List<JobDto> jobs;
        List<string> processedText = new List<string>();
        Dictionary<string, string> skillDictionary = new Dictionary<string, string>();
        using var reader = new StreamReader(Path.Combine(currentDirectoryPath, "jobstreet_scrap_result.csv"));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        jobs = csv.GetRecords<JobDto>().ToList();
        foreach (var job in jobs)
        {
            var stringBuilder = new StringBuilder();
            var skills = await GetAllSpecializedSkills(job.job_description);
            foreach (var skill in skills)
            {
                if(!filteredSkills.TryGetValue(skill, out var s)) continue;
                var formattedSkill = "__LABEL__" + skill.Replace(" ", "-").ToUpper();
                stringBuilder.Append(formattedSkill);
                stringBuilder.Append(' ');
                // skillDictionary.TryAdd(skill, skill);
            }

            if (string.IsNullOrEmpty(stringBuilder.ToString()))
            {
                foreach (var skill in skills.Take(5))
                {
                    var formattedSkill = "__LABEL__" + skill.Replace(" ", "-").ToUpper();
                    stringBuilder.Append(formattedSkill);
                    stringBuilder.Append(' ');
                }
            }
            
            stringBuilder.Append(job.job_title);
            stringBuilder.Append(' ');
            stringBuilder.Append(job.job_description);
            processedText.Add(stringBuilder.ToString().ToLower());
        }

        using (var writer = new StreamWriter(Path.Combine(currentDirectoryPath, "processed-special-skills.txt")))
        {
            foreach (var str in processedText)
            {
                await writer.WriteLineAsync(str);
            }
        }
        
        // using (var writer = new StreamWriter(Path.Combine(currentDirectoryPath, "special-skills.txt")))
        // {
        //     foreach (var sd in skillDictionary)
        //     {
        //         await writer.WriteLineAsync(sd.Key);
        //     }
        // }
       
    }
    
    [HttpGet]
    [Route("get-all-specialized-skills")]
    public async Task<List<string>> GetAllSpecializedSkills(string description)
    {
        var accessToken = await Login();
        
        WebApiClient.Headers.TryAdd("Authorization", $"Bearer {accessToken}");
        
        var result = await WebApiClient.PostAsync<ExtractSkillFromDocumentInput, ExtractSkillFromDocumentOutput>(
            new Uri("https://emsiservices.com/skills/versions/latest/extract?language=en"),
            new ExtractSkillFromDocumentInput()
            {
                Text = description,
                ConfidenceThreshold = 1
            });

        return result.Result.Data.Select(x => x.Skill).Where(x => x.Type.IsSpecialized()).Select(x => x.Name).ToList();
    }

    [HttpGet]
    [Route("generate-job-description")]
    public async Task<string> GenerateJobDescription(string resume)
    {
        var systemMd = @"
# IDENTITY AND GOALS

You are an expert human resource manager. You specialize in different industry and know what are the required things to look out for in a resume.

You are able to come up with the job description suitable for the resume given to you based on the skills set required.
Take a step back and think step by step about how to accomplish this task using the steps below.

# STEPS

- Included in the input should be job description which are telling the AI what to do to generate the output. 

- Think deeply and read through the job description and what they're trying to describe.

- Also included in the input should be the AI's output that was created from that prompt.

- Deeply analyze the job description and provide the resume.

## resume
{{resume}}

# OUTPUT

output only the job description data
";
        systemMd = systemMd.Replace("{{resume}}", resume);
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri(GPTURL),
            new OllamaGenerateInput()
            {
                Prompt = systemMd
            });

        return result.Result.Response;
    }

    [HttpGet]
    [Route("generate-resume")]
    public async Task<string> GenerateResume(string jobDescription)
    {
        var systemMd = @"
# IDENTITY AND GOALS

You are an expert human resource manager. You specialize in different industry and know what are the required things to look out for in a resume.

You are able to come up with the resume to match the job description based on your knowledge.

come up with actual real world data in singapore context to your knowledge instead of using placeholder for field like name, address, company, phone, school

Take a step back and think step by step about how to accomplish this task using the steps below.

# STEPS

- Included in the input should be job description which are telling the AI what to do to generate the output. 

- Think deeply and read through the job description and what they're trying to describe.

- Also included in the input should be the AI's output that was created from that prompt.

- Deeply analyze the job description and provide the resume.

## JOB DESCRIPTION
{{jobDescription}}

# OUTPUT

output only the resume data
";
        systemMd = systemMd.Replace("{{jobDescription}}", jobDescription);
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri(GPTURL),
            new OllamaGenerateInput()
            {
                Prompt = systemMd
            });

        return result.Result.Response;
    }
    
    [HttpGet]
    [Route("get-all-common-skills")]
    public async Task<List<string>> GetAllCommonSkills(string description)
    {
        var accessToken = await Login();
        
        WebApiClient.Headers.TryAdd("Authorization", $"Bearer {accessToken}");
        
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
            new Uri(GPTURL),
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
            new Uri(GPTURL),
            new OllamaGenerateInput()
            {
                Prompt = systemMd
            });

        return result.Result.Response;
    }

    [HttpPost]
    [Route("get-resume-summary")]
    public async Task<string> GetResumeSummary(string resume)
    {
        var systemMd = @"
# IDENTITY and PURPOSE

You are an expert content summarizer. You take content in with is the resume employment history and user skills and provide a summary.

Take a deep breath and think step by step about how to best accomplish this goal using the following steps.

# STEPS

- Included in the input should be employment history and user skills in a resume, which are telling the AI what to do to generate the output no more than 80 words. 

- Think deeply and read through the employment history and user skills and what they're trying to describe.

- Also included in the input should be the AI's output that was created from that prompt.

- Deeply analyze the employment history and user skills and provide the 3 different variation of the summary from first person point of view.

## RESUME
{{resume}}


# OUTPUT

Output the summary as a string less than 100 words

output only the summary as a single string

## EXAMPLE
'summary....'

";
        
        systemMd = systemMd.Replace("{{resume}}", resume);
        var result = await WebApiClient.PostAsync<OllamaGenerateInput, OllamaGenerateOutput>(
            new Uri(GPTURL),
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