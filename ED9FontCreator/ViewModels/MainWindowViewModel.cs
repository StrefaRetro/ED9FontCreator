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
                    Type = c.Type//c.Char is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or ' ' or ',' or '(' or ')' or '.' ? 1 : 0,
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
                //fnt
                if (!ExportFnt())
                    throw new Exception("Export font failed");
                //png
                var pSize = new PixelSize((int)DrawCanvas.Bounds.Width, (int)DrawCanvas.Bounds.Height);
                var size = new Size(pSize.Width, pSize.Height);
                using RenderTargetBitmap bitmap = new(pSize, new Vector(96, 96));
                DrawCanvas.Measure(size);
                DrawCanvas.Arrange(new Rect(size));
                DrawCanvas.UpdateLayout();
                bitmap.Render(DrawCanvas);
                var file = Path.Combine(OutDir, Path.GetFileNameWithoutExtension(FntPath) + ".png");
                bitmap.Save(file,100);
                //convert
                if (!(await PNG2DDS(file)))
                    throw new Exception("Font conversion failed");
#if RELEASE
                File.Delete(file);
#endif
                ShowInfo("Font export complete.", InfoBarState.Success);
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
                Arguments = $"-y -nologo -ft dds -w 0 -h 0 -if CUBIC -f BC7_UNORM -m 1 -o {OutDir} -r:keep {png}",
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
                var logFile = Path.Combine(OutDir, "texconv_log.txt");
                var logContent = $"[{DateTime.Now}] Texconv Failed\n" +
                                 $"Exit Code: {process.ExitCode}\n" +
                                 $"Arguments: {startInfo.Arguments}\n" +
                                 $"Output: {output}\n" +
                                 $"Error: {error}\n\n";
                await File.AppendAllTextAsync(logFile, logContent);
                ShowInfo($"Texconv Error! Check log: {logFile}", InfoBarState.Error);
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
                temp.Sort((x, y) => x.Code.CompareTo(y.Code));
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
                    fs.WriteShort(c.MaxWidth);
                    fs.WriteShort(c.PixelHeight);
                    fs.WriteShort(c.ColorChannel);
                    fs.WriteShort(c.XOffset);
                    fs.WriteShort(c.YOffset);
                    fs.WriteShort(c.Width);
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