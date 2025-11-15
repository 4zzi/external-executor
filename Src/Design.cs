using System.Numerics;
using ClickableTransparentOverlay;
using ImGuiNET;

public class OracleImGui : Overlay
{
    private List<ScriptTab> _tabs = new List<ScriptTab>();
    private int _activeTab = 0;
    private int _nextActiveTab = -1;
    private bool _showWindow = true;
    private string _renameBuffer = "";
    private int _renamingTab = -1;
    private List<string> _scriptFiles = new List<string>();
    private string _scriptsFolder;
    private Main.Roblox _roblox;

    public OracleImGui(Main.Roblox roblox) : base(1920, 1080)
    {
        _roblox = roblox;
        _tabs.Add(new ScriptTab { Name = "Script 1", Content = "" });

        string exeFolder = AppContext.BaseDirectory;
        string workspacePath = Path.Combine(exeFolder, "workspace");
        _scriptsFolder = Directory.Exists(workspacePath) ? workspacePath :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oracle Scripts");

        if (!Directory.Exists(_scriptsFolder))
            Directory.CreateDirectory(_scriptsFolder);

        RefreshScriptList();
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

    protected override void Render()
    {
        if (!_showWindow)
        {
            Environment.Exit(0);
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(1050, 500), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(300, 300), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 300), new Vector2(2000, 1000));

        ImGui.Begin("Oracle", ref _showWindow, ImGuiWindowFlags.NoCollapse);

        float sidebar = 150;
        ImGui.BeginChild("MainContent", new Vector2(ImGui.GetContentRegionAvail().X - sidebar - 10, ImGui.GetContentRegionAvail().Y));

        if (ImGui.BeginTabBar("Tabs"))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool open = true;
                ImGuiTabItemFlags f = ImGuiTabItemFlags.None;
                if (_nextActiveTab == i) { f = ImGuiTabItemFlags.SetSelected; _nextActiveTab = -1; }

                if (ImGui.BeginTabItem(_tabs[i].Name + "###" + i, ref open, f))
                {
                    _activeTab = i;

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
                    _tabs[_renamingTab].Name = _renameBuffer;
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

        if (_activeTab >= 0)
        {
            string content = _tabs[_activeTab].Content;
            float lineWidth = 28;

            float editorH = ImGui.GetContentRegionAvail().Y - 45;

            ImGui.BeginChild("Editor", new Vector2(0, editorH));

            ImGui.BeginChild("Lines", new Vector2(lineWidth, 0), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                ImGui.TextUnformatted((i + 1).ToString());
            }

            ImGui.EndChild();
            ImGui.SameLine();

            ImGui.BeginChild("TextBox", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar);
            ImGui.InputTextMultiline("##code", ref content, 100000, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.AllowTabInput);
            ImGui.EndChild();

            ImGui.EndChild();

            _tabs[_activeTab].Content = content;

            ImGui.Spacing();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X * 0.043f);
            float buttonWidth = ImGui.GetContentRegionAvail().X * 0.10f;

            if (ImGui.Button("Execute", new Vector2(buttonWidth, 25)))
            {
                Task.Run(async () => { try { await _roblox.Execute(_tabs[_activeTab].Content); } catch { } });
            }

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5);

            if (ImGui.Button("Clear", new Vector2(buttonWidth, 25)))
            {
                _tabs[_activeTab].Content = "";
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

        ImGui.BeginChild("SL");
        foreach (var s in _scriptFiles)
        {
            if (ImGui.Selectable(s))
            {
                string p = Path.Combine(_scriptsFolder, s);
                if (File.Exists(p))
                {
                    if (_activeTab >= 0)
                        _tabs[_activeTab].Content = File.ReadAllText(p);
                }
            }
        }
        ImGui.EndChild();

        ImGui.EndChild();

        ImGui.End();
    }

    private class ScriptTab
    {
        public string Name { get; set; }
        public string Content { get; set; }
    }
}
