public class DownloadSegment
{
    public long Start { get; set; }
    public long End { get; set; }
    public long BytesDownloaded { get; set; }
    public byte[] Data { get; set; }
    public bool IsComplete { get; set; }
}