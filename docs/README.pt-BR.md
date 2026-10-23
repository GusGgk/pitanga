# Pitanga

<img src="images/icon.png" width="96" alt="Pitanga">

> [Read in English](../README.md)

Um utilitário para Windows inspirado no Tangerine (macOS): **arraste um arquivo, toque em Shift** e um
menu radial de vidro aparece ao redor do cursor. Solte sobre uma fatia para converter ou editar —
tudo local, nada sai do seu PC.

## Como usar

1. Rode `Pitanga.exe` — ele fica na bandeja (ícone de pitanga, perto do relógio).
2. Na Área de Trabalho ou no Explorer, **comece a arrastar** um arquivo.
3. Com o botão ainda pressionado, **toque em Shift** → o menu aparece com o cursor no centro.
4. Arraste até a fatia e **solte**. O resultado aparece na mesma pasta.
   - Soltar no **miolo** cancela.
   - Em imagens, **toque Shift de novo** para alternar entre *Convert* e *Tools*.
   - `Esc` fecha o menu.

| Arquivo | Menu |
|---|---|
| PDF | DOCX · JPG · PNG · TXT (+ MERGE com vários PDFs) |
| Imagem — Convert | JPG · PNG · WEBP · HEIC · AVIF · BMP · TIFF · PDF · DOCX (menos o formato de origem) |
| Imagem — Tools | CROP · COMPRESS · METADATA · EDIT · ANNOTATE · ADD BG · REDACT |
| Word (DOCX, DOC, RTF, ODT) | PDF · DOCX · JPG · PNG · TXT *(precisa do Office ou LibreOffice; sem eles, só DOCX → TXT)* |
| Planilha (XLSX, XLS, CSV, ODS) | PDF · XLSX · CSV *(Office ou LibreOffice)* |
| Apresentação (PPTX, PPT, ODP) | PDF · PPTX · JPG · PNG *(Office ou LibreOffice)* |
| TXT / MD | DOCX · PDF |
| Vídeo / Áudio | MP4 · MOV · WEBM · GIF · MP3 / MP3 · WAV · M4A · FLAC · OGG *(precisa de `ffmpeg.exe`)* |

Se um tipo de arquivo não tem ações, o Pitanga avisa com uma notificação em vez de simplesmente não abrir.

Documentos do Office são convertidos pelo **próprio Word/Excel/PowerPoint instalado**, de forma invisível
(fidelidade total de layout, sem internet). A primeira conversão leva alguns segundos porque o app do Office
precisa iniciar. Se você já estiver com o PowerPoint aberto, ele não é fechado.

Arquivos gerados nunca sobrescrevem nada: `foto.jpg` → `foto.png`, `foto_Cropped.jpg`, `foto (2).png`…

## Instalar

Baixe o **`Pitanga-Setup-<versão>.exe`** em *Releases* e execute (não precisa de administrador).
Se aparecer "O Windows protegeu o computador", clique em **Mais informações → Executar assim mesmo**
(o instalador ainda não tem assinatura digital — veja [SIGNING.md](SIGNING.md)).

## Compilar

