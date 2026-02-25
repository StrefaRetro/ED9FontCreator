using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using ED9FontCreator.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using SkiaSharp;

namespace ED9FontCreator.ViewModels
{
    public partial class MainWindowViewModel
    {
        public MainWindowViewModel()
        {
#if DEBUG
            _fntPath = @"E:\Games\ED9A\asset\common\font\font_0.fnt";
#endif
            OutDir = Path.Combine(Environment.CurrentDirectory, "out");
            if (!Directory.Exists(OutDir))
                Directory.CreateDirectory(OutDir);
            if (File.Exists(ReplaceTxtFile))
                ReplaceText = File.ReadAllText(ReplaceTxtFile);
        }

        private int ToNextPOT(int value)
        {
            int v = 1;
            while (v < value) v <<= 1;
            return v;
        }

        [RelayCommand]
        private void AnalyzeFntFile()
        {
            try
            {
                if (!FntHelper.GetFnt(FntPath, out var fnt))
                {
                    DrawChars = null;
                    this.Fnt = null;
                    ShowInfo("Analysis failed", InfoBarState.Error);
                    return;
                }
                this.Fnt = fnt;
                DrawChars = null;
                ShowInfo("Analysis successful", InfoBarState.Success);
            }
            finally
            {
                CanExportFont = false;
            }
        }

        [RelayCommand]
        private void SearchFntChar(string? text)
        {
            if (text == null || Fnt == null) return;
            var code = text[0];
            SearchedFntChar = Fnt.Chars.FirstOrDefault(x => x.Code == code);
        }

        [RelayCommand]
        private void PreviewText(string? text)
        {
            if (text == null) return;
            FntHelper.InitReplaceGroup(ReplaceText);
            var temp = new List<FntChar>();
            foreach (var c in text.ToCharArray())
            {
                temp.Add(new FntChar
                {
                    Char = c,
                    ReplacedChar = FntHelper.Replace(c, IsSimplifiedChinese),
                    ColorChannel = 0x200,
                    Type = 1
                });
            }
            PreviewChars = temp;
        }

        [RelayCommand]
        private void DefaultUserSetting()
        {
            FontSettings = new();
        }

        [RelayCommand(CanExecute = nameof(Analysed))]
        private void GenerateChars()
        {
            try
            {
                FntHelper.InitReplaceGroup(ReplaceText);
                var temp = Fnt!.Chars.Select(c => new FntChar
                {
                    Char = c.Char,
                    ReplacedChar = FntHelper.Replace(c.Char, IsSimplifiedChinese),
                    ColorChannel = c.ColorChannel,
                    Offset = c.Offset,
                    Type = 1 // Force Type 1 (Proportional) according to TwnKey
                }).ToList();

                if (AddPolishChars)
                {
                    var polishChars = "ĄĆĘŁŃÓŚŹŻąćęłńóśźż";
                    foreach (var c in polishChars)
                    {
                        if (!temp.Any(x => x.Char == c))
                        {
                            temp.Add(new FntChar
                            {
                                Char = c,
                                ReplacedChar = c,
                                ColorChannel = 0x200,
                                Type = 1
                            });
                        }
                    }
                }

                DrawChars = temp;
                ShowInfo("Character generation complete", InfoBarState.Success);
                CanExportFont = true;
            }
            catch (Exception e)
            {
                ShowInfo(e.Message, InfoBarState.Error);
                CanExportFont = false;
                DrawChars = null;
            }
        }

