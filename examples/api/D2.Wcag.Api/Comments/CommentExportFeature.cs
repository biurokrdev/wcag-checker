namespace D2.Wcag.Api.Comments;

// Eksport kopii dokumentu z komentarzami przy zgłoszeniach. Zaczep przygotowany przez moduł wspólny: Program.cs woła
// AddCommentExport, a usługi i kontroler tej funkcji (POST reports/{reportId}/commented-document, ten sam prefiks
// trasy api/wcag-documents) dokłada się w tym katalogu. Na razie nic nie rejestruje.
public static class CommentExportFeature
{
    public static IServiceCollection AddCommentExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
