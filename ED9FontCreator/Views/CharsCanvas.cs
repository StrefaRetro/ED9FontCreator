using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using ED9FontCreator.ViewModels;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ED9FontCreator.Views;

public partial class CharsCanvas
{
    private class CharsDraw : ICustomDrawOperation
    {
        private List<FntChar>? _chars;
        private FontSettings? _fontSettings;
        private readonly bool _isPreview;
        private readonly bool _isDrawBackground;
        public void Dispose()
        {
            _chars = null;
            _fontSettings = null;
        }

        public CharsDraw(Rect bounds, List<FntChar> chars, FontSettings fontSettings,
            bool isPreview, bool isDrawBackground)
        {
            this.Bounds = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            this._chars = chars;
            this._fontSettings = fontSettings;
            this._isPreview = isPreview;
            this._isDrawBackground = isDrawBackground;
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public Rect Bounds { get; }

        public void Render(ImmediateDrawingContext context)
        {
            if (_fontSettings == null || _chars == null) return;

            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();

            Enum.TryParse(_fontSettings.FontWeight, out SKFontStyleWeight fontWeight);
            Enum.TryParse(_fontSettings.FontStyle, out SKFontStyleSlant fontStyle);

            using var typeface = SKTypeface.FromFamilyName(_fontSettings.FontName, fontWeight, SKFontStyleWidth.Normal, fontStyle);
            using var symbolTypeface = SKTypeface.FromFamilyName("Segoe UI Symbol", fontWeight, SKFontStyleWidth.Normal, fontStyle);

            using var paint = new SKPaint();
            paint.TextSize = _fontSettings.FontSize;
            paint.IsAntialias = true;
            if (_isPreview)
            {
                paint.Color = new SKColor(255, 255, 255);
                if (_isDrawBackground)
                {
                    using var background = new SKPaint();
                    background.Color = new SKColor(244, 0, 161);
                    Draw(_chars, canvas, paint, typeface, symbolTypeface, background);
                }
                else
                {
                    Draw(_chars, canvas, paint, typeface, symbolTypeface);
                }
                canvas.Restore();
                return;
            }
            //red
            paint.Color = new SKColor(255, 0, 0);
            var filteredChars = _chars.Where(c => c.ColorChannel == 0x200).ToList();
            Draw(filteredChars, canvas, paint, typeface, symbolTypeface);
            //green
            paint.BlendMode = SKBlendMode.Screen;
            paint.Color = new SKColor(0, 255, 0);
            filteredChars = _chars.Where(c => c.ColorChannel == 0x100).ToList();
            Draw(filteredChars, canvas, paint, typeface, symbolTypeface);

            canvas.Restore();
        }

        private void Draw(List<FntChar> filteredChars, SKCanvas canvas, SKPaint paint, SKTypeface typeface, SKTypeface symbolTypeface, SKPaint? background = null)
        {
            short x = 0, y = 0, highest = 0;
            var pixelRect = new SKRect();
            foreach (var c in filteredChars)
            {
                var text = c.ReplacedChar.ToString();
                paint.Typeface = typeface.ContainsGlyph(c.ReplacedChar) ? typeface : symbolTypeface;
                c.Width = (short)Math.Ceiling(paint.MeasureText(text, ref pixelRect));
                c.PixelWidth = (short)Math.Ceiling(pixelRect.Width);
                c.PixelHeight = pixelRect.IsEmpty ? c.Width : (short)Math.Ceiling(pixelRect.Height);
                c.XOffset = (short)Math.Ceiling(pixelRect.Left);
                c.YOffset = (short)Math.Ceiling(pixelRect.Top - paint.FontMetrics.Ascent);
                c.MaxWidth = c.Width > c.PixelWidth ? c.Width : c.PixelWidth;
                short drawXOffset = (short)(c.XOffset > 0 ? 0 : c.XOffset);
                if (x + c.MaxWidth > Bounds.Width)
                {
                    x = 0;
                    y += (short)(highest + 1);  //+1防止过于拥挤
                    highest = 0;
                }

                if (highest < c.PixelHeight)
                    highest = c.PixelHeight;

                if (_isPreview)
                {
                    //x += (short)(fntSettings.FntXOffset - c.XOffset);
                    y += c.YOffset;
                }

                if (background != null)
                {
                    background.Color = new SKColor(255, 255, 255, 150);
                    canvas.DrawRect(x, y, c.MaxWidth, c.PixelHeight, background);
                    background.Color = new SKColor(244, 0, 161);
                    canvas.DrawRect(x + c.XOffset - drawXOffset, y, c.PixelWidth, c.PixelHeight, background);
                }

                canvas.DrawText(text, new SKPoint(x - drawXOffset, y - pixelRect.Top), paint);

                if (_isPreview)
                {
                    y -= c.YOffset;
                }

                c.X = x;
                c.Y = y;

                x += (short)(c.MaxWidth + 1); //+1防止过于拥挤
            }
        }
    }

    static CharsCanvas()
    {
        ClipToBoundsProperty.OverrideDefaultValue(typeof(bool), true);
        IsHitTestVisibleProperty.OverrideDefaultValue(typeof(bool), true);
        FocusableProperty.OverrideDefaultValue(typeof(bool), true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CharsProperty)
            CharsPropertyChanged();
    }

    private void CharsPropertyChanged()
    {
        InvalidateMeasure();
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Black, null, new Rect(Bounds.Size));

        if (Chars is not { Count: > 0 }) return;
        var draw = new CharsDraw(Bounds, Chars, Font, IsPreview, IsShowCharBackground);
        context.Custom(draw);
        //Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }
}