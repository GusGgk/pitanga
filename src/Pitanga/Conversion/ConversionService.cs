using Pitanga.Core;
using Pitanga.ImageOps;

namespace Pitanga.Conversion;

public sealed record ConversionResult(string OutputPath, string? Note = null);

/// <summary>Ponto único de despacho: (arquivos, alvo) → conversor certo. Tudo local, sem rede.</summary>
public static class ConversionService
{
    public static async Task<IReadOnlyList<ConversionResult>> ConvertAsync(IReadOnlyList<string> files, string target)
    {
        target = target.ToUpperInvariant();
        if (files.Count == 0) return Array.Empty<ConversionResult>();

        var kind = FileKinds.Classify(files[0]);
        var sameKind = files.Where(f => FileKinds.Classify(f) == kind).ToList();

        // Arquivo de 0 bytes (ex.: "Novo → Apresentação" no Explorer, nunca aberto): os conversores dariam
        // erros obscuros como "erro ao salvar". Melhor dizer o que realmente aconteceu.
        if (sameKind.FirstOrDefault(f => new FileInfo(f).Length == 0) is { } empty)
            throw new InvalidOperationException($"{Path.GetFileName(empty)} is empty (0 bytes) — open it and save some content first.");

        switch (target)
        {
            case "MERGE":
                return new[] { new ConversionResult(PdfConverter.Merge(sameKind)) };
            case "COMPRESS":
                return sameKind.Select(Compressor.Compress).ToList();
            case "PDF" when kind == FileKind.Image && sameKind.Count > 1:
                return new[] { new ConversionResult(ImageConverter.ToCombinedPdf(sameKind)) };
        }

        var results = new List<ConversionResult>();
        foreach (var file in sameKind)
        {
            switch (kind)
            {
                case FileKind.Image:
                    results.Add(new(ImageConverter.Convert(file, target)));
                    break;
                case FileKind.Pdf:
                    results.AddRange((await PdfConverter.ConvertAsync(file, target)).Select(p => new ConversionResult(p)));
                    break;
                case FileKind.Video or FileKind.Audio:
                    results.Add(new(await MediaConverter.ConvertAsync(file, target)));
                    break;
                case FileKind.Text when target == "DOCX":
                    results.Add(new(DocxWriter.FromText(file)));
                    break;
                // .docx → TXT é instantâneo via Open XML; não precisa acordar o Word.
                case FileKind.Word when target == "TXT" && Path.GetExtension(file).Equals(".docx", StringComparison.OrdinalIgnoreCase):
                    results.Add(new(DocxWriter.ToText(file)));
                    break;
                case FileKind.Word or FileKind.Spreadsheet or FileKind.Presentation or FileKind.Text:
                    results.AddRange((await OfficeConverter.ConvertAsync(file, target)).Select(p => new ConversionResult(p)));
                    break;
                default:
                    throw new NotSupportedException($"{Path.GetExtension(file)} → {target} is not supported.");
            }
        }
        return results;
    }
}
