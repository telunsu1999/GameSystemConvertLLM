using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/bootstrap", () =>
{
    var paths = RepoPaths.Find();

    return Results.Ok(new BootstrapResponse(
        GetNames(paths.CollectConfigDir, "*.json"),
        GetNames(paths.SemanticConfigDir, "*.json"),
        GetNames(paths.TemplateDir, "*.txt"),
        "http://127.0.0.1:8000"));
});

app.MapGet("/api/llm/health", async (string? baseUrl, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var targetBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:8000" : baseUrl.TrimEnd('/');
    var client = httpClientFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(10);

    try
    {
        var response = await client.GetAsync($"{targetBaseUrl}/health", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return Results.Ok(new LlmHealthResponse(true, (int)response.StatusCode, payload, null));
    }
    catch (Exception ex)
    {
        return Results.Ok(new LlmHealthResponse(false, null, null, ex.Message));
    }
});

app.MapPost("/api/workbench/run", async (WorkbenchRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    try
    {
        var paths = RepoPaths.Find();
        var attrs = new Attributes();
        var records = new RecordModule();
        var semantic = new SemanticEngine();
        var collector = new DataCollector.DataCollector(
            attrs,
            records,
            semantic,
            paths.CollectConfigDir,
            paths.SemanticConfigDir);
        var promptBuilder = new PromptTemplate.PromptTemplate(paths.TemplateDir);

        SeedCoreAttributes(attrs, request);
        SeedCustomAttributes(attrs, request.Attributes);
        SeedRecords(records, request.Events);

        CollectResult collected = string.IsNullOrWhiteSpace(request.ConfigName)
            ? collector.Collect(request.NpcId, BuildCollectConfig(request.ManualConfig))
            : collector.Collect(request.NpcId, request.ConfigName);

        var options = request.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

        var prompt = promptBuilder.Build(attrs, collected, request.Task ?? string.Empty, options);

        LlmInvocationResult? llm = null;
        if (request.InvokeLlm)
        {
            llm = await InvokeLlmAsync(prompt, request.Llm, httpClientFactory, cancellationToken);
        }

        var response = new WorkbenchResponse(
            attrs.Export().ToDictionary(
                entry => entry.Key,
                entry => new AttributeSnapshot(entry.Value.Value, entry.Value.Type, entry.Value.Tags)),
            collected.RawData,
            collected.SemanticTexts,
            prompt,
            llm);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

app.Run();

static string[] GetNames(string directory, string pattern)
{
    if (!Directory.Exists(directory))
    {
        return Array.Empty<string>();
    }

    return Directory
        .GetFiles(directory, pattern)
        .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static void SeedCoreAttributes(Attributes attrs, WorkbenchRequest request)
{
    attrs.Set("name", request.Name, "string", new[] { "identity" });
    attrs.Set("personality", request.Personality, "string", new[] { "identity" });
    attrs.Set("prompt_template", request.TemplateName, "string", new[] { "meta" });
}

static void SeedCustomAttributes(Attributes attrs, IReadOnlyList<AttributeInput> attributes)
{
    foreach (var attribute in attributes)
    {
        if (string.IsNullOrWhiteSpace(attribute.Name))
        {
            continue;
        }

        var type = string.IsNullOrWhiteSpace(attribute.Type) ? "string" : attribute.Type.Trim();
        var value = ConvertAttributeValue(attribute.Value, type);
        var tags = SplitCsv(attribute.Tags);
        attrs.Set(attribute.Name.Trim(), value, type, tags);
    }
}

static void SeedRecords(RecordModule records, IReadOnlyList<EventInput> events)
{
    foreach (var evt in events)
    {
        if (string.IsNullOrWhiteSpace(evt.Type))
        {
            continue;
        }

        var data = evt.Data
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => ConvertLooseValue(pair.Value));

        records.Remember(new RecordSource{Method="self"},
            evt.Type.Trim(),
            SplitCsv(evt.Tags),
            data, 0);

        foreach (var perception in evt.Perceptions)
        {
            if (string.IsNullOrWhiteSpace(perception.NpcId) || string.IsNullOrWhiteSpace(perception.How))
            {
                continue;
            }

            records.Remember(new RecordSource{
                Method = perception.How.Trim(),
                FromNpcId = perception.NpcId.Trim()
            },
            evt.Type.Trim(),
            SplitCsv(evt.Tags),
            data, 0);
        }
    }
}

static CollectConfig BuildCollectConfig(ManualCollectConfig? manualConfig)
{
    manualConfig ??= new ManualCollectConfig();

    return new CollectConfig
    {
        AttrTags = SplitCsv(manualConfig.AttrTags),
        EventTags = SplitCsv(manualConfig.EventTags),
        RuleFiles = SplitCsv(manualConfig.RuleFiles),
        EventDays = manualConfig.EventDays,
        EventLimit = manualConfig.EventLimit,
        EventSortBy = string.IsNullOrWhiteSpace(manualConfig.EventSortBy) ? "time_desc" : manualConfig.EventSortBy,
        EventSourceFilter = SplitCsv(manualConfig.EventSourceFilter)
    };
}

static async Task<LlmInvocationResult> InvokeLlmAsync(string prompt, LlmRequest? llmRequest, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
{
    llmRequest ??= new LlmRequest();
    var baseUrl = string.IsNullOrWhiteSpace(llmRequest.BaseUrl) ? "http://127.0.0.1:8000" : llmRequest.BaseUrl.TrimEnd('/');
    var client = httpClientFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(60);

    var payload = new
    {
        prompt,
        max_tokens = llmRequest.MaxTokens,
        temperature = llmRequest.Temperature,
        top_p = llmRequest.TopP
    };

    try
    {
        var response = await client.PostAsJsonAsync($"{baseUrl}/api/v1/generate", payload, cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new LlmInvocationResult(false, (int)response.StatusCode, null, rawBody);
        }

        var parsed = JsonSerializer.Deserialize<LlmGenerateResponse>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return new LlmInvocationResult(true, (int)response.StatusCode, parsed, null);
    }
    catch (Exception ex)
    {
        return new LlmInvocationResult(false, null, null, ex.Message);
    }
}

static object ConvertAttributeValue(string? rawValue, string type)
{
    return type switch
    {
        "number" => ParseNumber(rawValue),
        "bool" => ParseBool(rawValue),
        _ => rawValue ?? string.Empty
    };
}

static object ConvertLooseValue(string? rawValue)
{
    if (rawValue is null)
    {
        return string.Empty;
    }

    if (bool.TryParse(rawValue, out var boolValue))
    {
        return boolValue;
    }

    if (long.TryParse(rawValue, out var longValue))
    {
        return longValue;
    }

    if (double.TryParse(rawValue, out var doubleValue))
    {
        return doubleValue;
    }

    return rawValue;
}

static object ParseNumber(string? rawValue)
{
    if (double.TryParse(rawValue, out var doubleValue))
    {
        if (Math.Abs(doubleValue % 1) < double.Epsilon && doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
        {
            return (int)doubleValue;
        }

        return doubleValue;
    }

    throw new ArgumentException($"'{rawValue}' 娑撳秵妲搁崥鍫熺《閺佹澘鐡ч妴?);
}

static bool ParseBool(string? rawValue)
{
    if (bool.TryParse(rawValue, out var boolValue))
    {
        return boolValue;
    }

    throw new ArgumentException($"'{rawValue}' 娑撳秵妲搁崥鍫熺《鐢啫鐨甸崐绗衡偓鍌濐嚞鏉堟挸鍙?true 閹?false閵?);
}

static string[] SplitCsv(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return Array.Empty<string>();
    }

    return raw
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

sealed record BootstrapResponse(string[] CollectConfigs, string[] RuleFiles, string[] Templates, string DefaultLlmBaseUrl);
sealed record LlmHealthResponse(bool Reachable, int? StatusCode, string? Payload, string? Error);
sealed record ErrorResponse(string Error);

sealed record WorkbenchRequest(
    string NpcId,
    string Name,
    string Personality,
    string TemplateName,
    string? ConfigName,
    ManualCollectConfig? ManualConfig,
    IReadOnlyList<AttributeInput> Attributes,
    IReadOnlyList<EventInput> Events,
    string? Task,
    IReadOnlyList<string> Options,
    bool InvokeLlm,
    LlmRequest? Llm);

sealed record ManualCollectConfig(
    string? AttrTags = null,
    string? EventTags = null,
    string? RuleFiles = null,
    int? EventDays = 7,
    int? EventLimit = 5,
    string? EventSortBy = "time_desc",
    string? EventSourceFilter = null);

sealed record AttributeInput(string Name, string Type, string? Value, string? Tags);
sealed record EventInput(string Type, string? Tags, IReadOnlyList<KeyValueInput> Data, IReadOnlyList<PerceptionInput> Perceptions);
sealed record KeyValueInput(string Key, string? Value);
sealed record PerceptionInput(string NpcId, string How, string? From);
sealed record LlmRequest(string? BaseUrl = null, int MaxTokens = 256, double Temperature = 0.7, double TopP = 0.9);

sealed record WorkbenchResponse(
    IReadOnlyDictionary<string, AttributeSnapshot> Attributes,
    Dictionary<string, object> RawData,
    Dictionary<string, string> SemanticTexts,
    string Prompt,
    LlmInvocationResult? Llm);

sealed record AttributeSnapshot(object Value, string Type, string[] Tags);
sealed record LlmInvocationResult(bool Success, int? StatusCode, LlmGenerateResponse? Response, string? Error);
sealed record LlmGenerateResponse(string Text, int TokensUsed);

sealed record RepoPaths(string RootDir, string CollectConfigDir, string SemanticConfigDir, string TemplateDir)
{
    public static RepoPaths Find()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var configDir = Path.Combine(current.FullName, "configs");
            var templateDir = Path.Combine(current.FullName, "src", "PromptTemplate", "Templates");
            if (Directory.Exists(configDir) && Directory.Exists(templateDir))
            {
                return new RepoPaths(
                    current.FullName,
                    Path.Combine(configDir, "collect"),
                    Path.Combine(configDir, "semantic"),
                    templateDir);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("閺冪姵纭剁€规矮缍呮禒鎾崇氨閺嶅湱娲拌ぐ鏇樷偓鍌濐嚞娴犲簼绮ㄦ惔鎾冲敶鏉╂劘顢戠拠銉ヤ紣閸忔灚鈧?);
    }
}