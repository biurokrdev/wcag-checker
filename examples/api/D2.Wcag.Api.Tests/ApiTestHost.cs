using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace D2.Wcag.Api.Tests;

// Host testowy z nadpisaną konfiguracją; domyślne standardy są przypięte, żeby testy nie zależały od appsettings.json.
public class ApiTestHost(params (string Key, string Value)[] settings) : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))));
    }

    public static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static MultipartFormDataContent Form(byte[]? file, string fileName, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        if (file is not null)
        {
            form.Add(new ByteArrayContent(file), "file", fileName);
        }

        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }

        return form;
    }

    public static async Task<T> Body<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;
}

public sealed class DefaultApiTestHost() : ApiTestHost(
    ("Wcag:Standards:0", "wcag/2.1"),
    ("Wcag:Standards:1", "firmowy/1.9"),
    ("Wcag:DefaultLanguage", "pl-PL"));
