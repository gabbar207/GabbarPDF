using GabbarPDF;
var pdf = new PdfBuilder();
pdf.AddPage(p =>
{
    p.Hindi("कितने पीडीएफ थे?", 100, 750, 48);
    p.Text("बस एक... GabbarPDF!", 100, 680, 32, "1 0 0");
    p.Rectangle(80, 600, 450, 120, "1 0 0", true);
    p.Table(100, 550, new[]
    {
        new[] { "नाम",        "गब्बर सिंह" },
        new[] { "काम",        "PDF बनाना" },
        new[] { "कौशल",      "100% देसी" },
        new[] { "डायलॉग",     "जो डर गया... समझो मर गया!" }
    });
});
File.WriteAllBytes("GabbarPDF.pdf", pdf.Build());
