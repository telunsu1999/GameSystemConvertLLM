using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GameLoop;

var root = RepoRoot.Find();

// Setup logging
var logSink = new FileLogSink(Path.Combine(root, "logs", "playground.log"));
Logger.SetSink(logSink);
Logger.Info("Playground", "Server starting", new { root });

var clock = WorldClock.FromConfig(Path.Combine(root, "configs", "clock", "world_clock.json"));
var events = new EventSystem();
var semantic = new SemanticEngine();
var llm = new LLMClient("http://localhost:8000", defaultMaxTokens: 8);
bool llmEnabled = true;
var llmManager = new LLMManager(llm);
llmManager.OnStatusChanged += status => { Logger.Info("LLM", $"Status: {status}"); };
llmManager.OnProcessOutput += line => { Logger.Debug("LLM.Proc", line); };
llmManager.Start(Path.Combine(root, ".venv", "Scripts", "python.exe"), root);

var game = new GameLoop.GameLoop(clock, events);
game.LlmEnabled = true;

// Create entities and attach LlmPlanner
foreach (var id in new[]{"blacksmith","merchant","guard"})
{
    var entity = EntityFactory.CreateFromJson(Path.Combine(root, "configs", "entities", $"{id}.json"), root);
    entity.Add(new LlmPlanner(llm, root));
    game.AddEntity(entity);
}
Logger.Info("Playground", "Entities initialized", new { count = game.Entities.Count });

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.Map("/ws", async (HttpContext ctx) => {
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await HandleSession(ws, root, game, llmManager);
});

app.Run();

async Task HandleSession(WebSocket ws, string root, GameLoop.GameLoop game, LLMManager llmManager)
{
    var buffer = new byte[4096];
    try {
        var state = game.BuildState();
        await Send(ws, new { type = "state", tick = state.Tick, season = state.Season,
            day = state.Day, timeBlock = state.TimeBlock, hour = state.Hour, npcs = state.Npcs });
        await SendLlmStatus(ws);
        void OnStatus(LLMStatus s) { try { if (ws.State == WebSocketState.Open) _ = SendLlmStatus(ws); } catch { } }
        llmManager.OnStatusChanged += OnStatus;
        while (ws.State == WebSocketState.Open) {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleMessage(ws, json, root, game, llmManager);
        }
        llmManager.OnStatusChanged -= OnStatus;
    } catch { }
}

async Task HandleMessage(WebSocket ws, string json, string root, GameLoop.GameLoop game, LLMManager llmManager)
{
    using var doc = JsonDocument.Parse(json);
    var type = doc.RootElement.GetProperty("type").GetString();
    switch (type) {
        case "tick":
            int count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 1;
            Logger.Info("Tick", $"Advancing {count} ticks", new { current = game.Clock.Tick });
            game.AdvanceTicks(count, state => {
                _ = Send(ws, new { type = "state", tick = state.Tick, season = state.Season,
                    day = state.Day, timeBlock = state.TimeBlock, hour = state.Hour, npcs = state.Npcs });
            });
            break;
        case "interact":
            var npcId = doc.RootElement.GetProperty("npcId").GetString();
            game.Interact(npcId, state => {
                _ = Send(ws, new { type = "state", tick = state.Tick, season = state.Season,
                    day = state.Day, timeBlock = state.TimeBlock, hour = state.Hour, npcs = state.Npcs });
            });
            break;
        case "semantic":
            npcId = doc.RootElement.GetProperty("npcId").GetString();
            await SendSemantic(ws, npcId, root, game);
            break;
        case "set_attr":
            npcId = doc.RootElement.GetProperty("npcId").GetString();
            var field = doc.RootElement.GetProperty("field").GetString();
            var val = doc.RootElement.GetProperty("value");
            await DebugSetAttr(ws, npcId, field, val, game);
            break;
        case "add_event":
            var etype = doc.RootElement.GetProperty("eventType").GetString();
            var tags = doc.RootElement.TryGetProperty("tags", out var t)
                ? t.EnumerateArray().Select(x => x.GetString()).ToArray() : Array.Empty<string>();
            var edata = doc.RootElement.TryGetProperty("data", out var d)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(d.GetRawText()) : new();
            var perceiveNpc = doc.RootElement.TryGetProperty("perceive", out var p) ? p.GetString() : null;
            var eid = game.Events.Record(etype, tags, edata);
            if (perceiveNpc != null) game.Events.Perceive(eid, perceiveNpc, "debug");
            await Send(ws, new { type = "event", tick = game.Clock.Tick, text = $"Event: {etype} (id={eid[..6]})" });
            break;
        case "toggle_llm": game.LlmEnabled = !game.LlmEnabled; await SendLlmStatus(ws); break;
        case "start_llm": llmManager.Start(Path.Combine(root, ".venv", "Scripts", "python.exe"), root); await SendLlmStatus(ws); break;
        case "stop_llm": llmManager.Stop(); await SendLlmStatus(ws); break;
        case "health": await SendLlmStatus(ws); break;
    }
}

