using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using api.Importing;

namespace api.Features.Admin;

[ApiController]
[Route("api/admin/import")]
public class AdminImportController : ControllerBase
{
    private readonly IEnumerable<ISourceImporter> _importers;

    public AdminImportController(IEnumerable<ISourceImporter> importers)
    {
        _importers = importers;
    }

    public record ImportSourceDto(string Key, string Name, IEnumerable<string> Games);

    [HttpGet("sources")]
    public ActionResult<IEnumerable<ImportSourceDto>> GetSources()
        => Ok(_importers.Select(i => new ImportSourceDto(i.Key, i.DisplayName, i.SupportedGames)));
}
