using System.Diagnostics;
using System.Runtime.InteropServices;
using Pitanga.Core;

namespace Pitanga.Conversion;

/// <summary>
/// Documentos do Office (Word/Excel/PowerPoint e equivalentes ODF/RTF/CSV).
/// Usa o próprio Microsoft Office instalado, via automação COM, invisível e sem rede — é o único
/// jeito de ter fidelidade total de layout. Sem Office, tenta o LibreOffice em modo headless.
/// </summary>
public static class OfficeConverter
{
    static readonly Lazy<bool> HasWord = new(() => Type.GetTypeFromProgID("Word.Application") != null);
    static readonly Lazy<bool> HasExcel = new(() => Type.GetTypeFromProgID("Excel.Application") != null);
    static readonly Lazy<bool> HasPowerPoint = new(() => Type.GetTypeFromProgID("PowerPoint.Application") != null);
    static readonly Lazy<string?> LibreOffice = new(LocateLibreOffice);

    // O Office não gosta de conversões simultâneas na mesma instância.
    static readonly SemaphoreSlim Gate = new(1, 1);

    public static bool CanConvert(FileKind kind) => kind switch
    {
        FileKind.Word or FileKind.Text => HasWord.Value || LibreOffice.Value != null,
        FileKind.Spreadsheet => HasExcel.Value || LibreOffice.Value != null,
        FileKind.Presentation => HasPowerPoint.Value || LibreOffice.Value != null,
        _ => false,
    };

    public static async Task<IReadOnlyList<string>> ConvertAsync(string source, string target)
    {
        var kind = FileKinds.Classify(source);

        // Imagem de documento = documento → PDF temporário → renderizador nativo de PDF.
        if (target is "JPG" or "PNG")
        {
            var tempPdf = Path.Combine(Path.GetTempPath(), $"pitanga-{Guid.NewGuid():N}.pdf");
            try
            {
                await ExportAsync(source, kind, "PDF", tempPdf);
                return await PdfConverter.RenderAsync(tempPdf, jpeg: target == "JPG", nameSource: source);
            }
            finally
            {
                try { File.Delete(tempPdf); } catch { }
            }
        }

        var output = OutputPaths.ChangeExtension(source, "." + target.ToLowerInvariant());
        await ExportAsync(source, kind, target, output);
        return new[] { output };
    }

    static async Task ExportAsync(string source, FileKind kind, string target, string output)
    {
        source = Path.GetFullPath(source);
        output = Path.GetFullPath(output);

        bool viaOffice = kind switch
        {
            FileKind.Word or FileKind.Text => HasWord.Value,
            FileKind.Spreadsheet => HasExcel.Value,
            FileKind.Presentation => HasPowerPoint.Value,
            _ => false,
        };

        // O Office grava primeiro numa pasta temporária curta e local; depois movemos para o destino.
        // Evita falhas do Office com OneDrive, pastas protegidas e caminhos longos (>218 caracteres).
        var staging = Path.Combine(Path.GetTempPath(), $"pitanga-{Guid.NewGuid():N}{Path.GetExtension(output)}");

        await Gate.WaitAsync();
        try
        {
            if (viaOffice)
            {
                await RunSta(() =>
                {
                    switch (kind)
                    {
                        case FileKind.Word or FileKind.Text: ExportWord(source, target, staging); break;
                        case FileKind.Spreadsheet: ExportExcel(source, target, staging); break;
                        case FileKind.Presentation: ExportPowerPoint(source, target, staging); break;
                    }
                });
            }
            else if (LibreOffice.Value is { } soffice)
            {
                await ExportLibreOffice(soffice, source, target, staging);
            }
            else
            {
                throw new InvalidOperationException("Microsoft Office or LibreOffice is required for this conversion.");
            }

            if (!File.Exists(staging)) throw new InvalidOperationException($"{target} export produced no file.");
            File.Move(staging, output);
        }
        finally
        {
            Gate.Release();
            try { if (File.Exists(staging)) File.Delete(staging); } catch { }
        }
    }

    /// <summary>
    /// msoAutomationSecurityForceDisable. Por padrão, documentos abertos por automação rodam macros
    /// sem perguntar; para converter um arquivo qualquer (às vezes baixado da internet), nunca queremos isso.
    /// </summary>
    const int MacrosDisabled = 3;

    // ------------------------------------------------------------ Word

    static void ExportWord(string source, string target, string output)
    {
        dynamic word = CreateApp("Word.Application");
        dynamic? doc = null;
        try
        {
            word.Visible = false;
            word.DisplayAlerts = 0; // wdAlertsNone
            word.AutomationSecurity = MacrosDisabled;
            // Open(FileName, ConfirmConversions, ReadOnly, AddToRecentFiles)
            doc = word.Documents.Open(source, false, true, false);
            switch (target)
            {
                case "PDF": doc.ExportAsFixedFormat(output, 17); break;      // wdExportFormatPDF
                case "DOCX": doc.SaveAs2(output, 16); break;                 // wdFormatXMLDocument
                case "TXT": doc.SaveAs2(output, 7); break;                   // wdFormatUnicodeText
                default: throw new NotSupportedException($"Word → {target} is not supported.");
            }
        }
        finally
        {
            if (doc != null) { doc.Close(false); Release(doc); }
            QuitIfIdle((object)word, () => (int)word.Documents.Count);
        }
    }

