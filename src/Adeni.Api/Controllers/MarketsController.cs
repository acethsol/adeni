namespace Adeni.Api.Controllers;

using Adeni.Application.Markets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/markets")]
public sealed class MarketsController(IMarketCatalog catalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult List()
    {
        var items = catalog.List().Select(market => new
        {
            id = market.Id,
            name = market.Name,
            countryCode = market.CountryCode,
            currency = market.Currency,
            timeZoneId = market.TimeZoneId,
            defaultLocation = new { lat = market.DefaultLocation.Lat, lng = market.DefaultLocation.Lng },
            languages = market.Languages,
            isLive = market.IsLive,
            launchNote = market.LaunchNote,
        });

        return Ok(new { items });
    }
}
