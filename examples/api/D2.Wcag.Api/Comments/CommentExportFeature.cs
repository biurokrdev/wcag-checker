using D2.Wcag.Docx;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D2.Wcag.Api.Comments;

// Eksport kopii dokumentu z komentarzami przy zgłoszeniach: zapis komentarzy z biblioteki (IWcagCommentWriter)
// i serwis, który bierze dokument oraz wystąpienia z raportu w pamięci. Kontroler tej funkcji
// (CommentedDocumentController, POST api/wcag-documents/reports/{reportId}/commented-document) leży w tym katalogu.
public static class CommentExportFeature
{
    public static IServiceCollection AddCommentExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IWcagCommentWriter, WcagCommentWriter>();
        services.TryAddScoped<ICommentExportService, CommentExportService>();
        return services;
    }
}
