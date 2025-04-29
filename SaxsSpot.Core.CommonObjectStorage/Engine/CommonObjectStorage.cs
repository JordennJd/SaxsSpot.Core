using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using SaxsSpot.Core.Contracts.Services;

namespace SaxsSpot.Core.CommonObjectStorage.Engine;

public abstract class CommonObjectStorage<T> : ICommonObjectStorage<T>
{
    private readonly IMinioClient _minioClient;
    private readonly string? _bucketName;
    
    protected CommonObjectStorage(IConfiguration configuration)
    {
        var minioConfig = configuration.GetSection("minio");
        
        _minioClient = new MinioClient()
            .WithEndpoint(minioConfig["endpoint"])
            .WithCredentials(minioConfig["accessKey"], minioConfig["secretKey"])
            .WithSSL(false)
            .Build();
        
        _bucketName = minioConfig["bucketName"];
        
        var found = _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName)).Result;
        if (!found)
        {
            _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName)).Wait();
        }
    }
    
    public async Task Save(IEnumerable<T> data, Guid objectId)
    {
        var objectName = $"{objectId}";
        
        await using var stream = GetStream(data);
        stream.Position = 0;

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length))
            .ConfigureAwait(false);
    }
    

    public async IAsyncEnumerable<T> Load(Guid objectId)
    {
        var objectName = $"{objectId}";
        using var stream = new MemoryStream();

        // Download the file
        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(stream)));

        // Deserialize and return the data
        stream.Seek(0, SeekOrigin.Begin);

        foreach (var item in FromStream(stream))
        {
            yield return item;
        }
    }

    public Task Delete(Guid objectId)
    {
        throw new NotImplementedException();
    }

    protected abstract Stream GetStream(IEnumerable<T> data);

    protected abstract IEnumerable<T> FromStream(Stream data);
}
