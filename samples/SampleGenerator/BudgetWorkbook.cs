using D2.Wcag.Fixtures.Xlsx;

namespace D2.Wcag.SampleGenerator;

public static class BudgetWorkbook
{
    public static IReadOnlyList<SampleProfile> NonCompliantProfiles { get; } = [];

    public static byte[] Build(bool compliant) => CleanXlsxFixture.Build();
}
