using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CodedByKay.ArtShow.CLI.Options;

namespace CodedByKay.ArtShow.CLI.Storage;

public class R2Client : IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public R2Client(R2Options options)
    {
        _bucket = options.Bucket;
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
            config);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // The client-level checksum config above wasn't enough on its own — R2 rejects
            // the SDK's chunked-with-trailing-checksum upload format
            // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER). These two request-level flags
            // are what actually force a plain, non-chunked signed upload.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };
        await _client.PutObjectAsync(request, ct);
    }

    /// <summary>Lists every object key currently in the bucket (paginated). Used by the sync-check flow.</summary>
    public async Task<Dictionary<string, long>> ListAllObjectsAsync(CancellationToken ct = default)
    {
        var objects = new Dictionary<string, long>();
        string? continuationToken = null;
        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                ContinuationToken = continuationToken,
            }, ct);

            foreach (var obj in response.S3Objects)
                objects[obj.Key] = obj.Size ?? 0;

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken is not null);

        return objects;
    }

    public void Dispose() => _client.Dispose();
}
