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
                    Type = 1 // Wymuszamy Type 1 (Proportional) zgodnie z TwnKey
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

						paint.TextSize = FontSettings.FontSize;
						paint.IsAntialias = true;
						paint.Color = SKColors.White;

						// Ustawienia cienia
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
						short lineHeight = (short)Math.Ceiling(fontAscent + fontDescent + shadowOffsetY + 6);
						// Align lineHeight to 4 bytes (BC7 block size) to prevent vertical bleeding
						lineHeight = (short)((lineHeight + 3) & ~3);

						short currentX = 0;
						short currentY = 0;
						int texturePadding = 8; // Increased padding to prevent bleeding

						var charList = DrawChars.ToList();
						charList.Sort((x, y) => x.Code.CompareTo(y.Code));

						var pixelRect = new SKRect();

						foreach (var c in charList)
						{
							var usedTypeface = typeface.ContainsGlyph(c.ReplacedChar) ? typeface : symbolTypeface;
							paint.Typeface = usedTypeface;
							shadowPaint.Typeface = usedTypeface;

							string textToDraw = c.ReplacedChar.ToString();

							// Advance: logiczna szerokość (o ile przesunąć kursor)
							float advanceWidth = paint.MeasureText(textToDraw);

							// Bounds: gdzie są piksele
							paint.MeasureText(textToDraw, ref pixelRect);

							// --- FIX SPACJI ---
							if (c.Char == ' ' || pixelRect.Width <= 0)
							{
								c.XOffset = 0; c.YOffset = 0;
								c.PixelWidth = 0; c.PixelHeight = 0;
								c.Width = (short)Math.Ceiling(advanceWidth);
								c.MaxWidth = (short)Math.Ceiling(advanceWidth);
								c.X = 0; c.Y = 0;
								continue;
							}

							// --- OBLICZANIE PRZESUNIĘCIA (Anti-Clip) ---
							// Jeśli litera wystaje w lewo (np. 'j', 'f'), przesuwamy ją w prawo na teksturze.
							float visualLeft = pixelRect.Left;
							// Round correction to integer to ensure pixel-perfect rendering
							float xCorrection = (visualLeft < 0) ? (float)Math.Ceiling(-visualLeft) : 0;

							float drawX = currentX + xCorrection + 1;
							float drawY = currentY + fontAscent;

							// --- WYMIARY KLATKI NA TEKSTURZE ---
							float contentRight = drawX + pixelRect.Width + shadowOffsetX;
							float contentWidth = contentRight - currentX;

							c.PixelWidth = (short)Math.Ceiling(contentWidth + 2);
							c.PixelHeight = lineHeight;
							c.Width = c.PixelWidth;

							// --- FIX GAP PO 'J' ---
							// Poprzednio dodawaliśmy xCorrection tutaj, co tworzyło dziurę.
							// Teraz bierzemy czysty AdvanceWidth + mały margines (1.5px).
							// xCorrection służy tylko do rysowania na teksturze, nie zwiększa logicznego odstępu.
							float calculatedAdvance = advanceWidth + 0.5f;

							// ZABEZPIECZENIE:
							// Sprawdzamy, czy "Czysty Advance" nie jest mniejszy niż "Fizyczne piksele pomniejszone o przesunięcie".
							// Czyli: Advance musi być przynajmniej taki, żeby pokryć narysowaną literę (nie licząc pustego miejsca z lewej).
							// float physicalEnd = (c.PixelWidth - xCorrection);
							// float safeAdvance = Math.Max(calculatedAdvance, physicalEnd);

							c.MaxWidth = (short)Math.Ceiling(calculatedAdvance);

							// Offsety 0 (silnik gry)
							// Jeżeli przesuwamy literę na teksturze (xCorrection), musimy cofnąć ją przy renderowaniu
							c.XOffset = (short)-Math.Ceiling(xCorrection);
							c.YOffset = 0;

							// Nowa linia
							if (currentX + c.PixelWidth + texturePadding > texWidth)
							{
								currentX = 0;
								currentY += (short)(lineHeight + texturePadding);
								drawX = xCorrection + 1;
								drawY = currentY + fontAscent;
							}

							// Rysowanie (Round positions to avoid subpixel blurring)
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

						// Zapis PNG
						using var image = surface.Snapshot();
						using var data = image.Encode(SKEncodedImageFormat.Png, 100);
						var pngFile = Path.Combine(OutDir, Path.GetFileNameWithoutExtension(FntPath) + ".png");
						using (var stream = File.OpenWrite(pngFile))
						{
							data.SaveTo(stream);
						}

						// Zapis FNT
						DrawChars = charList;
						if (!ExportFnt())
							throw new Exception("Export font failed");

						// Konwersja DDS
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

                    // ZMIANA: TwnKey mówi, że 0xC (tutaj MaxWidth) to po prostu Width (Szerokość klatki)
                    fs.WriteShort(c.PixelWidth); // Szerokość klatki na teksturze

                    // ZMIANA: 0xE (PixelHeight) to Height
                    fs.WriteShort(c.PixelHeight); // Wysokość klatki

                    fs.WriteShort(c.ColorChannel); // 0x100 / 0x200

                    // ZMIANA: 0x12 (XOffset) - wg TwnKey to kerning/spacing, ustawiamy 0 lub małą wartość
                    fs.WriteShort(c.XOffset);

                    // ZMIANA: 0x14 (YOffset) - wg TwnKey ustawić na 0, bo silnik jest zbugowany
                    fs.WriteShort(0);

                    // ZMIANA: 0x16 (Last Short) - wg TwnKey "distance to next char", Width + 2
                    fs.WriteShort(c.MaxWidth); // W kodzie wyżej ustawiłem to na PixelWidth + 2
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