async Task SendSemantic(WebSocket ws, string npcId, string root, GameLoop.GameLoop game)
{
    var entity = game.GetEntity(npcId);
    if (entity == null) return;
    var collector = new DataCollector(entity.Get<Attributes>(), game.Events, new SemanticEngine(),
        Path.Combine(root, "configs", "collect"), Path.Combine(root, "configs", "semantic"));
    var collected = collector.Collect(npcId, new CollectConfig{
        AttrTags = new[]{"identity","vital","world","currency","trade"},
        EventTags = new[]{"social","combat","travel"},
        RuleFiles = new[]{"npc_state","npc_danger","social","adventure_memory"},
        EventDays=7, EventLimit=5
    });
    var upcoming = entity.Get<Scheduler>()?.Upcoming(5).Select(u => new {
        tick = u.TriggerTick, action = u.ActionType,
        desc = u.Params.ContainsKey("location") ? $"to {u.Params["location"]}" : ""
    }).ToList();
    var cur = entity.Get<ActionResolver>()?.Current;
    var rawAttrs = new Dictionary<string, object>();
    foreach (var kv in entity.Get<Attributes>()?.Export()) rawAttrs[kv.Key] = kv.Value.Value;
    await Send(ws, new { type = "semantic", npcId,
        texts = collected.SemanticTexts, upcoming, current = cur?.ActionType ?? "idle", rawAttrs });
}

async Task DebugSetAttr(WebSocket ws, string npcId, string field, JsonElement val, GameLoop.GameLoop game)
{
    var entity = game.GetEntity(npcId);
    if (entity == null) return;
    var attrs = entity.Get<Attributes>();
    if (attrs == null) return;
    object v = val.ValueKind switch {
        JsonValueKind.Number => val.GetInt64(),
        JsonValueKind.String => val.GetString(),
        _ => val.ToString()
    };
    if (v is long lv) attrs.Set(field, lv, "number", new string[0]);
    else attrs.Set(field, v?.ToString() ?? "", "string", new string[0]);
    var res = entity.Get<TriggerSystem>()?.Evaluate("attr", field, attrs);
    Logger.Info("Debug", $"SetAttr: {npcId}.{field}={v}", new { matches = res?.DirectActions.Count + res?.LlmOptions.Count });
    await Send(ws, new { type = "event", tick = game.Clock.Tick,
        text = $"SetAttr: {npcId}.{field}={v} -> direct:{res?.DirectActions.Count} llm:{res?.LlmOptions.Count}" });
    if (res != null) {
        foreach (var r in res.DirectActions) {
            entity.Get<ActionResolver>()?.Execute(new ActionItem{ActionType=r.ActionType,Params=r.Params},
                attrs, game.Events, entity.Get<Scheduler>(), game.Clock.Snapshot());
        }
    }
}

async Task Send(WebSocket ws, object data) {
    if (ws.State != WebSocketState.Open) return;
    var json = JsonSerializer.Serialize(data);
    var bytes = Encoding.UTF8.GetBytes(json);
    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
}
async Task SendTrace(WebSocket ws, int step, string phase, string detail) {
    await Send(ws, new { type = "trace", step, phase, detail });
}
async Task SendLlmStatus(WebSocket ws) {
    var online = llmManager.Status == LLMStatus.Online;
    var running = llmManager.Status == LLMStatus.Starting || llmManager.Status == LLMStatus.Online;
    await Send(ws, new { type = "llm_status", online, llmEnabled = game.LlmEnabled, running });
}

static class RepoRoot { public static string Find() { var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); while (d != null) { if (Directory.Exists(Path.Combine(d.FullName, "configs"))) return d.FullName; d = d.Parent; } throw new Exception("root not found"); } }