using System.IO.Pipelines;
using System.Runtime.CompilerServices;
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
    

    public async IAsyncEnumerable<T> Load(Guid objectId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var objectName = $"{objectId}";
    
        var tempFilePath = Path.GetTempFileName();
    
        try
        {
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithFile(tempFilePath), cancellationToken);

            await using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        
            await foreach (var item in FromStreamAsync(fileStream).WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public Task Delete(Guid objectId)
    {
        throw new NotImplementedException();
    }

    protected abstract Stream GetStream(IEnumerable<T> data);

    protected abstract IAsyncEnumerable<T> FromStreamAsync(Stream data);
}