		[RelayCommand(CanExecute = nameof(CanExportFont))]
				private async void ExportFont()
				{
					try
					{
						if (DrawChars == null || DrawChars.Count == 0)
							throw new Exception("Generate characters first");

						int texWidth = 4096;
						int texHeight = 4096;

						using var surface = SKSurface.Create(new SKImageInfo(texWidth, texHeight));
						var canvas = surface.Canvas;
						canvas.Clear(SKColors.Transparent);

						using var paint = new SKPaint();

						SKFontStyleWeight weight = SKFontStyleWeight.Normal;
						if (Enum.TryParse(FontSettings.FontWeight, out SKFontStyleWeight parsedWeight)) weight = parsedWeight;

						using var typeface = SKTypeface.FromFamilyName(FontSettings.FontName, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
						using var symbolTypeface = SKTypeface.FromFamilyName("Segoe UI Symbol", weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

                        // Add fallback fonts for Latin/Polish characters
                        var fallbackFontFamilies = new[] { "Segoe UI", "Arial", "Tahoma", "Times New Roman" };
                        var fallbackTypefaces = fallbackFontFamilies.Select(f => SKTypeface.FromFamilyName(f, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)).ToList();

						paint.TextSize = FontSettings.FontSize;
						paint.IsAntialias = true;
						paint.Color = SKColors.White;

						// Shadow settings
						bool drawShadow = true;
						float shadowOffsetX = 2.0f;
						float shadowOffsetY = 2.0f;
						byte shadowAlpha = 180;

						using var shadowPaint = new SKPaint();
						shadowPaint.TextSize = paint.TextSize;
						shadowPaint.IsAntialias = true;
						shadowPaint.Color = SKColors.Black.WithAlpha(shadowAlpha);

						// Use Top/Bottom instead of Ascent/Descent to cover full glyph height including accents
						float fontAscent = -paint.FontMetrics.Top;
						float fontDescent = paint.FontMetrics.Bottom;

                        // Add extra padding at the top to prevent artifacts/bleeding above tall characters (Caps/Numbers)
                        // This shifts the glyph down inside the texture slot, ensuring the top rows are transparent.
                        int extraTopPadding = FontSettings.TopPadding;

						short lineHeight = (short)Math.Ceiling(fontAscent + fontDescent + shadowOffsetY + 6 + extraTopPadding);
						// Align lineHeight to 4 bytes (BC7 block size) to prevent vertical bleeding
						lineHeight = (short)((lineHeight + 3) & ~3);

						short currentX = 0;
						short currentY = 0;
                        // Increased minimum padding to 32 to prevent any BC7 compression bleeding or artifacts
                        int texturePadding = Math.Max(FontSettings.Padding, 32);

						var charList = DrawChars.ToList();
						charList.Sort((x, y) => x.Code.CompareTo(y.Code));

						var pixelRect = new SKRect();

						foreach (var c in charList)
						{
                            SKTypeface usedTypeface = typeface;
                            if (!typeface.ContainsGlyph(c.ReplacedChar))
                            {
                                usedTypeface = fallbackTypefaces.FirstOrDefault(tf => tf.ContainsGlyph(c.ReplacedChar)) ?? symbolTypeface;
                            }
							paint.Typeface = usedTypeface;
							shadowPaint.Typeface = usedTypeface;

							string textToDraw = c.ReplacedChar.ToString();

							// Advance: logical width (how much to move the cursor)
							float advanceWidth = paint.MeasureText(textToDraw);

							// Bounds: where pixels are
							paint.MeasureText(textToDraw, ref pixelRect);

							// --- SPACE FIX ---
							if (c.Char == ' ' || pixelRect.Width <= 0)
							{
								c.XOffset = 0; c.YOffset = 0;
								c.PixelWidth = 0; c.PixelHeight = 0;
								c.Width = (short)Math.Ceiling(advanceWidth);
								c.MaxWidth = (short)Math.Ceiling(advanceWidth);
								c.X = 0; c.Y = 0;
								continue;
							}

							// --- OFFSET CALCULATION (Anti-Clip) ---
							// If the letter protrudes to the left (e.g. 'j', 'f'), move it to the right on the texture.
							float visualLeft = pixelRect.Left;
							// Round correction to integer to ensure pixel-perfect rendering
							float xCorrection = (visualLeft < 0) ? (float)Math.Ceiling(-visualLeft) : 0;

							float drawX = currentX + xCorrection + 1;
                            // Add extra top padding to drawY to shift the character down
							float drawY = currentY + fontAscent + extraTopPadding;

							// --- TEXTURE FRAME DIMENSIONS ---
                            // Fix: Use pixelRect.Right instead of Width to account for positive Left bearings correctly.
                            // If we use Width, we underestimate the right edge when Left > 0, causing clipping or bleeding.
							float contentRight = drawX + pixelRect.Right + shadowOffsetX;
							float contentWidth = contentRight - currentX;

                            // Increased safety margin from +2 to +4
							c.PixelWidth = (short)Math.Ceiling(contentWidth + 4);
							c.PixelHeight = lineHeight;
							c.Width = c.PixelWidth;

							// --- FIX GAP AFTER 'J' ---
							// Previously we added xCorrection here, which created a hole.
							// Now we take pure AdvanceWidth + small margin (1.5px).
							// xCorrection is only for drawing on the texture, it does not increase the logical spacing.
							float calculatedAdvance = advanceWidth + 0.5f;

							// SAFEGUARD:
							// Check if "Pure Advance" is not smaller than "Physical pixels minus offset".
							// I.e.: Advance must be at least enough to cover the drawn letter (excluding empty space on the left).
							// float physicalEnd = (c.PixelWidth - xCorrection);
							// float safeAdvance = Math.Max(calculatedAdvance, physicalEnd);

							c.MaxWidth = (short)Math.Ceiling(calculatedAdvance);

							// Offsets 0 (game engine)
							// If we move the letter on the texture (xCorrection), we must move it back when rendering
							c.XOffset = (short)-Math.Ceiling(xCorrection);
							c.YOffset = 0;

							// New Line
							if (currentX + c.PixelWidth + texturePadding > texWidth)
							{
								currentX = 0;
								currentY += (short)(lineHeight + texturePadding);
								drawX = xCorrection + 1;
								drawY = currentY + fontAscent + extraTopPadding;
							}

							// Drawing (Round positions to avoid subpixel blurring)
							if (drawShadow)
							{
								canvas.DrawText(textToDraw, (float)Math.Round(drawX + shadowOffsetX), (float)Math.Round(drawY + shadowOffsetY), shadowPaint);
							}
							canvas.DrawText(textToDraw, (float)Math.Round(drawX), (float)Math.Round(drawY), paint);

							c.X = currentX;
							c.Y = currentY;

							currentX += (short)(c.PixelWidth + texturePadding);
							// Align currentX to 4 bytes (BC7 block size) to prevent compression bleeding
							currentX = (short)((currentX + 3) & ~3);
						}

                        // Dispose fallbacks
                        foreach(var fb in fallbackTypefaces) fb.Dispose();

						// Save PNG
						using var image = surface.Snapshot();
						using var data = image.Encode(SKEncodedImageFormat.Png, 100);
						var pngFile = Path.Combine(OutDir, Path.GetFileNameWithoutExtension(FntPath) + ".png");
						using (var stream = File.OpenWrite(pngFile))
						{
							data.SaveTo(stream);
						}

						// Save FNT
						DrawChars = charList;
						if (!ExportFnt())
							throw new Exception("Export font failed");

						// Conversion DDS
						if (!(await PNG2DDS(pngFile)))
							throw new Exception("Font conversion failed");

		#if RELEASE
						File.Delete(pngFile);
		#endif
						ShowInfo("Font export complete (Padding Fix).", InfoBarState.Success);
					}
					catch (Exception e)
					{
						ShowInfo(e.Message, InfoBarState.Error);
					}
				}

        [RelayCommand]
        private void OpenOutDir()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = OutDir,
                UseShellExecute = true
            });
        }

