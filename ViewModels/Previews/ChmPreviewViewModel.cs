using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class ChmTocNode
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public ObservableCollection<ChmTocNode> Children { get; set; } = new ObservableCollection<ChmTocNode>();
    }

    public class ChmPreviewViewModel : BasePreviewViewModel
    {
        private string _extractedPath;
        private string _indexPath;
        private ObservableCollection<ChmTocNode> _toc;

        public string IndexPath
        {
            get => _indexPath;
            set => SetProperty(ref _indexPath, value);
        }

        public ObservableCollection<ChmTocNode> Toc
        {
            get => _toc;
            set => SetProperty(ref _toc, value);
        }

        private bool _isTocVisible = true;
        public bool IsTocVisible
        {
            get => _isTocVisible;
            set => SetProperty(ref _isTocVisible, value);
        }

        public RelayCommand<string> NavigateCommand { get; }

        public ChmPreviewViewModel()
        {
            Icon = "📚";
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            NavigateCommand = new RelayCommand<string>(url =>
            {
                if (!string.IsNullOrEmpty(url) && File.Exists(url))
                    IndexPath = url;
            });
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            IsLoading = true;
            Toc = new ObservableCollection<ChmTocNode>();

            try
            {
                await Task.Run(() => ExtractChm(filePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CHM extraction failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExtractChm(string filePath)
        {
            _extractedPath = Path.Combine(Path.GetTempPath(), "YiboFile_CHM_" + Guid.NewGuid());
            Directory.CreateDirectory(_extractedPath);

            string sevenZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7z.exe");
            if (!File.Exists(sevenZipPath))
            {
                throw new Exception("未找到 7-Zip 工具，无法解压 CHM。");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"x \"{filePath}\" -o\"{_extractedPath}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(startInfo);
            process.WaitForExit();

            // Find index file
            IndexPath = FindIndexFile(_extractedPath);

            // Find and parse TOC
            var hhcFiles = Directory.GetFiles(_extractedPath, "*.hhc");
            if (hhcFiles.Length > 0)
            {
                ParseHhc(hhcFiles[0]);
            }
        }

        private void ParseHhc(string hhcPath)
        {
            try
            {
                // Ensure legacy encodings are available
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Detect encoding or default to correct one
                // Usually CHM hhc is ANSI, but for Chinese it's often GB2312.
                // We'll try to read as default, and if it fails or looks weird, try GB2312.
                string content;
                try
                {
                    // Try to detect if it's UTF8 BOM
                    using (var reader = new StreamReader(hhcPath, true))
                    {
                        content = reader.ReadToEnd();
                    }
                }
                catch
                {
                    // Fallback to GB2312 if default fail
                    content = File.ReadAllText(hhcPath, Encoding.GetEncoding("gb2312"));
                }

                // If content has replacement chars or looks broken, force GB2312
                if (content.Contains("") && !content.Contains("charset=utf-8"))
                {
                    content = File.ReadAllText(hhcPath, Encoding.GetEncoding("gb2312"));
                }

                var rootNodes = new ObservableCollection<ChmTocNode>();
                var collectionStack = new Stack<ObservableCollection<ChmTocNode>>();
                collectionStack.Push(rootNodes);
                ChmTocNode lastNode = null;

                // Regex to find tags: <UL>, </UL>, <OBJECT>...</OBJECT>, <PARAM ...>
                // We simplify by looking for relevant tags in order.
                // A <LI> usually precedes an <OBJECT>. nesting is done via <UL>

                // Matches: <UL, </UL, <PARAM name="x" value="y">
                // We'll iterate through all matches
                var regex = new Regex(@"<\/?ul>|<param\s+name=""?(\w+)""?\s+value=""?([^"">]+)""?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                var matches = regex.Matches(content);

                // We also need to know when we hit a new "node". 
                // In HHC, <LI> <OBJECT>... </OBJECT> is a node.
                // We can assume a new node starts when we see 'Name' param and we haven't 'finished' the previous one? 
                // Actually, the structure is:
                // <LI> <OBJECT> <param name="Name" value="Title"> <param name="Local" value="url"> </OBJECT>
                // <UL> ... </UL> (Children)

                // Better approach: Split by <OBJECT> tags first? No, nesting is outside objects.

                // Let's iterate matches.
                // If we see <UL>, we push the Children of the LAST node.
                // If we see </UL>, we pop.
                // If we see <PARAM Name="Name">, we are likely inside an object -> create new node or update current.
                // But we need to know if we are starting a NEW node. 
                // Usually <OBJECT> starts a new node definition for the preceding <LI>.

                // Let's assume every time we encounter a "Name" param, IF we don't have a 'current' node being built, or if we just finished one...
                // Actually, standard HHC has <OBJECT> block. Let's find matches for OBJECT blocks too.

                // Revised Regex to capture tokens: <UL>, </UL>, <OBJECT>
                var tokenRegex = new Regex(@"<\/?ul>|<object[^>]*>", RegexOptions.IgnoreCase);

                // We will scan the string.
                int pos = 0;
                while (pos < content.Length)
                {
                    var match = tokenRegex.Match(content, pos);
                    if (!match.Success) break;

                    pos = match.Index + match.Length;
                    string tag = match.Value.ToLower();

                    if (tag.StartsWith("<ul"))
                    {
                        if (lastNode != null)
                        {
                            collectionStack.Push(lastNode.Children);
                        }
                    }
                    else if (tag.StartsWith("</ul"))
                    {
                        if (collectionStack.Count > 1) collectionStack.Pop();
                    }
                    else if (tag.StartsWith("<object"))
                    {
                        // Found a node object. Parse its content until </object>
                        int endObj = content.IndexOf("</object>", pos, StringComparison.OrdinalIgnoreCase);
                        if (endObj == -1) endObj = content.Length;

                        string objContent = content.Substring(pos, endObj - pos);
                        pos = endObj + 9; // Skip </object>

                        var node = new ChmTocNode();

                        // Parse params
                        var nameMatch = Regex.Match(objContent, @"name=""?Name""?\s+value=""?([^""]+)""?", RegexOptions.IgnoreCase);
                        if (nameMatch.Success) node.Title = nameMatch.Groups[1].Value;
                        else
                        {
                            // Try single quotes
                            nameMatch = Regex.Match(objContent, @"name='?Name'?\s+value='?([^']+)['\s>]", RegexOptions.IgnoreCase);
                            if (nameMatch.Success) node.Title = nameMatch.Groups[1].Value;
                        }

                        var localMatch = Regex.Match(objContent, @"name=""?Local""?\s+value=""?([^""]+)""?", RegexOptions.IgnoreCase);
                        if (localMatch.Success) node.Url = Path.Combine(_extractedPath, localMatch.Groups[1].Value.TrimStart('/', '\\'));
                        else
                        {
                            localMatch = Regex.Match(objContent, @"name='?Local'?\s+value='?([^']+)['\s>]", RegexOptions.IgnoreCase);
                            if (localMatch.Success) node.Url = Path.Combine(_extractedPath, localMatch.Groups[1].Value.TrimStart('/', '\\'));
                        }

                        if (!string.IsNullOrEmpty(node.Title))
                        {
                            collectionStack.Peek().Add(node);
                            lastNode = node;
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() => Toc = rootNodes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error parsing HHC: " + ex.Message);
            }
        }

        private string FindIndexFile(string dir)
        {
            // Typical CHM entry points
            string[] names = { "index.html", "index.htm", "default.html", "default.htm", "main.html", "welcome.html" };
            foreach (var name in names)
            {
                var files = Directory.GetFiles(dir, name, SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            // Fallback: first html file
            var allHtml = Directory.GetFiles(dir, "*.h*", SearchOption.AllDirectories);
            return allHtml.Length > 0 ? allHtml[0] : null;
        }

        ~ChmPreviewViewModel()
        {
            // Cleanup in finalizer or Dispose
            try
            {
                if (!string.IsNullOrEmpty(_extractedPath) && Directory.Exists(_extractedPath))
                    Directory.Delete(_extractedPath, true);
            }
            catch { }
        }
    }
}

