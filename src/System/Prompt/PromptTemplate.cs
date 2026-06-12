using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scriban;
using Scriban.Runtime;

namespace GameLoop
{
    /// <summary>
    /// Assembles a complete LLM prompt from attributes, collected data, and task/options.
    /// Templates are Scriban .txt files selected by the NPC's prompt_template attribute.
    /// </summary>
    public class PromptTemplate : IPromptTemplate
    {
        private readonly string _templateDir;

        public PromptTemplate(string templateDir)
        {
            _templateDir = templateDir;
        }

        /// <summary>
        /// Build a full LLM prompt.
        /// </summary>
        /// <param name="attrs">Entity attributes (must contain name, personality)</param>
        /// <param name="collected">DataCollector output</param>
        /// <param name="task">Current task description</param>
        /// <param name="options">Available actions</param>
        public string Build(Attributes attrs, CollectResult collected, string task, List<string> options)
        {
            var templateName = attrs.Get("prompt_template")?.Value?.ToString();
            if (string.IsNullOrEmpty(templateName)) templateName = "default";

            var templatePath = Path.Combine(_templateDir, $"{templateName}.txt");
            if (!File.Exists(templatePath))
                templatePath = Path.Combine(_templateDir, "default.txt");

            var template = Template.Parse(File.ReadAllText(templatePath));

            // Separate state lines from memory lines
            var stateLines = new List<string>();
            var memoryLines = new List<string>();
            foreach (var kv in collected.SemanticTexts)
            {
                if (kv.Key.StartsWith("memory_"))
                    memoryLines.Add(kv.Value);
                else
                    stateLines.Add(kv.Value);
            }

            var scriptObj = new ScriptObject();
            scriptObj["name"] = attrs.Get("name")?.Value?.ToString() ?? "";
            scriptObj["personality"] = attrs.Get("personality")?.Value?.ToString() ?? "";
            scriptObj["state_lines"] = stateLines;
            scriptObj["memory_lines"] = memoryLines;
            scriptObj["task"] = task ?? "";
            scriptObj["options"] = options ?? new List<string>();

            var context = new TemplateContext();
            context.PushGlobal(scriptObj);
            var result = template.Render(context).Trim();
            // 闁告ê顑囩紓澶嬪緞濮橆偆绋囩紒宀勭細椤?
            while (result.Contains("\n\n\n"))
                result = result.Replace("\n\n\n", "\n\n");
            return result;
        }
    }
}
