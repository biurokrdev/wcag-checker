using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Validation;
using Microsoft.AspNetCore.Mvc;

namespace D2.Wcag.Api.Controllers;

// 200 niesie wynik sprawdzenia, także negatywny. 4xx/5xx oznaczają, że żądania nie dało się obsłużyć
// (zły plik, uszkodzony dokument, nieznany standard), i są ProblemDetails.
[ApiController]
[Route("api/wcag-documents")]
[Produces("application/json")]
public sealed class WcagDocumentsController(IDocumentCheckService service) : ControllerBase
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const long RequestLimitBytes = WordDocument.MaxSizeBytes + 1024 * 1024;

    // Katalog standardów do wyboru (WCAG i wersje standardu firmowego) oraz domyślny zestaw API.
    [HttpGet("standards")]
    [ProducesResponseType(typeof(StandardsResponse), StatusCodes.Status200OK)]
    public ActionResult<StandardsResponse> Standards() => Ok(service.Standards());

    // Które kryteria WCAG 2.1 potwierdza wynik bez zgłoszeń, a które trzeba sprawdzić ręcznie (do raportu zgodności).
    [HttpGet("wcag-criteria")]
    [ProducesResponseType(typeof(IReadOnlyList<WcagCriterionDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WcagCriterionDto>> WcagCriteria() =>
        Ok(D2.Wcag.Standards.WcagStandard.Criteria.Select(WcagCriterionDto.From).ToList());

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ValidationResponse>> Validate([FromForm] CheckDocumentRequest request, CancellationToken ct)
    {
        var document = await ReadAsync(request.File!, ct);
        return Ok(service.Validate(document, request.Standards));
    }

    [HttpPost("fix")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [ProducesResponseType(typeof(FixResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FixResponse>> Fix([FromForm] FixDocumentRequest request, CancellationToken ct)
    {
        var document = await ReadAsync(request.File!, ct);
        return Ok(service.Fix(document, request.Standards, request.Title, request.Author, request.Language));
    }

    // Podgląd jednego wystąpienia (pozycja z findings[]) z raportu trzymanego w pamięci API: obrazy jako data URI, nic nie jest zapisywane.
    [HttpGet("reports/{reportId}/findings/{index:int}/preview")]
    [ProducesResponseType(typeof(FindingPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<FindingPreviewDto> Preview(string reportId, int index) => Ok(service.Preview(reportId, index));

    // Raport do pobrania: docx (domyślnie) albo pdf; w treści JSON lista wystąpień oznaczonych jako zweryfikowane.
    [HttpPost("reports/{reportId}/export")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Export(string reportId, [FromBody] ExportReportRequest request, [FromQuery] string format = "docx")
    {
        var exported = service.Export(reportId, request, format);
        return File(exported.Content, exported.ContentType, exported.FileName);
    }

    // Wariant dla integracji: od razu poprawiony plik, liczba napraw w nagłówku X-Applied-Fixes.
    [HttpPost("fix/file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [Produces(DocxContentType)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FixFile([FromForm] FixDocumentRequest request, CancellationToken ct)
    {
        var document = await ReadAsync(request.File!, ct);
        var result = service.Fix(document, request.Standards, request.Title, request.Author, request.Language);
        Response.Headers["X-Applied-Fixes"] = result.Applied.Count.ToString();
        return File(Convert.FromBase64String(result.DocumentBase64), DocxContentType, result.FileName);
    }

    private static async Task<WordDocument> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > WordDocument.MaxSizeBytes)
        {
            throw new DocumentRejectedException($"Plik jest za duży. Dopuszczalny rozmiar to {WordDocument.MaxSizeBytes / (1024 * 1024)} MB.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return WordDocument.Create(file.FileName, buffer.ToArray());
    }
}
