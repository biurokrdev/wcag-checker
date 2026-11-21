using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Workbooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace D2.Wcag.Api.Controllers;

// Audyt dostępności skoroszytów Excel wg WCAG (profil 2.2 AA albo 2.1 AA). 200 niesie wynik audytu – nigdy nie
// stwierdza on zgodności z WCAG. 4xx to żądanie, którego nie dało się obsłużyć (plik innego typu, format
// nieobsługiwany, plik uszkodzony), jako ProblemDetails po polsku.
[ApiController]
[Route("api/wcag-workbooks")]
[Produces("application/json")]
public sealed class WcagWorkbooksController(IWorkbookAuditService service) : ControllerBase
{
    // Limit pliku wspólny ze ścieżką Word.
    private const long RequestLimitBytes = WordDocument.MaxSizeBytes + 1024 * 1024;

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [ProducesResponseType(typeof(WorkbookValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkbookValidationResponse>> Validate([FromForm] CheckWorkbookRequest request, CancellationToken ct)
    {
        var file = request.File!;
        if (file.Length > WordDocument.MaxSizeBytes)
        {
            throw new DocumentRejectedException($"Plik jest za duży. Dopuszczalny rozmiar to {WordDocument.MaxSizeBytes / (1024 * 1024)} MB.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return Ok(service.Validate(file.FileName, buffer.ToArray(), request.WcagVersion, ct));
    }

    // Katalog kryteriów WCAG poziomu A i AA z zastosowaniem do arkuszy – bez pliku (widok pusty, dokumentacja).
    [HttpGet("criteria")]
    [ProducesResponseType(typeof(WorkbookCriteriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<WorkbookCriteriaResponse> Criteria([FromQuery] string? version) => Ok(service.Criteria(version));

    // Raport do pobrania: docx (domyślnie) albo pdf. Ciało JSON jest opcjonalne: lista odhaczonych pozycji checklisty.
    [HttpPost("reports/{reportId}/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Export(
        string reportId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ExportWorkbookReportRequest? request,
        [FromQuery] string format = "docx")
    {
        var exported = service.Export(reportId, request, format);
        return File(exported.Content, exported.ContentType, exported.FileName);
    }
}
