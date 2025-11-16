using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace GabbarPDF
{
    public class PdfBuilder
    {
        private readonly MemoryStream _ms = new();
        private readonly List<string> _objects = new();
        private int _objCount = 0;
        private readonly List<int> _pages = new();

        public PdfBuilder() => Write("%PDF-1.7\n%����\n");

        private void Write(string s) => _ms.Write(Encoding.ASCII.GetBytes(s));

        private int AddObject(string content)
        {
            _objCount++;
            Write($"{_objCount} 0 obj\n<<{content}>>\nendobj\n");
            _objects.Add($"{_objCount} 0 obj\n<<{content}>>\nendobj\n");
            return _objCount;
        }

        private int AddStream(string data, string extra = "")
        {
            _objCount++;
            var obj = $"{_objCount} 0 obj\n<<{extra}/Length {data.Length}>>\nstream\n{data}\nendstream\nendobj\n";
            Write(obj);
            _objects.Add(obj);
            return _objCount;
        }

        public PdfBuilder AddPage(Action<Page> draw)
        {
            var page = new Page(this);
            draw(page);
            _pages.Add(page.PageId);
            page.Finish();
            return this;
        }

        public byte[] Build()
        {
            var fontId = EmbedDevanagariFont();
            var resourcesId = AddObject($"/Font << /F1 10 0 R /F2 {fontId} 0 R >> /ProcSet [/PDF /Text /ImageB /ImageC /ImageI]");
            var pagesId = AddObject($"/Type /Pages /Kids [{string.Join(" ", _pages.Select(p => p + " 0 R"))}] /Count {_pages.Count}");
            var catalogId = AddObject($"/Type /Catalog /Pages {pagesId} 0 R");
            var xref = (int)_ms.Position;
            Write("xref\n");
            Write($"0 {_objCount + 1}\n");
            Write("0000000000 65535 f \n");
            for (int i = 0; i < _objCount; i++) Write("0000000010 00000 n \n");
            Write($"trailer << /Size {_objCount + 1} /Root {catalogId} 0 R >>\n");
            Write("startxref\n");
            Write(xref + "\n%%EOF");
            return _ms.ToArray();
        }

        private int EmbedDevanagariFont()
        {
            var fontBytes = GetFontBytes();
            var compressed = Convert.ToBase64String(Compress(fontBytes));
            var streamId = AddStream(compressed, $"/Length1 {fontBytes.Length} /Filter /FlateDecode");
            var descId = AddObject("/Type /FontDescriptor /FontName /GabbarHindi /Flags 4 /FontBBox [0 0 1000 1000] /Ascent 900 /Descent -100 /FontFile2 " + streamId + " 0 R");
            return AddObject("/Type /Font /Subtype /TrueType /BaseFont /GabbarHindi /FontDescriptor " + descId + " 0 R /FirstChar 32 /LastChar 255 /Widths 12 0 R");
        }

        private byte[] GetFontBytes()
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = "GabbarPDF.Fonts.NotoSansDevanagari-Regular.ttf";
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new Exception($"Font not found! Expected: {resourceName}\nAvailable resources: {string.Join(", ", asm.GetManifestResourceNames())}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private byte[] Compress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                input.CopyTo(gzip);
            return output.ToArray();
        }

        public class Page
        {
            internal int PageId { get; }
            private readonly PdfBuilder _pdf;
            private readonly StringBuilder _content = new();

            internal Page(PdfBuilder pdf)
            {
                _pdf = pdf;
                _content.Append("BT\n");
                PageId = _pdf.AddObject("/Type /Page /Parent 1 0 R /Contents [[CONTENT]] 0 R /Resources 2 0 R /MediaBox [0 0 595 842]");
            }

            public Page Text(string text, float x, float y, float size = 16, string color = "0 0 0")
            {
                _content.Append($"{color} rg /F1 {size} Tf {x} {y} Td ({Escape(text)}) Tj\n");
                return this;
            }

            public Page Hindi(string hindi, float x, float y, float size = 18)
            {
                _content.Append($"0 0 1 rg /F2 {size} Tf {x} {y} Td ({Escape(hindi)}) Tj\n");
                return this;
            }

            public Page Rectangle(float x, float y, float w, float h, string color = "0 0 0", bool fill = false)
            {
                _content.Append($"{color} {(fill ? "rg" : "RG")}\n{x} {y} {w} {h} re {(fill ? "f" : "S")}\n");
                return this;
            }

            public Page Table(float x, float y, string[][] data, float cellW = 120, float cellH = 40)
            {
                for (int r = 0; r < data.Length; r++)
                    for (int c = 0; c < data[r].Length; c++)
                    {
                        float px = x + c * cellW;
                        float py = y - r * cellH;
                        Rectangle(px, py, cellW, cellH, "0 0 0", false);
                        Hindi(data[r][c], px + 10, py - cellH + 15, 14);
                    }
                return this;
            }
            internal void Finish()
            {
                var stream = _content.ToString() + "ET\n";
                var streamId = _pdf.AddStream(stream);
                var lastObj = _pdf._objects[^1];
                _pdf._objects[^1] = lastObj.Replace("[[CONTENT]]", streamId.ToString());
            }
            private static string Escape(string s) => s.Replace("(", "\\(").Replace(")", "\\)").Replace("'", "\\'");
        }
    }
}