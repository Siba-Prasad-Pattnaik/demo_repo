using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Infrastructure.Storage;

public class MinIOStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinIOConfiguration _config;
    private readonly ILogger<MinIOStorageService> _logger;

    public MinIOStorageService(
        IMinioClient minioClient,
        IOptions<MinIOConfiguration> config,
        ILogger<MinIOStorageService> logger)
    {
        _minioClient = minioClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            // Ensure bucket exists
            await EnsureBucketExistsAsync();

            // Generate unique file path
            var fileId = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(fileName);
            var objectName = $"{DateTime.UtcNow:yyyy/MM/dd}/{fileId}{extension}";

            // Upload file
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            _logger.LogInformation("File uploaded successfully: {ObjectName}", objectName);
            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string filePath)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(filePath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(filePath);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);

            _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(filePath);

            await _minioClient.StatObjectAsync(statObjectArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetFileUrlAsync(string filePath, TimeSpan expiration)
    {
        try
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(filePath)
                .WithExpiry((int)expiration.TotalSeconds);

            return await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned URL: {FilePath}", filePath);
            throw;
        }
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_config.BucketName);

            if (!await _minioClient.BucketExistsAsync(bucketExistsArgs))
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_config.BucketName)
                    .WithLocation(_config.Region);

                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Created bucket: {BucketName}", _config.BucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring bucket exists: {BucketName}", _config.BucketName);
            throw;
        }
    }
}
