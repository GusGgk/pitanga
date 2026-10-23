using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.Interop;

namespace Pitanga.Menu;

/// <summary>
/// Janela do menu radial. Ela mesma é o alvo do drag &amp; drop OLE: quando aparece sob o cursor
/// durante um arraste do Explorer, recebe DragEnter (descobre o arquivo), DragOver (destaca a fatia)
/// e Drop (executa a ação). Assim não é preciso nenhuma API não documentada para saber o que está
/// sendo arrastado.
/// </summary>
public partial class RadialMenuWindow : Window
{
    const double MenuSize = 440; // DIPs (o mesmo valor do XAML)

    readonly DispatcherTimer _enterTimeout = new() { Interval = TimeSpan.FromMilliseconds(900) };
    IntPtr _hwnd;
    string[] _files = Array.Empty<string>();
    FileKind _kind;
    MenuMode _mode;
    bool _populated;
    int _generation, _thumbToken;

    public event Action<RadialItem, string[]>? ItemChosen;
    public bool IsOpen { get; private set; }

    public RadialMenuWindow()
    {
        InitializeComponent();
        _enterTimeout.Tick += (_, _) =>
        {
            _enterTimeout.Stop();
            if (!_populated) CloseMenu(); // Shift + arraste que não era de arquivo
        };
        Root.DragEnter += OnDragEnter;
        Root.DragOver += OnDragOver;
        Root.DragLeave += (_, _) => Wheel.HotIndex = -1;
        Root.Drop += OnDrop;
    }