> **Clone em uma pasta com caminho curto** (ex.: `C:\dev\Pitanga`). Caminhos longos fazem a 2ª passada do
> compilador XAML falhar; o projeto recusa compilar nesse caso, com uma mensagem explicando.

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e, para o instalador, [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
.\build.ps1 -Version 1.0.0   # gera artifacts\: instalador, zip portátil e SHA256SUMS
```

Publicar uma versão: `git tag v1.0.0` e `git push origin v1.0.0` — o GitHub Actions compila e cria a release.
Microsoft Store: [STORE.md](STORE.md).

Linha de comando (testes, "Enviar para"):

```powershell
Pitanga.exe --convert JPG "C:\docs\contrato.pdf"
Pitanga.exe --tool CROP "C:\fotos\praia.jpg"
```

## Arquitetura

```
src/Pitanga/Input/GlobalInputHook.cs      Hooks WH_MOUSE_LL + WH_KEYBOARD_LL numa thread própria (zero lag no mouse)
Menu/RadialMenuWindow.*       Janela do menu = alvo de drag & drop OLE (DragEnter/Over/Drop)
Menu/RadialMenuControl.cs     Fatias desenhadas direto no DrawingContext (60 fps)
Menu/MenuCatalog.cs           Que opções aparecem para cada tipo de arquivo
Conversion/                   PDF (Windows.Data.Pdf + PdfPig), imagens (ImageMagick), DOCX (OpenXML), mídia (ffmpeg)
ImageOps/                     Ponte Magick ⇄ WPF, compressão, ajustes, composição de fundo
Tools/                        Janelas CROP, ADD BG, EDIT, ANNOTATE/REDACT, METADATA
UI/                           Janela estilo macOS, toasts, ícone de bandeja
Themes/Theme.xaml             Paleta, botões, segmented control, sliders, semáforos
```

### Decisões que importam

- **Como saber qual arquivo está sendo arrastado?** Não há API pública para "espiar" um drag do Explorer.
  O truque: ao detectar *botão esquerdo arrastando + Shift*, mostramos o menu **sob o cursor**; ele é um
  drop target OLE, então o próprio Windows entrega o `DataObject` com os caminhos no `DragEnter`.
- **Segurança do arquivo original:** com Shift pressionado o Explorer sugere *Mover*. O menu **sempre
  responde Copy/Link**, nunca Move — caso contrário o Explorer apagaria o original após o drop.
- **Bandeja leve (~50 MB) + processos worker:** o processo que fica aberto o dia todo só tem o hook,
  o menu radial, as notificações e o ícone. Conversões e janelas de ferramenta rodam em
  `Pitanga.exe --convert …` / `--tool …`, que termina ao final e devolve toda a memória
  (ImageMagick, Office, PDF e GPU nunca ficam presos na bandeja). Medido: 125 MB → 52 MB parado;
  depois de usar uma ferramenta, 225 MB → 52 MB.
- **Sem GPU na bandeja:** o processo da bandeja renderiza em software (`RenderMode.SoftwareOnly`),
  o que evita ~70 MB do driver Direct3D. Para a animação continuar fluida, nada ali usa efeitos:
  a sombra é um gradiente radial e o vidro é desfocado uma única vez por abertura.
- **Vidro de verdade com borda redonda:** o blur nativo do DWM só funciona em regiões retangulares
  (serrilhadas). Capturamos a área atrás do menu com GDI num buffer reaproveitado, aplicamos um
  box blur de 3 passadas (≈ gaussiano, poucos ms) e recortamos com `EllipseGeometry` antialiased.
- **Bandeja nativa:** `Shell_NotifyIcon` direto e menu em WPF no estilo macOS — sem WinForms
  (que custava ~16 MB só para existir).
- **Teclas de Aderência:** 5 toques em Shift abrem o aviso do Windows. Enquanto o menu está aberto, o
  Pitanga desliga só esse *atalho* (não o recurso) e o restaura 2 s depois de fechar — a mesma técnica
  que a Microsoft recomenda para jogos. Se você usa Teclas de Aderência ligadas, nada é alterado.
- **Per-Monitor DPI v2:** cursor, captura e posicionamento em pixels físicos; funciona em monitores mistos.
- **Prévia = exportação:** ADD BG monta a mesma árvore WPF para a prévia e para o `RenderTargetBitmap`
  em resolução total.

## Limitações conhecidas

- Cada conversão inicia um processo novo (~0,5 s a mais). É o preço de a bandeja não acumular memória.

- PDF → DOCX extrai **texto em parágrafos** (páginas escaneadas entram como imagem). Layout idêntico
  ao original exigiria um motor como o do Word.
- Escrita de HEIC depende do encoder incluído no Magick.NET; se indisponível, o arquivo é salvo em PNG.
- Janelas de apps rodando **como administrador** não disparam o menu (isolamento UIPI do Windows),
  a menos que o Pitanga também rode elevado.
- Tema escuro ainda não implementado.
