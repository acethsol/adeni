namespace Adeni.Application.Tests.Storage;

using Adeni.Application.Storage;

public sealed class GalleryImageKeysTests
{
    [Fact]
    public void Deserialize_Empty_ReturnsEmpty()
    {
        Assert.Empty(GalleryImageKeys.Deserialize(null));
        Assert.Empty(GalleryImageKeys.Deserialize(""));
        Assert.Empty(GalleryImageKeys.Deserialize("[]"));
    }

    [Fact]
    public void Deserialize_ParsesAndDedupes()
    {
        var keys = GalleryImageKeys.Deserialize(
            """["tenants/a/gallery/1.jpg","tenants/a/gallery/1.jpg","tenants/a/gallery/2.jpg"]""");

        Assert.Equal(2, keys.Count);
        Assert.Equal("tenants/a/gallery/1.jpg", keys[0]);
        Assert.Equal("tenants/a/gallery/2.jpg", keys[1]);
    }

    [Fact]
    public void Serialize_CapsAtMax()
    {
        var input = Enumerable.Range(1, 10).Select(i => $"tenants/a/gallery/{i}.jpg").ToList();
        var json = GalleryImageKeys.Serialize(input);
        var roundTrip = GalleryImageKeys.Deserialize(json);

        Assert.Equal(GalleryImageKeys.MaxCount, roundTrip.Count);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(GalleryImageKeys.Deserialize("{not-json"));
    }
}