    public void EnsureCreated()
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        WindowStyles.MakeNoActivate(_hwnd);
    }

    /// <summary>Chamado a cada toque em Shift durante um arraste.</summary>
    public void OpenOrToggle()
    {
        if (IsOpen)
        {
            ToggleMode();
            return;
        }

        Input.StickyKeysGuard.Suspend();
        NativeMethods.GetCursorPos(out var pt);
        double scale = ScreenUtil.ScaleAt(pt);
        int size = (int)Math.Round(MenuSize * scale);
        int x = pt.X - size / 2, y = pt.Y - size / 2;

        _generation++;
        IsOpen = true;
        _populated = false;
        _mode = MenuMode.Convert;
        _files = Array.Empty<string>();
        Wheel.Items = Array.Empty<RadialItem>();
        ResetStage();

        // Captura ANTES de mostrar a janela, senão capturaríamos o próprio menu.
        Backdrop.Source = ScreenUtil.CaptureBlurred(x, y, size, size, blurRadius: (int)Math.Round(9 * scale));

        Place(x, y, size, show: false);
        Show();
        Place(x, y, size, show: true); // reafirma posição/tamanho após eventual WM_DPICHANGED
        _enterTimeout.Start();
    }

    void Place(int x, int y, int size, bool show) =>
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, x, y, size, size,
            NativeMethods.SWP_NOACTIVATE | (show ? NativeMethods.SWP_SHOWWINDOW : 0));

    public void CloseMenu(bool chosen = false)
    {
        if (!IsOpen) return;
        IsOpen = false;
        _enterTimeout.Stop();
        Input.StickyKeysGuard.RestoreSoon();
        int generation = ++_generation;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(chosen ? 170 : 110));
        fade.Completed += (_, _) =>
        {
            if (generation != _generation) return; // reaberto durante a animação
            Hide();
            Backdrop.Source = null;
            Wheel.Items = Array.Empty<RadialItem>();
        };
        Stage.BeginAnimation(OpacityProperty, fade);

        var scaleOut = new DoubleAnimation(chosen ? 1.06 : 0.9, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        StageScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
        StageScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
    }

    // ------------------------------------------------------------------ drag & drop

    void OnDragEnter(object sender, DragEventArgs e)
    {
        if (IsOpen && !_populated && !TryPopulate(e.Data))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            CloseMenu();
            return;
        }
        Track(e);
    }

    void OnDragOver(object sender, DragEventArgs e) => Track(e);

    void Track(DragEventArgs e)
    {
        e.Handled = true;
        if (!IsOpen || !_populated)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        int index = Wheel.HitTestIndex(e.GetPosition(Wheel));
        Wheel.HotIndex = index;
        // Miolo = cancelar (cursor "proibido"; o OLE nem chama Drop).
        e.Effects = index < 0 ? DragDropEffects.None : SafeEffect(e.AllowedEffects);
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        int index = _populated ? Wheel.HitTestIndex(e.GetPosition(Wheel)) : -1;
        if (index < 0)
        {
            e.Effects = DragDropEffects.None;
            CloseMenu();
            return;
        }

        var item = Wheel.Items[index];
        var files = _files;
        e.Effects = SafeEffect(e.AllowedEffects);
        CloseMenu(chosen: true);

        // Drop precisa retornar rápido: o Explorer fica bloqueado esperando. A ação roda depois.
        Dispatcher.InvokeAsync(() => ItemChosen?.Invoke(item, files), DispatcherPriority.Background);
    }

    /// <summary>
    /// NUNCA devolvemos Move: com Shift pressionado o Explorer sugere "mover", e responder Move
    /// faria o Explorer apagar o original. Copy/Link dizem ao Explorer "não faça nada".
    /// </summary>
    static DragDropEffects SafeEffect(DragDropEffects allowed) =>
        (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy :
        (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.None;

    bool TryPopulate(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] dropped)
            return false;

        var files = dropped.Where(File.Exists).ToArray();
        if (files.Length == 0) return false;

        var kind = FileKinds.Classify(files[0]);
        files = files.Where(f => FileKinds.Classify(f) == kind).ToArray();
        if (!MenuCatalog.HasToolsMode(kind)) _mode = MenuMode.Convert;

        var items = MenuCatalog.For(kind, files, _mode);
        if (items.Count == 0)
        {
            // Antes o menu fechava em silêncio e parecia "não funcionar". Agora explica o motivo.
            var ext = Path.GetExtension(files[0]).ToLowerInvariant();
            var reason = kind is FileKind.Word or FileKind.Spreadsheet or FileKind.Presentation
                ? "Needs Microsoft Office or LibreOffice installed."
                : kind is FileKind.Video or FileKind.Audio
                    ? "Needs ffmpeg.exe next to Pitanga or on PATH."
                    : "This file type isn't supported yet.";
            UI.Toast.Show($"No actions for {ext} files", reason, UI.ToastKind.Info);
            return false;
        }

        _files = files;
        _kind = kind;
        _populated = true;
        _enterTimeout.Stop();
        ApplyItems(items);
        LoadCenter(files);
        AnimateIn();
        return true;
    }

    // ------------------------------------------------------------------ modos / conteúdo

    void ToggleMode()
    {
        if (!_populated)
        {
            _mode = _mode == MenuMode.Convert ? MenuMode.Tools : MenuMode.Convert;
            return;
        }
        if (!MenuCatalog.HasToolsMode(_kind)) return;

        _mode = _mode == MenuMode.Convert ? MenuMode.Tools : MenuMode.Convert;
        ApplyItems(MenuCatalog.For(_kind, _files, _mode));

        var bump = new DoubleAnimation(0.93, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut },
        };
        StageScale.BeginAnimation(ScaleTransform.ScaleXProperty, bump);
        StageScale.BeginAnimation(ScaleTransform.ScaleYProperty, bump);
    }

    void ApplyItems(IReadOnlyList<RadialItem> items)
    {
        Wheel.Items = items;
        ModeHint.Text = MenuCatalog.HasToolsMode(_kind)
            ? _mode == MenuMode.Convert ? "⇧ Tools" : "⇧ Convert"
            : "";
    }

    void LoadCenter(string[] files)
    {
        FileLabel.Text = files.Length > 1 ? $"{files.Length} files" : Path.GetFileNameWithoutExtension(files[0]);
        GlyphExt.Text = FileKinds.FormatId(files[0]);
        DocGlyph.Visibility = Visibility.Visible;
        ThumbBorder.Visibility = Visibility.Collapsed;
        if (_kind != FileKind.Image) return;

        int token = ++_thumbToken;
        string path = files[0];
        // Sem fallback para o ImageMagick: carregá-lo aqui deixaria ~50 MB presos no processo da bandeja.
        Task.Run(() => ImageIO.TryLoadThumbnail(path, 128, allowMagickFallback: false)).ContinueWith(t =>
        {
            if (token != _thumbToken || t.Result is null) return;
            ThumbBorder.Background = new ImageBrush(t.Result) { Stretch = Stretch.UniformToFill };
            ThumbBorder.Visibility = Visibility.Visible;
            DocGlyph.Visibility = Visibility.Collapsed;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ------------------------------------------------------------------ animações

    void ResetStage()
    {
        Stage.BeginAnimation(OpacityProperty, null);
        StageScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        StageScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Stage.Opacity = 0;
        StageScale.ScaleX = StageScale.ScaleY = 0.72;
    }

    void AnimateIn()
    {
        Stage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
        var pop = new DoubleAnimation(0.72, 1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut },
        };
        StageScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        StageScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }
}
