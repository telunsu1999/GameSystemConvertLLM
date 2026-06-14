using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    /// <summary>
    /// Static helper to convert ActionSchema list into OpenAI function-calling
    /// tool definitions. Shared by all plan strategies.
    /// </summary>
    public static class PlanToolBuilder
    {
        public static List<object> Build(List<ActionSchema> actions)
        {
            var tools = new List<object>();
            foreach (var a in actions)
            {
                var func = new Dictionary<string, object>
                {
                    ["name"] = a.ActionType,
                    ["description"] = $"{a.Description}。效果: {a.Effect}",
                };
                if (a.Params != null && a.Params.Count > 0)
                {
                    var props = new Dictionary<string, object>();
                    var required = new List<string>();
                    foreach (var p in a.Params)
                    {
                        props[p.Name] = new Dictionary<string, object>
                        {
                            ["type"] = p.Type switch { "number" => "integer", _ => "string" },
                            ["description"] = p.Name,
                        };
                        required.Add(p.Name);
                    }
                    func["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = props,
                        ["required"] = required,
                    };
                }
                tools.Add(new Dictionary<string, object> { ["type"] = "function", ["function"] = func });
            }
            return tools;
        }
    }
}
