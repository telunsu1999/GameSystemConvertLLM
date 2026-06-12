using System.Collections.Generic;

namespace GameLoop
{
    public interface IPromptTemplate
    {
        string Build(Attributes attrs, CollectResult collected, string task, List<string> options);
    }
}