        private async Task<bool> PNG2DDS(string png)
        {
            if (!File.Exists(png)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = "texconv.exe",
                Arguments = $"-y -nologo -ft dds -w 0 -h 0 -if CUBIC -f BC7_UNORM -m 1 -pmalpha -o \"{OutDir}\" -r:keep \"{png}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
            {
                ShowInfo($"Texconv Error: {error}", InfoBarState.Error);
                return false;
            }
            return true;
        }

        private bool ExportFnt()
        {
            try
            {
                if (DrawChars == null) throw new Exception();
                var temp = DrawChars.ToList();

                var file = Path.Combine(OutDir, Path.GetFileName(FntPath));
                if (File.Exists(file))
                    File.Delete(file);

                var count = (short)temp.Count;
                var dataLength = count * 24;
                var head = (byte[])Fnt!.Head.Clone();
                var countBytes = BitConverter.GetBytes(count);
                head[8] = countBytes[0];
                head[9] = countBytes[1];
                var lengthBytes = BitConverter.GetBytes(dataLength);
                head[36] = lengthBytes[0];
                head[37] = lengthBytes[1];
                head[38] = lengthBytes[2];
                head[39] = lengthBytes[3];

                using FileStream fs = new(file, FileMode.Create);
                fs.Write(head);
                foreach (var c in temp)
                {
                    fs.WriteInt(c.Code);
                    fs.WriteInt(c.Type);
                    fs.WriteShort(c.X);
                    fs.WriteShort(c.Y);

                    // CHANGE: TwnKey says 0xC (here MaxWidth) is simply Width (Frame Width)
                    fs.WriteShort(c.PixelWidth); // Frame width on texture

                    // CHANGE: 0xE (PixelHeight) is Height
                    fs.WriteShort(c.PixelHeight); // Frame height

                    fs.WriteShort(c.ColorChannel); // 0x100 / 0x200

                    // CHANGE: 0x12 (XOffset) - according to TwnKey this is kerning/spacing, set to 0 or small value
                    fs.WriteShort(c.XOffset);

                    // CHANGE: 0x14 (YOffset) - according to TwnKey set to 0, because engine is buggy
                    fs.WriteShort(0);

                    // CHANGE: 0x16 (Last Short) - according to TwnKey "distance to next char", Width + 2
                    fs.WriteShort(c.MaxWidth); // In code above I set this to PixelWidth + 2
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        public void ShowInfo(string text, InfoBarState state = InfoBarState.None)
        {
            InfoText = text;
            InfoState = state;
        }
    }
}