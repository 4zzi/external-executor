using System.Numerics;
using System.Runtime.InteropServices;
using ClickableTransparentOverlay;
using ImGuiNET;
using System.Text.Json;
#if USE_SCINTILLA
using ScintillaNET;
using System.Drawing;
#endif

public class IMGui : Overlay
{
    private List<ScriptTab> _tabs = new List<ScriptTab>();
    private int _activeTab = 0;
    private int _nextActiveTab = -1;
    private bool _showWindow = true;
    private string _renameBuffer = "";
    private int _renamingTab = -1;
    private List<string> _scriptFiles = new List<string>();
    private string _scriptsFolder;
    private string _tabsFolder;
    private Main.Roblox _roblox;
    private bool _showSettings = false;
    private bool _topmost = true;
    private bool _highlightLuau = true;
    private float _editorScrollY = 0f;
    private int _caretIndex = 0;
    private int _selectionStart = -1;
#if USE_SCINTILLA
    private Scintilla _scintilla;
#endif
    private const string _defaultCode = "print(\"labubu 67\")";

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    public IMGui(Main.Roblox roblox) : base(1920, 1080)
    {
        _roblox = roblox;

        string exeFolder = AppContext.BaseDirectory;
        string workspacePath = Path.Combine(exeFolder, "workspace");
        _scriptsFolder = workspacePath;

        _tabsFolder = Path.Combine(exeFolder, "tabs");
        
        if (!Directory.Exists(_scriptsFolder))
            Directory.CreateDirectory(_scriptsFolder);
        
        if (!Directory.Exists(_tabsFolder))
            Directory.CreateDirectory(_tabsFolder);

        File.WriteAllText(Path.Combine(_scriptsFolder, "List.lua"), "print(\"script list test\")");
        File.WriteAllText(Path.Combine(_tabsFolder, "Script 1.lua"), "print(\"best exec 2067\")");
        File.WriteAllText(Path.Combine(_tabsFolder, "Tabs.lua"), "print(\"tab saving test\")");

        LoadSavedTabs();

        if (_tabs.Count == 0)
        {
            var scriptFiles = Directory.GetFiles(_scriptsFolder, "*.lua")
                .Concat(Directory.GetFiles(_scriptsFolder, "*.txt"))
                .ToList();

            if (scriptFiles.Any())
            {
                foreach (var p in scriptFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(p);
                        string content = File.ReadAllText(p);
                        _tabs.Add(new ScriptTab { Name = fileName, Content = content });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not load {p}: {ex.Message}");
                    }
                }
            }
            else
            {
                _tabs.Add(new ScriptTab { Name = "Script 1", Content = _defaultCode });
            }
        }

        if (_tabs.Count > 0 && (_activeTab < 0 || _activeTab >= _tabs.Count))
            _activeTab = 0;