    // ------------------------------------------------------------ Excel

    static void ExportExcel(string source, string target, string output)
    {
        dynamic excel = CreateApp("Excel.Application");
        dynamic? book = null;
        try
        {
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excel.AutomationSecurity = MacrosDisabled;
            // Open(FileName, UpdateLinks, ReadOnly)
            book = excel.Workbooks.Open(source, 0, true);
            switch (target)
            {
                case "PDF": book.ExportAsFixedFormat(0, output); break;      // xlTypePDF
                case "XLSX": book.SaveAs(output, 51); break;                 // xlOpenXMLWorkbook
                case "CSV": book.SaveAs(output, 62); break;                  // xlCSVUTF8 (planilha ativa)
                default: throw new NotSupportedException($"Excel → {target} is not supported.");
            }
        }
        finally
        {
            if (book != null) { book.Close(false); Release(book); }
            QuitIfIdle((object)excel, () => (int)excel.Workbooks.Count);
        }
    }

    // ------------------------------------------------------------ PowerPoint

    static void ExportPowerPoint(string source, string target, string output)
    {
        // PowerPoint é instância única: se o usuário estiver com ele aberto, recebemos a MESMA instância.
        dynamic ppt = CreateApp("PowerPoint.Application");
        dynamic? pres = null;
        int previousSecurity = (int)ppt.AutomationSecurity; // pode ser a instância do usuário: restaurar no fim
        try
        {
            ppt.AutomationSecurity = MacrosDisabled;
            // Open(FileName, ReadOnly=msoTrue, Untitled=msoFalse, WithWindow=msoFalse)
            pres = ppt.Presentations.Open(source, -1, 0, 0);
            if ((int)pres.Slides.Count == 0)
                throw new InvalidOperationException("The presentation has no slides, so there is nothing to export.");
            switch (target)
            {
                case "PDF":
                    // SaveCopyAs grava uma cópia sem "mudar" a apresentação aberta como somente-leitura;
                    // SaveAs fica como plano B (alguns arquivos falham em um dos dois).
                    try { pres.SaveCopyAs(output, 32); }                      // ppSaveAsPDF
                    catch (Exception) { pres.SaveAs(output, 32); }
                    break;
                case "PPTX": pres.SaveAs(output, 24); break;                 // ppSaveAsOpenXMLPresentation
                default: throw new NotSupportedException($"PowerPoint → {target} is not supported.");
            }
        }
        finally
        {
            if (pres != null) { pres.Close(); Release(pres); }
            try { ppt.AutomationSecurity = previousSecurity; } catch { }
            QuitIfIdle((object)ppt, () => (int)ppt.Presentations.Count);
        }
    }

    // ------------------------------------------------------------ LibreOffice

    static async Task ExportLibreOffice(string soffice, string source, string target, string output)
    {
        var filter = target switch
        {
            "PDF" => "pdf",
            "DOCX" => "docx",
            "TXT" => "txt:Text (encoded):UTF8",
            "XLSX" => "xlsx",
            "CSV" => "csv",
            "PPTX" => "pptx",
            _ => throw new NotSupportedException($"LibreOffice → {target} is not supported."),
        };
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pitanga-{Guid.NewGuid():N}")).FullName;
        try
        {
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "--headless", "--norestore", "--convert-to", filter, "--outdir", tempDir, source })
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start LibreOffice.");
            await p.WaitForExitAsync();
            var produced = Directory.GetFiles(tempDir).FirstOrDefault()
                           ?? throw new InvalidOperationException("LibreOffice produced no file.");
            File.Move(produced, output);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    static string? LocateLibreOffice()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var candidate = Path.Combine(root, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // ------------------------------------------------------------ COM helpers

    static dynamic CreateApp(string progId) =>
        Activator.CreateInstance(Type.GetTypeFromProgID(progId) ?? throw new InvalidOperationException($"{progId} is not installed."))!;

    /// <summary>Só fecha o app se não houver documentos do usuário abertos nele.</summary>
    static void QuitIfIdle(object app, Func<int> openCount)
    {
        try
        {
            if (openCount() == 0) ((dynamic)app).Quit();
        }
        catch { /* app já fechado */ }
        Release(app);
    }

    static void Release(object comObject)
    {
        try { Marshal.FinalReleaseComObject(comObject); } catch { }
    }

    /// <summary>Automação do Office deve rodar numa thread STA.</summary>
    static Task RunSta(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }) { IsBackground = true, Name = "Pitanga.Office" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
