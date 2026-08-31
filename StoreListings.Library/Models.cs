namespace StoreListings.Library;

public sealed class Image(string url, string backgroundColor, int height, int width)
{
    public string Url { get; set; } = url;
    public string BackgroundColor { get; } = backgroundColor;
    public int Height { get; } = height;
    public int Width { get; } = width;
}

public sealed class Card(
    string productId,
    string title,
    string? displayPrice,
    double? averageRating,
    InstallerType installerType,
    Image image
)
{
    public string ProductId { get; } = productId;
    public string Title { get; } = title;
    public string? DisplayPrice { get; } = displayPrice;
    public double? AverageRating { get; } = averageRating;
    public InstallerType InstallerType { get; } = installerType;
    public Image Image { get; } = image;
}

public sealed class DownloadResource(string url, string digest)
{
    public string Url { get; } = url;
    public string Digest { get; } = digest;
}

public sealed class PackageDownloadInfo(DownloadResource package, DownloadResource? blockmapCab)
{
    public DownloadResource Package { get; } = package;
    public DownloadResource? BlockmapCab { get; } = blockmapCab;
}
