namespace GameLoop
{
    public interface IGameEntity
    {
        string Id { get; }
        T Get<T>() where T : class, IEntityComponent;
        bool Has<T>() where T : IEntityComponent;
    }
}