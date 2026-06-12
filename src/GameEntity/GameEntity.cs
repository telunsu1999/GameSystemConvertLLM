using System;
using System.Collections.Generic;

namespace GameLoop
{
    /// <summary>
    /// Component-based game entity (Actor pattern).
    /// Holds a dynamic list of IEntityComponent instances.
    /// OnTick iterates all components in order.
    /// </summary>
    public class GameEntity
    {
        public string Id { get; }
        private readonly List<IEntityComponent> _components = new List<IEntityComponent>();
        private readonly Dictionary<Type, IEntityComponent> _index = new Dictionary<Type, IEntityComponent>();

        public GameEntity(string id)
        {
            Id = id;
        }

        // === Component Management ===

        public T Add<T>(T component) where T : IEntityComponent
        {
            _components.Add(component);
            _index[typeof(T)] = component;
            return component;
        }

        public T Get<T>() where T : class, IEntityComponent
        {
            _index.TryGetValue(typeof(T), out var c);
            return c as T;
        }

        public bool Has<T>() where T : IEntityComponent
            => _index.ContainsKey(typeof(T));

        // === Lifecycle ===

        public void OnTick(TickContext ctx)
        {
            ctx.Entity = this;
            foreach (var c in _components)
                c.OnTick(ctx);
        }

        // === Convenience (delegate to Attributes component) ===

        public string Str(string key)
        {
            var a = Get<Attributes>();
            return a?.Get(key)?.Value?.ToString() ?? "";
        }

        public int Int(string key)
        {
            var a = Get<Attributes>();
            if (a == null) return 0;
            var v = a.Get(key)?.Value;
            return v is long l ? (int)l : v is int i ? i : 0;
        }
    }
}
