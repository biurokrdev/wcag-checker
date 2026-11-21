using System.Text.Json.Serialization;
using D2.Wcag;
using D2.Wcag.Api.Comments;
using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Reports;
using D2.Wcag.Api.Validation;
using D2.Wcag.DependencyInjection;
using D2.Wcag.Standards;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Profil (które standardy domyślnie) z sekcji "Wcag"; dodatkowe wersje standardu firmowego z "Wcag:CorporateStandards"
// trafiają do katalogu obok wbudowanych 1.9 i 2.0. AddD2Wcag rejestruje katalog, walidator i naprawę jako singletony.
builder.Services.Configure<WcagValidationProfile>(builder.Configuration.GetSection(WcagProfileSection.Name));
foreach (var standard in builder.Configuration.GetSection(WcagProfileSection.CorporateStandards).Get<List<CorporateStandard>>() ?? [])
{
    builder.Services.AddDocumentStandard(standard);
}

// Parametry standardu „Formatowanie z Galerii stylów” (style/1.0) z "Wcag:StyleGallery"; ten sam klucz zastępuje wbudowany.
builder.Services.Configure<StyleGalleryOptions>(builder.Configuration.GetSection(WcagProfileSection.StyleGallery));
builder.Services.AddSingleton<IDocumentStandard>(sp => new StyleGalleryStandard(sp.GetRequiredService<IOptions<StyleGalleryOptions>>().Value));

builder.Services.AddD2Wcag();
builder.Services.AddCommentExport();
builder.Services.Configure<ReportStoreOptions>(builder.Configuration.GetSection(ReportStoreOptions.Section));
builder.Services.AddSingleton<IReportStore, ReportStore>();
builder.Services.AddScoped<IDocumentCheckService, DocumentCheckService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

// Błędy techniczne to ProblemDetails z polskim tytułem; wynik walidacji (także negatywny) nigdy nie idzie tą drogą.
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        DocumentRejectedException => (StatusCodes.Status400BadRequest, "Nie można przyjąć pliku."),
        StandardNotFoundException => (StatusCodes.Status400BadRequest, "Nieznany standard."),
        UnreadableDocumentException => (StatusCodes.Status422UnprocessableEntity, "Dokumentu nie da się odczytać."),
        ReportNotFoundException => (StatusCodes.Status404NotFound, "Raport wygasł."),
        BadHttpRequestException bad => (bad.StatusCode, "Nieprawidłowe żądanie."),
        _ => (StatusCodes.Status500InternalServerError, "Błąd serwera."),
    };

    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = ex is null || status == StatusCodes.Status500InternalServerError ? null : ex.Message,
        Instance = ctx.Request.Path,
    });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
