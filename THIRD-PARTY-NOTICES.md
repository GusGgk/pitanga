# Third-party notices

Pitanga is licensed under the [MIT License](LICENSE). It is built on and redistributes the following
third-party software. Each component remains under its own license.

| Component | Version | License | Source |
|---|---|---|---|
| .NET runtime, WPF (bundled, self-contained) | 10.0 | MIT | https://github.com/dotnet/runtime · https://github.com/dotnet/wpf |
| Magick.NET (Magick.NET-Q8-AnyCPU, Magick.NET.Core) | 14.17.2 | Apache-2.0 | https://github.com/dlemstra/Magick.NET |
| ImageMagick and its native libraries (via Magick.NET) | 7.1.2-32 | ImageMagick License + see below | https://imagemagick.org |
| PdfPig | 0.1.16 | Apache-2.0 | https://github.com/UglyToad/PdfPig |
| Open XML SDK (DocumentFormat.OpenXml, .Framework) | 3.5.1 | MIT | https://github.com/dotnet/Open-XML-SDK |

## ImageMagick native libraries

Magick.NET ships a native build of ImageMagick together with these libraries: aom, brotli, bzip2, cairo,
libcroco, libde265, OpenEXR, libffi, fontconfig, FreeType, FriBidi, gdk-pixbuf, GLib, HarfBuzz, libheif,
Highway, Imath, libjpeg-turbo, libjxl, Little CMS, liblqr, liblzma, OpenH264, OpenJPEG, OpenJPH, Pango,
pixman, libpng, libraqm, LibRaw, librsvg, LibTIFF, libwebp, libxml2, libzip, zlib and Zstandard.

The full copyright and license text of every one of them is in
[`licenses/Magick.NET-Notice.txt`](licenses/Magick.NET-Notice.txt), copied unmodified from the
Magick.NET package. Some of these libraries (e.g. cairo, GLib, Pango, gdk-pixbuf, librsvg, FriBidi, liblqr,
libde265) are licensed under the LGPL; their source code is available from their upstream projects and from
the Magick.NET repository linked above.

## Not bundled

- **Microsoft Office** — used only if it is already installed, through its own automation interface.
- **FFmpeg** — optional; used only if `ffmpeg.exe` is placed next to `Pitanga.exe` or is on `PATH`.
- **LibreOffice** — optional fallback for documents when Office is not installed.

## Inspiration

The interaction (drag a file, tap Shift, pick from a radial menu) is inspired by
Tangerine for macOS, by Thameem. Pitanga is an independent implementation for
Windows and shares no code with it.