        RefreshScriptList();
        SetTopmost(false);
#if USE_SCINTILLA
        try
        {
            _scintilla = new Scintilla();
            _scintilla.Lexer = Lexer.Lua;
            _scintilla.StyleResetDefault();
            _scintilla.Styles[Style.Default].ForeColor = Color.White;
            _scintilla.Styles[Style.Default].BackColor = Color.FromArgb(30, 30, 30);
            _scintilla.Styles[Style.Lua.Keyword].ForeColor = Color.FromArgb(242, 109, 109);
            _scintilla.Styles[Style.Lua.String].ForeColor = Color.FromArgb(255, 220, 120);
            _scintilla.Styles[Style.Lua.Comment].ForeColor = Color.FromArgb(150, 150, 150);
            _scintilla.SetKeywords(0, "and break do else elseif end false for function if in local nil not or repeat return then true until while continue");
        }
        catch
        {
            _scintilla = null;
        }
#endif
    }

    private void LoadSavedTabs()
    {
        try
        {
            if (!Directory.Exists(_tabsFolder))
                return;

            var tabFiles = Directory.GetFiles(_tabsFolder, "*.lua")
                .Concat(Directory.GetFiles(_tabsFolder, "*.txt"))
                .OrderBy(f => f)
                .ToList();

            foreach (var tabFile in tabFiles)
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(tabFile);
                    string content = File.ReadAllText(tabFile);
                    _tabs.Add(new ScriptTab { Name = fileName, Content = content });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not load tab {tabFile}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading saved tabs: {ex.Message}");
        }
    }

    private void SaveTabsToFolder()
    {
        try
        {
            if (!Directory.Exists(_tabsFolder))
                Directory.CreateDirectory(_tabsFolder);

            var existingFiles = Directory.GetFiles(_tabsFolder, "*.lua")
                .Concat(Directory.GetFiles(_tabsFolder, "*.txt"))
                .ToList();

            foreach (var file in existingFiles)
            {
                try { File.Delete(file); } catch { }
            }

            foreach (var tab in _tabs)
            {
                string safeName = string.Join("_", tab.Name.Split(Path.GetInvalidFileNameChars()));

                if (!safeName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    safeName += ".lua";
                }

                string filePath = Path.Combine(_tabsFolder, safeName);

                try
                {
                    File.WriteAllText(filePath, tab.Content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving tab '{tab.Name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving tabs: {ex.Message}");
        }
    }

    private void RefreshScriptList()
    {
        _scriptFiles.Clear();
        if (Directory.Exists(_scriptsFolder))
        {
            foreach (var file in Directory.GetFiles(_scriptsFolder, "*.lua"))
                _scriptFiles.Add(Path.GetFileName(file));

            foreach (var file in Directory.GetFiles(_scriptsFolder, "*.txt"))
                _scriptFiles.Add(Path.GetFileName(file));
        }
    }

    private void SetTopmost(bool topmost)
    {
        IntPtr hwnd = GetActiveWindow();
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    private void SaveOpenTabsAsScripts()
    {
        if (!Directory.Exists(_scriptsFolder))
        {
            Directory.CreateDirectory(_scriptsFolder);
        }

        foreach (var tab in _tabs)
        {
            string safeName = string.Join("_", tab.Name.Split(Path.GetInvalidFileNameChars()));

            if (!safeName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                safeName += ".lua";
            }

            string filePath = Path.Combine(_scriptsFolder, safeName);

            try
            {
                File.WriteAllText(filePath, tab.Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving script '{tab.Name}': {ex.Message}");
            }
        }
    }

    protected override void Render()
    {
        if (!_showWindow)
        {
            SaveTabsToFolder();
            Environment.Exit(0);
            return;
        }

        var screenSize = ImGui.GetIO().DisplaySize;
        var windowSize = new Vector2(1050, 500);
        var centeredPos = new Vector2((screenSize.X - windowSize.X) * 0.5f, (screenSize.Y - windowSize.Y) * 0.5f);
        
        ImGui.SetNextWindowPos(centeredPos, ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 300), new Vector2(2000, 1000));

        ImGui.Begin("Oracle", ref _showWindow,
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoTitleBar);

        var drawList = ImGui.GetWindowDrawList();
        var titleBarColor = ImGui.GetColorU32(new Vector4(0.0f, 0.47f, 0.84f, 1.0f));
        var windowPos = ImGui.GetWindowPos();
        drawList.AddRectFilled(windowPos, new Vector2(windowPos.X + ImGui.GetWindowWidth(), windowPos.Y + 25), titleBarColor);

        ImGui.SetCursorPosY(4);
        ImGui.Text("Oracle");
        ImGui.SameLine(ImGui.GetWindowWidth() - 50);
        ImGui.SetCursorPosY(2);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.3f));
        if (ImGui.Button("⚙##settings"))
            _showSettings = true;
        ImGui.SameLine();
        ImGui.SetCursorPosY(2);
        if (ImGui.Button("X##close"))
            _showWindow = false;
        ImGui.PopStyleColor(3);

        ImGui.SetCursorPosY(26);
        float sidebar = 150;

        ImGui.BeginChild("MainContent",
            new Vector2(ImGui.GetContentRegionAvail().X - sidebar - 10,
            ImGui.GetContentRegionAvail().Y),
            ImGuiChildFlags.None,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        if (ImGui.BeginTabBar("Tabs"))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool open = true;
                ImGuiTabItemFlags f = _nextActiveTab == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

                if (ImGui.BeginTabItem(_tabs[i].Name + "###" + i, ref open, f))
                {
                    _activeTab = i;
                    _nextActiveTab = -1;

                    if (ImGui.BeginPopupContextItem())
                    {
                        if (ImGui.MenuItem("Rename"))
                        {
                            _renamingTab = i;
                            _renameBuffer = _tabs[i].Name;
                            ImGui.OpenPopup("Rename Tab");
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.EndTabItem();
                }

                if (!open && _tabs.Count > 1)
                {
                    _tabs.RemoveAt(i);
                    if (_activeTab >= _tabs.Count) _activeTab = _tabs.Count - 1;
                    SaveTabsToFolder();
                    break;
                }
            }

            if (ImGui.TabItemButton("+", ImGuiTabItemFlags.Trailing))
            {
                _tabs.Add(new ScriptTab { Name = "Script " + (_tabs.Count + 1), Content = "" });
                _nextActiveTab = _tabs.Count - 1;
            }

            ImGui.EndTabBar();
        }

        if (_renamingTab >= 0)
            ImGui.OpenPopup("Rename Tab");

        bool ro = true;
        if (ImGui.BeginPopupModal("Rename Tab", ref ro))
        {
            ImGui.InputText("##rn", ref _renameBuffer, 100);
            if (ImGui.Button("OK", new Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_renameBuffer))
                {
                    _tabs[_renamingTab].Name = _renameBuffer;
                    SaveTabsToFolder();
                }
                _renamingTab = -1;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _renamingTab = -1;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (!ro) _renamingTab = -1;

        if (_showSettings)
            ImGui.OpenPopup("Settings");

        bool settingsOpen = true;
        if (ImGui.BeginPopupModal("Settings", ref settingsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Separator();
            ImGui.Spacing();
            bool previousTopmost = _topmost;
            if (ImGui.Checkbox("Always on Top", ref _topmost))
            {
                if (_topmost != previousTopmost)
                    SetTopmost(_topmost);
            }
            ImGui.Spacing();

            _highlightLuau = true;
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Close", new Vector2(120, 0)))
            {
                _showSettings = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (!settingsOpen) _showSettings = false;

        if (_activeTab >= 0 && _activeTab < _tabs.Count)
        {
            string content = _tabs[_activeTab].Content;
            float editorH = ImGui.GetContentRegionAvail().Y - 45;

            ImGui.BeginChild("Editor", new Vector2(0, editorH), ImGuiChildFlags.None);
            ImGui.SetScrollY(_editorScrollY);
            float scrollY = _editorScrollY;

            ImGui.SameLine();

            ImGui.BeginChild("TextBox", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            ImGui.SetScrollY(scrollY);

            var dl = ImGui.GetWindowDrawList();
            var childPos = ImGui.GetCursorScreenPos();
            var avail = ImGui.GetContentRegionAvail();

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 0, 0, 0));
            string _oldContent = content;
            ImGui.InputTextMultiline("##code", ref content, 100000, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.AllowTabInput);
            ImGui.PopStyleColor();

            if (content != _oldContent && Math.Abs(content.Length - _oldContent.Length) > 1)
            {
                ImGui.SetScrollY(ImGui.GetScrollMaxY());
            }

            _editorScrollY = ImGui.GetScrollY();

            RenderHighlightedLuaForeground(content, childPos, avail, dl);

            ImGui.EndChild();

            ImGui.EndChild();

            if (_tabs[_activeTab].Content != content)
            {
                _tabs[_activeTab].Content = content;
            }

            ImGui.Spacing();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X * 0.01f);
            float buttonWidth = ImGui.GetContentRegionAvail().X * 0.10f;

            if (ImGui.Button("Execute", new Vector2(buttonWidth, 25)))
                Task.Run(async () => { try { await _roblox.Execute(_tabs[_activeTab].Content); } catch { } });

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5);

            if (ImGui.Button("Clear", new Vector2(buttonWidth, 25)))
            {
                _tabs[_activeTab].Content = "";
                SaveTabsToFolder();
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("Scripts", new Vector2(sidebar, ImGui.GetContentRegionAvail().Y));
        ImGui.Text("Scripts");
        ImGui.Separator();
        if (ImGui.Button("Refresh", new Vector2(ImGui.GetContentRegionAvail().X, 25)))
            RefreshScriptList();
        ImGui.Spacing();
        ImGui.BeginChild("SL", new Vector2(0, 0));
        
        foreach (var s in _scriptFiles)
        {
            if (ImGui.Selectable(s))
            {
                string p = Path.Combine(_scriptsFolder, s);
                if (File.Exists(p))
                {
                    string fileName = Path.GetFileNameWithoutExtension(s);
                    string content = File.ReadAllText(p);
                    _tabs.Add(new ScriptTab { Name = fileName, Content = content });
                    _activeTab = _tabs.Count - 1;
                }
            }
        }
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.End();
    }

    private class ScriptTab
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private void RenderHighlightedLuaDraw(string content, Vector2 startPos, Vector2 avail, ImDrawListPtr dl)
    {
        var keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "and","break","do","else","elseif","end","false","for","function","if","in","local","nil","not","or","repeat","return","then","true","until","while","continue"
        };
        var builtins = new HashSet<string>(StringComparer.Ordinal)
        {
            "print","warn","require","pcall","xpcall","assert","error","spawn","delay","wait","pairs","ipairs","next","typeof","tostring","tonumber","table","math","string","task"
        };

        var commentColorV = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        var stringColorV = new Vector4(0.8f, 0.6f, 0.2f, 1.0f);
        var keywordColorV = new Vector4(0.9f, 0.3f, 0.3f, 1.0f);
        var numberColorV = new Vector4(0.6f, 0.4f, 0.9f, 1.0f);
        var defaultColorV = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

        var commentBg = new Vector4(commentColorV.X, commentColorV.Y, commentColorV.Z, 0.14f);
        var stringBg = new Vector4(stringColorV.X, stringColorV.Y, stringColorV.Z, 0.16f);
        var keywordBg = new Vector4(keywordColorV.X, keywordColorV.Y, keywordColorV.Z, 0.14f);
        var numberBg = new Vector4(numberColorV.X, numberColorV.Y, numberColorV.Z, 0.14f);

        uint commentCol = ImGui.GetColorU32(commentBg);
        uint stringCol = ImGui.GetColorU32(stringBg);
        uint keywordCol = ImGui.GetColorU32(keywordBg);
        uint numberCol = ImGui.GetColorU32(numberBg);
        uint defaultCol = ImGui.GetColorU32(defaultColorV);

        var lines = content.Replace("\r", "").Split('\n');
        float lineHeight = ImGui.GetFontSize();
        float scrollY = ImGui.GetScrollY();
        float scrollX = ImGui.GetScrollX();

        Vector2 clipMin = startPos;
        Vector2 clipMax = new Vector2(startPos.X + avail.X, startPos.Y + avail.Y);
        dl.PushClipRect(clipMin, clipMax, true);

        float overlayOffset = 2.0f;
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            float x = startPos.X - scrollX + overlayOffset;
            float y = startPos.Y + li * lineHeight - scrollY;
            int i = 0;

            while (i < line.Length)
            {
                if (i + 1 < line.Length && line[i] == '-' && line[i + 1] == '-')
                {
                    string rest = line.Substring(i);
                    var sizeR = ImGui.CalcTextSize(rest);
                    x += sizeR.X;
                    break;
                }

                if (char.IsWhiteSpace(line[i]))
                {
                    int start = i;
                    while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                    string ws = line.Substring(start, i - start);
                    x += ImGui.CalcTextSize(ws).X;
                    continue;
                }

                if (line[i] == '"' || line[i] == '\'')
                {
                    char q = line[i];
                    int start = i;
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                        if (line[i] == q) { i++; break; }
                        i++;
                    }
                    string tok = line.Substring(start, i - start);
                    var sizeS = ImGui.CalcTextSize(tok);
                    dl.AddRectFilled(new Vector2(x, y), new Vector2(x + sizeS.X, y + lineHeight), stringCol);
                    x += sizeS.X;
                    continue;
                }

                if (char.IsDigit(line[i]))
                {
                    int start = i;
                    while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.')) i++;
                    string tok = line.Substring(start, i - start);
                    var sizeN = ImGui.CalcTextSize(tok);
                    dl.AddRectFilled(new Vector2(x, y), new Vector2(x + sizeN.X, y + lineHeight), numberCol);
                    x += sizeN.X;
                    continue;
                }

                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    string tok = line.Substring(start, i - start);
                    var sizeT = ImGui.CalcTextSize(tok);
                    if (keywords.Contains(tok))
                        dl.AddRectFilled(new Vector2(x, y), new Vector2(x + sizeT.X, y + lineHeight), keywordCol);
                    else if (builtins.Contains(tok))
                        dl.AddRectFilled(new Vector2(x, y), new Vector2(x + sizeT.X, y + lineHeight), ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.25f, 0.10f)));
                    x += sizeT.X;
                    continue;
                }

                string ch = line[i].ToString();
                var sizeCh = ImGui.CalcTextSize(ch);
                x += sizeCh.X;
                i++;
            }
        }

        dl.PopClipRect();
    }

    private int InputTextCallback(ImGuiInputTextCallbackDataPtr data, IntPtr userData)
    {
        _caretIndex = (int)data.CursorPos;
        _selectionStart = (int)data.SelectionStart;
        return 0;
    }

    private void RenderHighlightedLuaForeground(string content, Vector2 startPos, Vector2 avail, ImDrawListPtr dl)
    {
#if USE_SCINTILLA
        if (_scintilla != null)
        {
            _scintilla.Text = content ?? "";
            var fd = ImGui.GetForegroundDrawList();
            var lines = (content ?? "").Replace("\r", "").Split('\n');
            float lineHeight = ImGui.GetFontSize();
            float scrollY = ImGui.GetScrollY();
            float scrollX = ImGui.GetScrollX();
            Vector2 clipMin = startPos;
            Vector2 clipMax = new Vector2(startPos.X + avail.X, startPos.Y + avail.Y);
            fd.PushClipRect(clipMin, clipMax, true);

            var styleColors = new Dictionary<int, uint>();
            styleColors[0] = ImGui.GetColorU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
            styleColors[1] = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
            styleColors[2] = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
            styleColors[4] = ImGui.GetColorU32(new Vector4(0.75f, 0.55f, 1.0f, 1.0f));
            styleColors[5] = ImGui.GetColorU32(new Vector4(0.95f, 0.45f, 0.45f, 1.0f));
            styleColors[6] = ImGui.GetColorU32(new Vector4(1.0f, 0.85f, 0.4f, 1.0f));

            int globalIndex = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                float x = startPos.X - scrollX;
                float y = startPos.Y + li * lineHeight - scrollY;

                int col = 0;
                while (col < line.Length)
                {
                    int style = _scintilla.GetStyleAt(globalIndex);
                    int startCol = col;
                    int startGlobal = globalIndex;
                    while (col < line.Length && _scintilla.GetStyleAt(globalIndex) == style)
                    {
                        col++;
                        globalIndex++;
                    }
                    string tok = line.Substring(startCol, col - startCol);
                    uint color = styleColors.ContainsKey(style) ? styleColors[style] : styleColors[0];
                    fd.AddText(new Vector2(x, y), color, tok);
                    x += ImGui.CalcTextSize(tok).X;
                }
                globalIndex++;
            }

            fd.PopClipRect();
            return;
        }
