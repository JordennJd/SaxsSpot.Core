namespace SaxsSpot.Core.Contracts.Services;

public interface ICommonObjectStorage<T>
{
    Task Save(IEnumerable<T> data, Guid objectId);

    IAsyncEnumerable<T> Load(Guid objectId, CancellationToken cancellationToken);
    
}