using Adeni.Infrastructure.Storage;

namespace Adeni.Infrastructure.Tests.Storage;

public sealed class TenantMediaGalleryKeysTests
{
    [Fact]
    public void DeserializeGalleryKeys_Empty_ReturnsEmpty()
    {
        Assert.Empty(TenantMediaService.DeserializeGalleryKeys(null));
        Assert.Empty(TenantMediaService.DeserializeGalleryKeys(""));
        Assert.Empty(TenantMediaService.DeserializeGalleryKeys("[]"));
    }

    [Fact]
    public void DeserializeGalleryKeys_ParsesAndDedupes()
    {
        var keys = TenantMediaService.DeserializeGalleryKeys(
            """["tenants/a/gallery/1.jpg","tenants/a/gallery/1.jpg","tenants/a/gallery/2.jpg"]""");

        Assert.Equal(2, keys.Count);
        Assert.Equal("tenants/a/gallery/1.jpg", keys[0]);
        Assert.Equal("tenants/a/gallery/2.jpg", keys[1]);
    }

    [Fact]
    public void SerializeGalleryKeys_CapsAtMax()
    {
        var input = Enumerable.Range(1, 10).Select(i => $"tenants/a/gallery/{i}.jpg").ToList();
        var json = TenantMediaService.SerializeGalleryKeys(input);
        var roundTrip = TenantMediaService.DeserializeGalleryKeys(json);

        Assert.Equal(TenantMediaService.MaxGalleryImages, roundTrip.Count);
    }
}
