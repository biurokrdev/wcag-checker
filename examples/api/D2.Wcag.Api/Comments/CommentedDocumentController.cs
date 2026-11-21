using Microsoft.AspNetCore.Mvc;

namespace D2.Wcag.Api.Comments;

// „Pobierz z komentarzami”: kopia sprawdzonego dokumentu z natywnymi komentarzami Worda w miejscach wystąpień.
// Ten sam prefiks trasy co WcagDocumentsController; treść żądania jest pusta – wszystko pochodzi z raportu w pamięci.
// Wygasły raport = 404 „Raport wygasł.”; odmowa eksportu = 422 „Nie można przygotować kopii z komentarzami.”
// (ProblemDetails). Typ i rozszerzenie pliku są takie jak sprawdzonego dokumentu (.docx, .dotx, .docm, .dotm).
[ApiController]
[Route("api/wcag-documents")]
public sealed class CommentedDocumentController(ICommentExportService service) : ControllerBase
{
    [HttpPost("reports/{reportId}/commented-document")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Download(string reportId)
    {
        var exported = service.Export(reportId);
        return File(exported.Content, exported.ContentType, exported.FileName);
    }
}
