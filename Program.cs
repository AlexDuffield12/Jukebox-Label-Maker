using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Layout.Borders;

class Program
{
    static void Main(string[] args)
    {
        string csvPath = null!;

        if (args.Length > 0 && File.Exists(args[0]))
        {
            csvPath = args[0];
            Console.WriteLine($"Using dropped file: {csvPath}");
        }
        else
        {
            Console.WriteLine("Please drag and drop your CSV file into this window, then press Enter:");
            Console.Write("> ");
            csvPath = Console.ReadLine()?.Trim('"', ' ') ?? "";

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs.csv");
                Console.WriteLine($"No file provided. Using default: {csvPath}");
            }
        }

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"CSV file not found at: {csvPath}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        string docsPath = AppDomain.CurrentDomain.BaseDirectory;
        Directory.CreateDirectory(docsPath);

        string filePath = Path.Combine(docsPath, "TitleStrips.pdf");
        Console.WriteLine($"Will save PDF to: {filePath}");

        var stripsByDecade = new Dictionary<string, List<string>>();
        string[] lines = File.ReadAllLines(csvPath);

        if (lines.Length <= 1)
        {
            Console.WriteLine("The CSV file is empty or missing rows.");
            return;
        }

        Console.WriteLine("Generating jukebox title strips...\n");

        for (int i = 1; i < lines.Length; i++)
        {
            string[] parts = ParseCsvLine(lines[i]);
            if (parts.Length < 7) continue;

            string artist = parts[1].Trim();
            string title = parts[2].Trim();
            string album = parts[3].Trim();
            string year = parts[6].Trim();

            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
                continue;

            string strip = GetTitleStripText(artist, title, album, year);
            strip = Truncate(strip, 100);

            string decade = GetDecade(year);
            if (!stripsByDecade.ContainsKey(decade))
                stripsByDecade[decade] = new List<string>();

            stripsByDecade[decade].Add(strip);
            Console.WriteLine($"{decade}: {strip}");
        }

        Console.WriteLine("All title strips grouped by decade!");
        GeneratePdf(stripsByDecade, filePath);
        Console.WriteLine($"\nPDF created successfully at:\n{filePath}");
    }

    static string GetTitleStripText(string artist, string title, string album, string year)
    {
        string space = "  ";
        return $"{title}\n{artist + space + year}\n" +
               (string.IsNullOrWhiteSpace(album) ? "" : $"{album}\n");
    }

    static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "…";
    }

    static string GetDecade(string year)
    {
        if (int.TryParse(year, out int y))
        {
            int decadeStart = (y / 10) * 10;
            return $"{decadeStart}s";
        }
        return "Unknown";
    }

    static void GeneratePdf(Dictionary<string, List<string>> stripsByDecade, string filePath)
    {
        string? folder = Path.GetDirectoryName(filePath);

        if (string.IsNullOrWhiteSpace(folder))
            folder = AppDomain.CurrentDomain.BaseDirectory;

        Directory.CreateDirectory(folder);
        Console.WriteLine($"Writing PDF to: {filePath}");

        using (var writer = new PdfWriter(filePath))
        using (var pdf = new PdfDocument(writer))
        using (var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4))
        {
            doc.SetMargins(10, 10, 10, 10);

            float labelWidthPt = 7.5f * 28.35f;
            float labelHeightPt = 2.5f * 28.35f;
            int columns = 2;

            foreach (var decade in stripsByDecade.Keys.OrderBy(k => k))
            {
                List<string> strips = stripsByDecade[decade];
                Table table = new Table(columns);
                table.SetWidth(UnitValue.CreatePercentValue(100));

                for (int i = 0; i < strips.Count; i++)
                {
                    int row = i / columns;
                    bool isOffsetRow = row % 2 == 1;
                    bool isFirstInRow = i % columns == 0;

                    //Color bgColor = row % 2 == 0 ? ColorConstants.WHITE : new DeviceRgb(230, 230, 230);

                    Cell cell = new Cell()
                        .Add(new Paragraph(strips[i]).SetFontSize(10).SetMultipliedLeading(1.2f))
                        .SetHeight(labelHeightPt)
                        .SetWidth(labelWidthPt)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                        .SetBorder(new SolidBorder(ColorConstants.BLACK, 0.5f));

                    if (isOffsetRow && isFirstInRow)
                    {
                        cell.SetPaddingLeft(20);
                    }

                    table.AddCell(cell);
                }

                doc.Add(table);
                doc.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
            }
        }

        Console.WriteLine("PDF successfully created!");
        TryOpenPdf(filePath);
    }

    static void TryOpenPdf(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Opening PDF: {path}");
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                Console.WriteLine("PDF not found to open.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not open PDF automatically: {ex.Message}");
        }
    }

    static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        bool inQuotes = false;
        string current = "";

        foreach (char c in line)
        {
            if (c == '\"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                values.Add(current);
                current = "";
            }
            else
                current += c;
        }

        values.Add(current);
        return values.ToArray();
    }
}
