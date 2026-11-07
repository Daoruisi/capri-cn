using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Mainframe
    {
        private const int ModRows = 4, ModRowTop = 204, ModRowHeight = 62;
        private readonly List<KeyValuePair<Control, int>> modLeft = new List<KeyValuePair<Control, int>>();
        private readonly Label[] modNames = new Label[ModRows], modAbouts = new Label[ModRows];
        private readonly Key[] modSwitches = new Key[ModRows], modRemoves = new Key[ModRows], modPublishes = new Key[ModRows];
        private Key modsKey, modBack, modNext, modBrowse;
        private Head modListHead;
        private readonly Key[] colourKeys = new Key[ArtColors.Slots];
        private Label modStatus, modEmpty, modPage;
        private bool modded;
        private int modFirst;

        private void BuildMods()
        {
            ModAsk = AskToRun;
            int first = Controls.Count;
            Section("模组 >", 0, 58);
            Caption("代码模组能碰到你整台电脑，只装信得过的。", 28, 80, Look.Mono(8.5f, FontStyle.Italic, GraphicsUnit.Point), Faint);
            Key import = Make("导入模组", 28, 104, 100, 28, delegate { ImportMod(); });
            import.Accent = Cyan;
            import.Loud = true;
            modBrowse = Make("浏览模组", 134, 104, 100, 28, delegate { BrowseMods(); });
            Key folder = Make("模组文件夹", 240, 104, 116, 28, delegate { OpenModsFolder(); });
            Explain(import, "导入模组", "MainframeMods.cs");
            Explain(modBrowse, "浏览模组", "MainframeMods.cs");
            Explain(folder, "Open mods folder", "MainframeMods.cs");
            modStatus = ModLabel("", 28, 138, 328, 36, Font, Faint);
            // A long import error, like a compiler's, is cut to two lines here, and hovering it shows the whole thing.
            ((Note)modStatus).Hovers = true;
            modListHead = Section("已安装 >", 0, 180);
            modEmpty = ModLabel("还没有模组。导入一个 " + Mods.Extension + " file.", 28, ModRowTop, 328, 20, Font, Faint);
            for (int i = 0; i < ModRows; i++)
            {
                int row = i, top = ModRowTop + ModRowHeight * i;
                modNames[i] = ModLabel("", 28, top + 3, 196, 20, Type(10f, true), White);
                modSwitches[i] = Make("开", 230, top, 54, 22, delegate { SwitchMod(row); });
                modRemoves[i] = Make("移除", 290, top, 66, 22, delegate { RemoveMod(row); });
                modAbouts[i] = ModLabel("", 28, top + 26, 256, 30, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Faint);
                Explain(modSwitches[i], "Mod switch", "MainframeMods.cs", null, "On / Off");
                Explain(modRemoves[i], "移除", "MainframeMods.cs");
                modPublishes[i] = Make("发布", 290, top + 28, 66, 22, delegate { PublishMod(row); });
                Explain(modPublishes[i], "Publish mod", "MainframeMods.cs");
            }
            modBack = Make("返回", 28, ModRowTop + ModRowHeight * ModRows, 60, 22, delegate { modFirst -= ModRows; FillMods(); });
            modPage = ModLabel("", 96, ModRowTop + ModRowHeight * ModRows + 3, 192, 18, Font, Faint);
            modPage.TextAlign = ContentAlignment.MiddleCenter;
            Explain(modBack, "Mod pages", "MainframeMods.cs");
            modNext = Make("下一页", 296, ModRowTop + ModRowHeight * ModRows, 60, 22, delegate { modFirst += ModRows; FillMods(); });
            Explain(modNext, "Mod pages", "MainframeMods.cs");
            Section("配色 >", 0, ModRowTop + ModRowHeight * ModRows + 30);
            int colourRow = ModRowTop + ModRowHeight * ModRows + 54;
            for (int i = 0; i < ArtColors.Slots; i++)
            {
                int slot = i;
                colourKeys[i] = Make(ArtColors.Names[i], 28 + i * 66, colourRow, 62, 26, delegate { PickColour(slot); });
                colourKeys[i].AccessibleName = "改她的 " + ArtColors.Names[i].ToLowerInvariant() + "颜色";
                Explain(colourKeys[i], "Her colors", "MainframeMods.cs", null, "Her colors");
            }
            Key colourReset = Make("恢复配色", 28, colourRow + 32, 160, 24, delegate { ResetColours(); });
            Explain(colourReset, "恢复配色", "MainframeMods.cs");
            for (int i = first; i < Controls.Count; i++)
            {
                Control control = Controls[i];
                modLeft.Add(new KeyValuePair<Control, int>(control, control.Left));
                control.Left += Far;
                Reachable(control, false);
            }
        }

        private Label ModLabel(string text, int x, int y, int width, int height, Font font, Color colour)
        {
            Label label = Caption(text, x, y, font, colour);
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.Size = new Size(width, height);
            return label;
        }

        internal bool ModsShown { get { return modded; } }

        private void CheckModsKey()
        {
            if (more.Left != Scaled(146) || more.Text != "设置" || modsKey.Top != more.Top || modsKey.Left < more.Right || modsKey.Right > minimize.Left) throw new Exception("Settings or Mods is in the wrong place.");
            try
            {
                foreach (bool fallback in new[] { false, true })
                {
                    LayoutScale(96, 100, fallback);
                    foreach (bool page in new[] { false, true })
                    {
                        ShowMods(page);
                        string crowded = TextFits() ?? Overlap();
                        if (crowded != null) throw new Exception("The Mods page or key overlaps" + (fallback ? " on the fallback font: " : ": ") + crowded);
                    }
                    ShowSettings(false);
                    if (more.Text != "返回") throw new Exception("Settings lost Back while Mods was open.");
                    more.PerformClick();
                    if (modded || paged || more.Text != "设置") throw new Exception("Back did not leave Mods.");
                }
            }
            finally { LayoutScale(0, 0); ShowMods(false); }
        }

        internal void ShowMods(bool on)
        {
            DismissHint();
            if (on && paged) ShowSettings(false);
            modded = on;
            more.Text = on ? "返回" : "设置";
            more.Invalidate();
            Place();
            foreach (KeyValuePair<Control, int> entry in mainLeft) Reachable(entry.Key, !on);
            foreach (KeyValuePair<Control, int> entry in modLeft) Reachable(entry.Key, on);
            if (Hidden(ActiveControl)) more.Focus();
            if (on) FillMods();
            Arrange(expanded);
        }

        private void FillMods()
        {
            List<Mod> mods = capri.ModList.All;
            int total = mods.Count, pageSize = ModRows;
            modFirst = Math.Max(0, Math.Min(modFirst, (Math.Max(0, total - 1) / pageSize) * pageSize));
            modEmpty.Visible = total == 0;
            for (int i = 0; i < ModRows; i++)
            {
                Mod mod = modFirst + i < mods.Count ? mods[modFirst + i] : null;
                modNames[i].Visible = modAbouts[i].Visible = modSwitches[i].Visible = modRemoves[i].Visible = modPublishes[i].Visible = mod != null;
                if (mod == null) continue;
                modNames[i].Text = mod.File.Name + (string.IsNullOrEmpty(mod.File.Version) ? "" : " v" + mod.File.Version);
                modNames[i].ForeColor = mod.On ? White : Faint;
                modAbouts[i].Text = ((mod.Changed ? "已改动，需重新批准。" : mod.Code ? "运行代码。" : mod.Skin ? "皮肤。" : mod.Skill ? "技能。" : "") + (string.IsNullOrEmpty(mod.File.Author) ? "" : "作者：" + mod.File.Author + ". ") + (mod.File.Description ?? "") + (mod.Code || mod.File.WakeNames.Length == 0 ? "" : " 名字：" + string.Join(", ", mod.File.WakeNames)) + (mod.Skill ? " For " + mod.File.Games[0] + "." : "")).Trim();
                modAbouts[i].ForeColor = mod.Changed ? Rose : Faint;
                modSwitches[i].Text = mod.On ? "开" : "关";
                modSwitches[i].Accent = mod.On ? Cyan : Rose;
                modSwitches[i].AccessibleName = (mod.On ? "关掉 " : "打开 ") + mod.File.Name;
                modRemoves[i].AccessibleName = "Remove " + mod.File.Name;
                modPublishes[i].AccessibleName = "发布 " + mod.File.Name + " 到模组目录";
                modSwitches[i].Invalidate();
            }
            bool pages = total > pageSize;
            modBack.Visible = modNext.Visible = modPage.Visible = pages;
            modBack.Enabled = modFirst > 0;
            modNext.Enabled = modFirst + pageSize < total;
            modPage.Text = "第 " + (modFirst / pageSize + 1) + " / " + ((total - 1) / pageSize + 1);
            string[] colours = capri.ModList.ColoursNow;
            for (int i = 0; i < ArtColors.Slots; i++)
            {
                string shown = colours[i] ?? ArtColors.Usual[i];
                Color swatch = ColorTranslator.FromHtml(shown);
                colourKeys[i].Accent = swatch.GetBrightness() < 0.2f ? Faint : swatch;
                colourKeys[i].Invalidate();
            }
            if (capri.ModList.Problems.Count > 0) ModSays("Skipped " + capri.ModList.Problems[0], true);
        }

        private void ModSays(string text, bool problem)
        {
            modStatus.Text = text;
            modStatus.ForeColor = problem ? Rose : Cyan;
        }

        private void ImportMod()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Import a Capri mod";
                dialog.Filter = "Capri mods (*" + Mods.Extension + ";*" + Mods.CodeExtension + ";*" + Mods.SkinExtension + ";*" + Mods.SkillExtension + ")|*" + Mods.Extension + ";*" + Mods.CodeExtension + ";*" + Mods.SkinExtension + ";*" + Mods.SkillExtension;
                if (dialog.ShowDialog(this) == DialogResult.OK) ImportMod(dialog.FileName);
            }
        }

        internal bool ImportMod(string file)
        {
            try
            {
                Mod mod = capri.ModList.Import(file, ModAsk);
                if (mod == null)
                {
                    ModSays("Not imported. Nothing ran.", false);
                    return false;
                }
                FillMods();
                ModSays(mod.Code ? mod.File.Name + " is on and running." : mod.Skill ? mod.File.Name + " is on. She uses it when she plays " + mod.File.Games[0] + "." : mod.File.WakeNames.Length == 0 ? mod.File.Name + " is on." : mod.File.Name + " is on. She now answers to " + Mods.Listed(mod.File.WakeNames) + " too.", false);
                return true;
            }
            catch (ModException error)
            {
                // Its whole reason goes to errors.txt too, and the list shows a code mod that saved but didn't start as off.
                Brain.Log("mods", -1, Path.GetFileName(file) + ": " + error.Message);
                FillMods();
                ModSays(error.Message, true);
                return false;
            }
        }

        internal bool ModStatusHovers { get { Note note = modStatus as Note; return note != null && note.Hovers; } }

        private static string DirectoryProblem(Exception error, string lead)
        {
            return error is ModException ? error.Message : lead + error.Message;
        }

        private void PickColour(int slot)
        {
            string now = capri.ModList.ColoursNow[slot] ?? ArtColors.Usual[slot];
            using (ColorDialog dialog = new ColorDialog { FullOpen = true, Color = ColorTranslator.FromHtml(now) })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Color picked = dialog.Color;
                try
                {
                    capri.ModList.SetColour(slot, "#" + picked.R.ToString("X2") + picked.G.ToString("X2") + picked.B.ToString("X2"));
                    FillMods();
                    ModSays("Her " + ArtColors.Names[slot].ToLowerInvariant() + " changed. Saved as My Colors, so you can share it.", false);
                }
                catch (IOException error) { ModSays("Couldn't save that color: " + error.Message, true); }
                catch (UnauthorizedAccessException error) { ModSays("Couldn't save that color: " + error.Message, true); }
            }
        }

        private void ResetColours()
        {
            try
            {
                capri.ModList.ResetColours();
                FillMods();
                ModSays("Her own colors are back.", false);
            }
            catch (IOException error) { ModSays("Couldn't reset her colors: " + error.Message, true); }
            catch (UnauthorizedAccessException error) { ModSays("Couldn't reset her colors: " + error.Message, true); }
        }

        private bool SwitchMod(int row)
        {
            Mod mod = capri.ModList.All[modFirst + row];
            try
            {
                bool done = capri.ModList.Set(mod, !mod.On, ModAsk);
                FillMods();
                ModSays(!done ? mod.File.Name + " stays off. Nothing ran." : mod.File.Name + (mod.On ? " is on." : " is off."), false);
                return done;
            }
            catch (ModException error) { ModSays(error.Message, true); }
            catch (IOException error) { ModSays("Couldn't change that mod: " + error.Message, true); }
            catch (UnauthorizedAccessException error) { ModSays("Couldn't change that mod: " + error.Message, true); }
            return false;
        }

        internal bool SwitchModForTest(int row) { return SwitchMod(row); }
        internal string ModStatusText { get { return modStatus.Text; } }

        internal Func<ModFile, string, bool> ModAsk;

        private bool AskToRun(ModFile mod, string hash)
        {
            string message = mod.Name + (string.IsNullOrEmpty(mod.Version) ? "" : " v" + mod.Version) + (string.IsNullOrEmpty(mod.Author) ? "" : ", which says it's by " + mod.Author) + ", is a code mod.\n\n"
                + "代码模组作为 Capri 的一部分运行，能完全访问你的电脑：你的文件、账号以及她保存的 API Key。对它没有任何限制，Capri 也不会检查它是否安全。\n\n"
                + "Only run it if you wrote it, or you've read it and trust who did.\n\nFile fingerprint (SHA-256):\n" + hash + "\n\nRun this code?";
            return MessageBox.Show(this, message, "要运行这个代码模组吗？", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void RemoveMod(int row)
        {
            Mod mod = capri.ModList.All[modFirst + row];
            try
            {
                capri.ModList.Remove(mod);
                FillMods();
                ModSays(mod.File.Name + " is removed.", false);
            }
            catch (IOException error) { ModSays("Couldn't remove that mod: " + error.Message, true); }
            catch (UnauthorizedAccessException error) { ModSays("Couldn't remove that mod: " + error.Message, true); }
        }

        private void OpenModsFolder()
        {
            try
            {
                Directory.CreateDirectory(capri.ModList.Folder);
                using (Process.Start("explorer.exe", "\"" + capri.ModList.Folder + "\"")) { }
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception)) throw;
                ModSays("Couldn't open the mods folder: " + error.Message, true);
            }
        }

        internal void CheckMods(List<string> checks, string output)
        {
            FormWindowState wasState = WindowState;
            Restore();
            Mods mods = capri.ModList;
            Func<string, string, string> sample = delegate(string name, string body)
            {
                string file = Path.Combine(output, name);
                File.WriteAllText(file, body);
                return file;
            };
            string good = sample("custom-names" + Mods.Extension, "{\"format\":1,\"id\":\"custom-names\",\"name\":\"Custom Names\",\"version\":\"1.0.0\",\"author\":\"Avery\",\"description\":\"Answers to more names.\",\"wake_names\":[\"Nova\",\"Big Z\",\"Capricorn\"]}");
            foreach (Mod old in mods.All.ToArray()) mods.Remove(old);
            if (Hands.HeardName("Nova, what time is it?") || !Hands.HeardName("Capri, what time is it?")) throw new Exception("Her names were wrong before any mod.");
            ShowMods(true);
            if (!modded || more.Text != "返回" || modStatus.Left >= Far || modsKey.Left >= Far) throw new Exception("The Mods page did not open.");
            if (!ImportMod(good)) throw new Exception("A good mod was refused: " + modStatus.Text);
            if (mods.All.Count != 1 || !File.Exists(Path.Combine(mods.Folder, "custom-names" + Mods.Extension))) throw new Exception("An imported mod was not saved.");
            if (!Hands.HeardName("hey nova, what time is it") || !Hands.HeardName("BIG Z open notepad") || !Hands.HeardName("big   z") || !Hands.HeardName("ask capricorn") || !Hands.HeardName("Capri, hello"))
                throw new Exception("An imported name was not heard.");
            if (Hands.HeardName("the supernova was bright") || Hands.HeardName("novas are bright") || Hands.HeardName("big zebra"))
                throw new Exception("Part of a word counted as her name.");
            if (Hands.NoteFor(Hands.KitFor()).IndexOf("They can also call you Big Z, Capricorn or Nova", StringComparison.Ordinal) < 0 && Hands.NoteFor(Hands.KitFor()).IndexOf("They can also call you Nova, Big Z or Capricorn", StringComparison.Ordinal) < 0)
                throw new Exception("Her brain was not told her nicknames.");
            checks.Add("PASS mods: an imported mod adds whole-word names and tells her brain");
            using (Bitmap page = new Bitmap(Width, Height))
            {
                DrawToBitmap(page, new Rectangle(Point.Empty, Size));
                page.Save(Path.Combine(output, "mods-page.png"), ImageFormat.Png);
            }
            SwitchMod(0);
            if (Hands.HeardName("hey nova") || !Hands.HeardName("hey capri") || Hands.NoteFor(Hands.KitFor()).Contains("They can also call you")) throw new Exception("Turning a mod off left its names on.");
            mods.Load();
            if (mods.All[0].On) throw new Exception("A mod turned off came back on after loading.");
            SwitchMod(0);
            if (!Hands.HeardName("hey nova")) throw new Exception("Turning a mod back on did not bring its names back.");
            checks.Add("PASS mods: on and off apply straight away and off survives a reload");
            string[,] bad =
            {
                { "bad-json", "{nope" },
                { "bad-format", "{\"format\":99,\"id\":\"a\",\"name\":\"A\",\"wake_names\":[\"Nova\"]}" },
                { "bad-path", "{\"format\":1,\"id\":\"../../evil\",\"name\":\"A\",\"wake_names\":[\"Nova\"]}" },
                { "bad-name", "{\"format\":1,\"id\":\"a\",\"name\":\"A\",\"wake_names\":[\".*\"]}" },
                { "bad-short", "{\"format\":1,\"id\":\"a\",\"name\":\"A\",\"wake_names\":[\"x\"]}" },
                { "bad-empty", "{\"format\":1,\"id\":\"a\",\"name\":\"A\",\"wake_names\":[]}" }
            };
            for (int i = 0; i < bad.GetLength(0); i++)
                if (ImportMod(sample(bad[i, 0] + Mods.Extension, bad[i, 1])) || mods.All.Count != 1) throw new Exception("A bad mod was let in: " + bad[i, 0]);
            if (ImportMod(sample("wrong.txt", "{}"))) throw new Exception("A file that isn't a mod was let in.");
            if (Directory.GetFiles(mods.Folder, "*" + Mods.Extension).Length != 1) throw new Exception("A refused mod was left in her folder.");
            checks.Add("PASS mods: broken files, bad ids and bad names are refused");
            RemoveMod(0);
            if (mods.All.Count != 0 || Hands.HeardName("hey nova")) throw new Exception("Removing a mod left it behind.");
            ShowSettings(true);
            if (modded || !paged || modStatus.Left < Far) throw new Exception("Settings did not take over from Mods.");
            ShowMods(true);
            if (paged || !modded || modStatus.Left >= Far) throw new Exception("Mods did not take over from Settings.");
            ShowMods(false);
            if (modded || modStatus.Left < Far) throw new Exception("Back did not leave Mods.");
            CheckModsKey();
            checks.Add("PASS mods key: Mods sits in the CAPRI band, Settings stays where it was and turns into Back on the Mods page, and nothing overlaps with her own font or the fallback");
            using (Bitmap page = new Bitmap(Width, Height))
            {
                DrawToBitmap(page, new Rectangle(Point.Empty, Size));
                page.Save(Path.Combine(output, "main-page.png"), ImageFormat.Png);
            }
            WindowState = wasState;
            checks.Add("PASS mods: removing works and Mods and Settings swap cleanly");
            string ears = sample("ears" + Mods.Extension, "{\"format\":1,\"id\":\"ears\",\"name\":\"Ears\",\"sounds_like\":true,\"heard_as\":[\"Coppering\"],\"wake_names\":[\"Choom\"]}");
            string[] missedBefore = { "Copri, what time is it", "hey coppery", "Yokapri what's up", "CAH Bree are you there", "Shum, what time is it", "hey coppering" };
            string[] stayQuiet = { "copy that file", "the copper ore", "a cobra", "show me the map", "that's a shame", "in the room", "keeper of the keys", "hum a song" };
            foreach (string line in missedBefore) if (Hands.HeardName(line)) throw new Exception("Heard a sound-alike with no mod on: " + line);
            if (!ImportMod(ears)) throw new Exception("A sounds_like mod was refused: " + modStatus.Text);
            foreach (string line in missedBefore) if (!Hands.HeardName(line)) throw new Exception("Sounds like missed: " + line);
            foreach (string line in stayQuiet) if (Hands.HeardName(line)) throw new Exception("Sounds like fired on: " + line);
            if (!Hands.NoteFor(Hands.KitFor()).Contains("They can also call you Choom.")) throw new Exception("Her brain wasn't told only her real nickname.");
            SwitchMod(0);
            if (Hands.HeardName("hey coppery") || !Hands.HeardName("hey capri")) throw new Exception("Turning sounds like off left it on.");
            RemoveMod(0);
            checks.Add("PASS mods: sounds like catches her ears' misspellings, skips everyday words and switches off cleanly");
            string log = sample("log" + Mods.Extension, "{\"format\":1,\"id\":\"log\",\"name\":\"Log\",\"hearing_log\":true}");
            mods.LogHearing("nothing on yet", false);
            if (File.Exists(mods.LogPath)) throw new Exception("She kept a hearing log with no mod asking.");
            if (!ImportMod(log)) throw new Exception("A hearing log mod was refused: " + modStatus.Text);
            mods.LogHearing("hello there", false);
            if (!File.ReadAllText(mods.LogPath).Contains("ignored | hello there")) throw new Exception("The hearing log didn't record a line.");
            RemoveMod(0);
            if (File.Exists(mods.LogPath)) throw new Exception("Removing the hearing log mod left its list behind.");
            checks.Add("PASS mods: the hearing log only runs while its mod is on, and goes with it");
            CheckDirectory(checks, output);
        }
    }
}