#endif

        var keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "and","break","do","else","elseif","end","false","for","function","if","in","local","nil","not","or","repeat","return","then","true","until","while","continue"
        };

        var commentColorV = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
        var stringColorV = new Vector4(1.0f, 0.85f, 0.4f, 1.0f);
        var keywordColorV = new Vector4(0.95f, 0.45f, 0.45f, 1.0f);
        var numberColorV = new Vector4(0.75f, 0.55f, 1.0f, 1.0f);
        var defaultColorV = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

        uint commentCol = ImGui.GetColorU32(commentColorV);
        uint stringCol = ImGui.GetColorU32(stringColorV);
        uint keywordCol = ImGui.GetColorU32(keywordColorV);
        uint numberCol = ImGui.GetColorU32(numberColorV);
        uint defaultCol = ImGui.GetColorU32(defaultColorV);

        var builtins = new HashSet<string>(StringComparer.Ordinal)
        {
            "print","warn","require","pcall","xpcall","assert","error","spawn","delay","wait","pairs","ipairs","next","typeof","tostring","tonumber","table","math","string","task"
        };
        uint builtinCol = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.4f, 1.0f));

        var fdFallback = ImGui.GetForegroundDrawList();

        var linesFallback = content.Replace("\r", "").Split('\n');
        float lineHeightFallback = ImGui.GetFontSize();
        float scrollYFallback = ImGui.GetScrollY();
        float scrollXFallback = ImGui.GetScrollX();

        Vector2 clipMinFallback = startPos;
        Vector2 clipMaxFallback = new Vector2(startPos.X + avail.X, startPos.Y + avail.Y);
        fdFallback.PushClipRect(clipMinFallback, clipMaxFallback, true);

        for (int li = 0; li < linesFallback.Length; li++)
        {
            string line = linesFallback[li];
            float x = startPos.X - scrollXFallback;
            float y = startPos.Y + li * lineHeightFallback - scrollYFallback;
            int i = 0;

            while (i < line.Length)
            {
                if (i + 1 < line.Length && line[i] == '-' && line[i + 1] == '-')
                {
                    string rest = line.Substring(i);
                    fdFallback.AddText(new Vector2(x, y), commentCol, rest);
                    x += ImGui.CalcTextSize(rest).X;
                    break;
                }

                if (char.IsWhiteSpace(line[i]))
                {
                    int start = i;
                    while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                    string ws = line.Substring(start, i - start);
                    x += ImGui.CalcTextSize(ws).X;
                    continue;
                }

                if (line[i] == '"' || line[i] == '\'')
                {
                    char q = line[i];
                    int start = i;
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                        if (line[i] == q) { i++; break; }
                        i++;
                    }
                    string tok = line.Substring(start, i - start);
                    fdFallback.AddText(new Vector2(x, y), stringCol, tok);
                    x += ImGui.CalcTextSize(tok).X;
                    continue;
                }

                if (char.IsDigit(line[i]))
                {
                    int start = i;
                    while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.')) i++;
                    string tok = line.Substring(start, i - start);
                    fdFallback.AddText(new Vector2(x, y), numberCol, tok);
                    x += ImGui.CalcTextSize(tok).X;
                    continue;
                }

                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    string tok = line.Substring(start, i - start);
                    
                    if (keywords.Contains(tok))
                        fdFallback.AddText(new Vector2(x, y), keywordCol, tok);
                    else if (builtins.Contains(tok))
                        fdFallback.AddText(new Vector2(x, y), builtinCol, tok);
                    else
                        fdFallback.AddText(new Vector2(x, y), defaultCol, tok);
                    x += ImGui.CalcTextSize(tok).X;
                    continue;
                }

                string ch = line[i].ToString();
                fdFallback.AddText(new Vector2(x, y), defaultCol, ch);
                x += ImGui.CalcTextSize(ch).X;
                i++;
            }
        }
        fdFallback.PopClipRect();
    }

    private void DrawCaretOverlay(string content, Vector2 startPos, Vector2 avail, ImDrawListPtr dl)
    {
        int caret = _caretIndex;
        if (caret < 0) return;

        caret = Math.Min(caret, content.Length);

        int line = 0;
        int col = 0;
        int idx = 0;
        var lines = content.Replace("\r", "").Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            int len = lines[li].Length + 1;
            if (caret <= idx + lines[li].Length)
            {
                line = li;
                col = caret - idx;
                break;
            }
            idx += len;
        }

        float lineHeight = ImGui.GetFontSize();
        float scrollY = ImGui.GetScrollY();
        float scrollX = ImGui.GetScrollX();

        string textLine = lines.Length > line ? lines[line] : "";
        string before = textLine.Substring(0, Math.Max(0, Math.Min(col, textLine.Length)));
        float caretX = startPos.X - scrollX + ImGui.CalcTextSize(before).X;
        float caretY = startPos.Y + line * lineHeight - scrollY;

        var caretColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        dl.AddRectFilled(new Vector2(caretX, caretY), new Vector2(caretX + 1.2f, caretY + lineHeight), caretColor);
    }
}