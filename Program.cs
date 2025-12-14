using System.Runtime.InteropServices;
using System.Text.Json;

namespace StickyNotesApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new NotesManager());
        }
    }

    public class NotesManager : ApplicationContext
    {
        private List<StickyNote> notes = new List<StickyNote>();
        private bool notesVisible = true;
        private bool alwaysOnTop = true;
        private KeyboardHook? keyboardHook;
        private string saveFilePath;
        private string notesDirectory;
        private string lastUsedDirectory;

        public NotesManager()
        {
            notesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StickyNotes"
            );
            saveFilePath = Path.Combine(notesDirectory, "notes.json");
            lastUsedDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Directory.CreateDirectory(notesDirectory);

            keyboardHook = new KeyboardHook();
            keyboardHook.RegisterHotKey(Keys.H, KeyModifiers.Control);
            keyboardHook.RegisterHotKey(Keys.N, KeyModifiers.Control);
            keyboardHook.RegisterHotKey(Keys.L, KeyModifiers.Control);
            keyboardHook.RegisterHotKey(Keys.S, KeyModifiers.Control);
            keyboardHook.RegisterHotKey(Keys.S, KeyModifiers.Control | KeyModifiers.Shift);
            keyboardHook.RegisterHotKey(Keys.Q, KeyModifiers.Control | KeyModifiers.Shift);
            keyboardHook.KeyPressed += OnHotKeyPressed;

            LoadNotes();

            if (notes.Count == 0)
            {
                CreateNewNote();
            }
        }

        private void OnHotKeyPressed(object? sender, KeyPressedEventArgs e)
        {
            if (e.Modifier == KeyModifiers.Control)
            {
                if (e.Key == Keys.H)
                {
                    ToggleNotesVisibility();
                }
                else if (e.Key == Keys.N)
                {
                    CreateNewNote();
                }
                else if (e.Key == Keys.L)
                {
                    LoadNoteFromFile();
                }
                else if (e.Key == Keys.S)
                {
                    // Find active sticky note and save it
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is StickyNote note && (form.Focused || form.ContainsFocus))
                        {
                            note.SaveNote();
                            break;
                        }
                    }
                }
            }
            else if (e.Modifier == (KeyModifiers.Control | KeyModifiers.Shift))
            {
                if (e.Key == Keys.S)
                {
                    SaveAllNotes();
                }
                else if (e.Key == Keys.Q)
                {
                    CloseAllNotes();
                }
            }
        }

        public void SaveAllNotes()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is StickyNote note)
                {
                    note.SaveNote();
                }
            }
        }

        public void CloseAllNotes()
        {
            // Check if any notes have unsaved changes
            bool anyUnsaved = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is StickyNote note && note.HasUnsavedChanges())
                {
                    anyUnsaved = true;
                    break;
                }
            }

            if (anyUnsaved)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close all notes?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    // Close without saving
                }
                else if (result == DialogResult.No)
                {
                    // Cancel close operation
                    return;
                }
                else if (result == DialogResult.Cancel)
                {
                    // Save all and close
                    SaveAllNotes();
                }
                else
                {
                    return;
                }
            }

            var notesToClose = new List<StickyNote>();
            foreach (Form form in Application.OpenForms)
            {
                if (form is StickyNote note)
                {
                    notesToClose.Add(note);
                }
            }

            foreach (var note in notesToClose)
            {
                DeleteNote(note);
            }
        }

        public string GetLastUsedDirectory()
        {
            return lastUsedDirectory;
        }

        public void SetLastUsedDirectory(string directory)
        {
            lastUsedDirectory = directory;
        }

        public void ToggleAlwaysOnTop()
        {
            alwaysOnTop = !alwaysOnTop;
            foreach (var note in notes)
            {
                note.TopMost = alwaysOnTop;
            }
        }

        public bool IsAlwaysOnTop()
        {
            return alwaysOnTop;
        }

        public void ArrangeNotesGrid(bool startFromRight)
        {
            if (notes.Count == 0) return;

            var screen = Screen.PrimaryScreen!.WorkingArea;
            int padding = 2;
            int noteWidth = 300;
            int noteHeight = 250;

            // Calculate how many notes fit per column (vertical) - aiming for 4
            int notesPerColumn = 4;

            int startX = startFromRight ? screen.Right - noteWidth - padding : screen.Left + padding;
            int startY = screen.Top + padding;
            int deltaX = startFromRight ? -(noteWidth + padding) : (noteWidth + padding);

            for (int i = 0; i < notes.Count; i++)
            {
                int col = i / notesPerColumn;
                int row = i % notesPerColumn;

                int targetX = startX + (col * deltaX);
                int targetY = startY + (row * (noteHeight + padding));

                AnimateNoteToPosition(notes[i], new Point(targetX, targetY));
            }
        }

        private void AnimateNoteToPosition(StickyNote note, Point targetLocation)
        {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 10;

            Point startLocation = note.Location;
            int steps = 20;
            int currentStep = 0;

            timer.Tick += (s, e) =>
            {
                currentStep++;
                float progress = (float)currentStep / steps;

                // Easing function (ease-out)
                progress = 1 - (float)Math.Pow(1 - progress, 3);

                int newX = (int)(startLocation.X + (targetLocation.X - startLocation.X) * progress);
                int newY = (int)(startLocation.Y + (targetLocation.Y - startLocation.Y) * progress);

                note.Location = new Point(newX, newY);

                if (currentStep >= steps)
                {
                    note.Location = targetLocation;
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        private void ToggleNotesVisibility()
        {
            notesVisible = !notesVisible;
            foreach (var note in notes)
            {
                if (notesVisible)
                    note.Show();
                else
                    note.Hide();
            }
        }

        public void CreateNewNote()
        {
            var note = new StickyNote(this);
            // Center on screen
            var screen = Screen.PrimaryScreen!.WorkingArea;
            note.Location = new Point(
                screen.Left + (screen.Width - note.Width) / 2,
                screen.Top + (screen.Height - note.Height) / 2
            );

            // Check for overlapping notes and adjust position
            note.Location = GetNonOverlappingPosition(note.Location, note.Size, screen);

            notes.Add(note);
            note.Show();
            SaveNotes();
        }

        private Point GetNonOverlappingPosition(Point desiredLocation, Size noteSize, Rectangle screen)
        {
            const int offset = 30;
            Point newLocation = desiredLocation;

            // Check if any existing note overlaps
            foreach (var existingNote in notes)
            {
                Rectangle existingRect = new Rectangle(existingNote.Location, existingNote.Size);
                Rectangle newRect = new Rectangle(newLocation, noteSize);

                if (existingRect.IntersectsWith(newRect))
                {
                    // Try to offset to the right
                    newLocation.X += offset;
                    newLocation.Y += offset;

                    // Check if going off right edge
                    if (newLocation.X + noteSize.Width > screen.Right)
                    {
                        // Move to left instead
                        newLocation.X = desiredLocation.X - offset;
                        newLocation.Y += offset;
                    }

                    // Check if going off left edge
                    if (newLocation.X < screen.Left)
                    {
                        newLocation.X = screen.Left + 20;
                    }

                    // Check if going off bottom
                    if (newLocation.Y + noteSize.Height > screen.Bottom)
                    {
                        newLocation.Y = screen.Top + 20;
                    }
                }
            }

            return newLocation;
        }

        public void LoadNoteFromFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = notesDirectory;
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Load Note";
                dialog.Multiselect = true; // Çoklu seçim aktif

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var screen = Screen.PrimaryScreen!.WorkingArea;

                    for (int i = 0; i < dialog.FileNames.Length; i++)
                    {
                        try
                        {
                            var json = File.ReadAllText(dialog.FileNames[i]);
                            var data = JsonSerializer.Deserialize<NoteData>(json);

                            if (data != null)
                            {
                                var note = new StickyNote(this, data);

                                // Dosya adını not isminden al
                                string fileName = Path.GetFileNameWithoutExtension(dialog.FileNames[i]);
                                note.SetNoteName(fileName);
                                note.SetLastSavedPath(dialog.FileNames[i]);

                                // Kaydedilmiş konumda aç
                                note.Location = new Point(data.X, data.Y);
                                note.Size = new Size(data.Width, data.Height);

                                // Check for overlapping and adjust
                                note.Location = GetNonOverlappingPosition(note.Location, note.Size, screen);

                                notes.Add(note);
                                note.Show();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to load note '{Path.GetFileName(dialog.FileNames[i])}': {ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    SaveNotes();
                }
            }
        }

        public void DeleteNote(StickyNote note)
        {
            notes.Remove(note);
            note.Close();
            SaveNotes();

            if (notes.Count == 0)
            {
                ExitThread();
            }
        }

        public void SaveNotes()
        {
            var notesData = notes.Select(n => n.GetNoteData()).ToList();
            var json = JsonSerializer.Serialize(notesData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(saveFilePath, json);
        }

        private void LoadNotes()
        {
            if (File.Exists(saveFilePath))
            {
                try
                {
                    var json = File.ReadAllText(saveFilePath);
                    var notesData = JsonSerializer.Deserialize<List<NoteData>>(json);

                    if (notesData != null)
                    {
                        foreach (var data in notesData)
                        {
                            var note = new StickyNote(this, data);
                            notes.Add(note);
                            note.Show();
                        }
                    }
                }
                catch { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            keyboardHook?.Dispose();
            base.Dispose(disposing);
        }
    }

    public class StickyNote : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private NotesManager manager;
        private RichTextBox noteTextBox;
        private Panel menuPanel;
        private ToolStrip toolStrip;
        private bool isResizing = false;
        private Point resizeStartPoint;
        private Size resizeStartSize;
        private string noteName = "Unnamed";
        private System.Windows.Forms.Timer? saveIndicatorTimer;
        private string? lastSavedFilePath = null;
        private Dictionary<string, string> fileLinks = new Dictionary<string, string>(); // displayName -> filePath

        public StickyNote(NotesManager manager, NoteData? data = null)
        {
            this.manager = manager;

            InitializeUI();

            if (data != null)
            {
                noteTextBox.Rtf = data.RtfText;
                noteTextBox.BackColor = ColorTranslator.FromHtml(data.BackColor);
                noteTextBox.ForeColor = ColorTranslator.FromHtml(data.ForeColor);
                noteName = data.NoteName;
                lastSavedFilePath = data.LastSavedPath;
                if (data.FileLinks != null)
                {
                    fileLinks = new Dictionary<string, string>(data.FileLinks);
                }
                Location = new Point(data.X, data.Y);
                Size = new Size(data.Width, data.Height);
            }
        }

        private void InitializeUI()
        {
            // Form settings
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(300, 250);
            MinimumSize = new Size(200, 150);
            TopMost = manager.IsAlwaysOnTop();
            ShowInTaskbar = false;
            Opacity = 0.95;
            BackColor = Color.FromArgb(45, 45, 48);

            // Menu panel
            menuPanel = new Panel();
            menuPanel.Dock = DockStyle.Top;
            menuPanel.Height = 30;
            menuPanel.BackColor = Color.FromArgb(37, 37, 38);
            menuPanel.MouseDown += MenuPanel_MouseDown;

            // Custom MenuStrip-like toolbar
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Fill;
            toolStrip.BackColor = Color.FromArgb(37, 37, 38);
            toolStrip.ForeColor = Color.White;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Renderer = new DarkToolStripRenderer();
            toolStrip.MouseDown += MenuPanel_MouseDown;

            // Menu dropdown
            var menuButton = new ToolStripDropDownButton("☰");
            menuButton.ForeColor = Color.White;
            menuButton.DisplayStyle = ToolStripItemDisplayStyle.Text;

            var saveItem = new ToolStripMenuItem("Save", null, (s, e) => SaveNote());
            saveItem.ForeColor = Color.White;
            saveItem.ShowShortcutKeys = false;  // Menüde gösterme, global hotkey olarak çalışıyor

            var saveAllItem = new ToolStripMenuItem("Save All", null, (s, e) => manager.SaveAllNotes());
            saveAllItem.ForeColor = Color.White;
            saveAllItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAllItem.ShowShortcutKeys = true;

            var loadItem = new ToolStripMenuItem("Load Note...", null, (s, e) => manager.LoadNoteFromFile());
            loadItem.ForeColor = Color.White;
            loadItem.ShortcutKeys = Keys.Control | Keys.L;
            loadItem.ShowShortcutKeys = true;

            var newItem = new ToolStripMenuItem("New Note", null, (s, e) => manager.CreateNewNote());
            newItem.ForeColor = Color.White;
            newItem.ShortcutKeys = Keys.Control | Keys.N;
            newItem.ShowShortcutKeys = true;

            var hideItem = new ToolStripMenuItem("Hide", null, (s, e) => this.Hide());
            hideItem.ForeColor = Color.White;

            var hideAllItem = new ToolStripMenuItem("Hide All", null, (s, e) => HideAllNotes());
            hideAllItem.ForeColor = Color.White;
            hideAllItem.ShortcutKeys = Keys.Control | Keys.H;
            hideAllItem.ShowShortcutKeys = true;

            var deleteItem = new ToolStripMenuItem("Close", null, (s, e) => manager.DeleteNote(this));
            deleteItem.ForeColor = Color.White;
            deleteItem.ShortcutKeys = Keys.Control | Keys.Q;
            deleteItem.ShowShortcutKeys = true;

            var closeAllItem = new ToolStripMenuItem("Close All", null, (s, e) => manager.CloseAllNotes());
            closeAllItem.ForeColor = Color.White;
            closeAllItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.Q;
            closeAllItem.ShowShortcutKeys = true;

            var bgColorItem = new ToolStripMenuItem("Background Color", null, (s, e) => ChangeBackgroundColor());
            bgColorItem.ForeColor = Color.White;

            var fgColorItem = new ToolStripMenuItem("Font Color", null, (s, e) => ChangeFontColor());
            fgColorItem.ForeColor = Color.White;

            var alwaysOnTopItem = new ToolStripMenuItem("Always on Top", null, (s, e) => {
                var menuItem = s as ToolStripMenuItem;
                manager.ToggleAlwaysOnTop();
                if (menuItem != null)
                {
                    menuItem.Checked = manager.IsAlwaysOnTop();
                }
            });
            alwaysOnTopItem.ForeColor = Color.White;
            alwaysOnTopItem.Checked = manager.IsAlwaysOnTop();
            alwaysOnTopItem.CheckOnClick = false;

            var arrangeMenu = new ToolStripMenuItem("Arrange Grid");
            arrangeMenu.ForeColor = Color.White;

            var arrangeRightItem = new ToolStripMenuItem("From Top Right", null, (s, e) => manager.ArrangeNotesGrid(true));
            arrangeRightItem.ForeColor = Color.White;

            var arrangeLeftItem = new ToolStripMenuItem("From Top Left", null, (s, e) => manager.ArrangeNotesGrid(false));
            arrangeLeftItem.ForeColor = Color.White;

            arrangeMenu.DropDownItems.AddRange(new ToolStripItem[] { arrangeRightItem, arrangeLeftItem });

            menuButton.DropDownItems.AddRange(new ToolStripItem[] {
                saveItem, saveAllItem, loadItem, newItem, hideItem, hideAllItem, deleteItem, closeAllItem,
                new ToolStripSeparator(), bgColorItem, fgColorItem, alwaysOnTopItem,
                new ToolStripSeparator(), arrangeMenu
            });

            toolStrip.Items.Add(menuButton);
            toolStrip.Items.Add(new ToolStripSeparator());

            // Note name textbox
            var nameTextBox = new ToolStripTextBox();
            nameTextBox.Width = 100;
            nameTextBox.Text = noteName;
            nameTextBox.BackColor = Color.FromArgb(60, 60, 60);
            nameTextBox.ForeColor = Color.White;
            nameTextBox.BorderStyle = BorderStyle.FixedSingle;
            nameTextBox.TextChanged += (s, e) => {
                noteName = nameTextBox.Text;
                manager.SaveNotes();
            };
            toolStrip.Items.Add(nameTextBox);

            // Save status indicator
            var saveStatusLabel = new ToolStripLabel("");
            saveStatusLabel.Name = "saveStatus";
            saveStatusLabel.ForeColor = Color.LightGreen;
            saveStatusLabel.Alignment = ToolStripItemAlignment.Right;
            toolStrip.Items.Add(saveStatusLabel);

            toolStrip.Items.Add(new ToolStripSeparator());

            // Bold button
            var boldBtn = new ToolStripButton("B");
            boldBtn.ForeColor = Color.White;
            boldBtn.Font = new Font(boldBtn.Font, FontStyle.Bold);
            boldBtn.Click += (s, e) => ToggleFontStyle(FontStyle.Bold);
            toolStrip.Items.Add(boldBtn);

            // Italic button
            var italicBtn = new ToolStripButton("I");
            italicBtn.ForeColor = Color.White;
            italicBtn.Font = new Font(italicBtn.Font, FontStyle.Italic);
            italicBtn.Click += (s, e) => ToggleFontStyle(FontStyle.Italic);
            toolStrip.Items.Add(italicBtn);

            // Bullets button
            var bulletsBtn = new ToolStripButton("•");
            bulletsBtn.ForeColor = Color.White;
            bulletsBtn.Click += (s, e) => ToggleBullets();
            toolStrip.Items.Add(bulletsBtn);

            // Checkbox button
            var checkboxBtn = new ToolStripButton("☐");
            checkboxBtn.ForeColor = Color.White;
            checkboxBtn.Click += (s, e) => InsertCheckbox();
            toolStrip.Items.Add(checkboxBtn);

            menuPanel.Controls.Add(toolStrip);

            // Text box (using RichTextBox for better formatting)
            noteTextBox = new RichTextBox();
            noteTextBox.Dock = DockStyle.Fill;
            noteTextBox.BorderStyle = BorderStyle.None;
            noteTextBox.BackColor = Color.FromArgb(45, 45, 48);
            noteTextBox.ForeColor = Color.White;
            noteTextBox.Font = new Font("Segoe UI", 11);
            noteTextBox.TextChanged += (s, e) => {
                // Reset any blue text that's not a file link
                if (noteTextBox.SelectionLength == 0 && noteTextBox.SelectionColor == Color.LightBlue)
                {
                    noteTextBox.SelectionColor = noteTextBox.ForeColor;
                    noteTextBox.SelectionFont = new Font(noteTextBox.Font, FontStyle.Regular);
                }
                hasUnsavedChanges = true;
                manager.SaveNotes();
            };
            noteTextBox.LinkClicked += NoteTextBox_LinkClicked;
            noteTextBox.DetectUrls = true;
            noteTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            noteTextBox.KeyDown += NoteTextBox_KeyDown;
            noteTextBox.SelectionChanged += NoteTextBox_SelectionChanged;
            noteTextBox.AllowDrop = true;
            noteTextBox.DragEnter += NoteTextBox_DragEnter;
            noteTextBox.DragDrop += NoteTextBox_DragDrop;
            noteTextBox.MouseDown += NoteTextBox_MouseDown;

            Controls.Add(noteTextBox);
            Controls.Add(menuPanel);

            // Handle Delete key
            KeyPreview = true;
            KeyDown += StickyNote_KeyDown;

            // Handle Ctrl+S and Ctrl+L globally
            this.KeyDown += StickyNote_GlobalKeyDown;

            // Handle checkbox clicks
            noteTextBox.MouseDown += NoteTextBox_MouseDown;

            // Mouse events for resizing
            this.MouseDown += Form_MouseDown;
            this.MouseMove += Form_MouseMove;
            this.MouseUp += Form_MouseUp;
            this.Paint += (s, e) => DrawResizeGrip(e.Graphics);

            // Focus on mouse enter
            this.MouseEnter += (s, e) => this.Focus();
            noteTextBox.MouseEnter += (s, e) => this.Focus();
            menuPanel.MouseEnter += (s, e) => this.Focus();

            // Remove focus on mouse leave
            this.MouseLeave += (s, e) => {
                // Check if mouse is not over any child controls
                if (!this.ClientRectangle.Contains(this.PointToClient(Cursor.Position)))
                {
                    // Move focus away (to nothing)
                    IntPtr handle = GetDesktopWindow();
                    SetFocus(handle);
                }
            };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        private void DrawResizeGrip(Graphics g)
        {
            // Draw resize grip in bottom-right corner
            int size = 12;
            int x = this.Width - size - 2;
            int y = this.Height - size - 2;

            using (Pen pen = new Pen(Color.FromArgb(100, 100, 100), 2))
            {
                for (int i = 0; i < 3; i++)
                {
                    g.DrawLine(pen, x + i * 4, y + size, x + size, y + i * 4);
                }
            }
        }

        private void Form_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Check if click is in resize area (bottom-right corner)
                int resizeAreaSize = 20;
                if (e.X >= this.Width - resizeAreaSize && e.Y >= this.Height - resizeAreaSize)
                {
                    isResizing = true;
                    resizeStartPoint = this.PointToScreen(e.Location);
                    resizeStartSize = this.Size;
                }
            }
        }

        private void Form_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                Point currentPoint = this.PointToScreen(e.Location);
                int deltaX = currentPoint.X - resizeStartPoint.X;
                int deltaY = currentPoint.Y - resizeStartPoint.Y;

                int newWidth = resizeStartSize.Width + deltaX;
                int newHeight = resizeStartSize.Height + deltaY;

                if (newWidth >= MinimumSize.Width)
                    this.Width = newWidth;
                if (newHeight >= MinimumSize.Height)
                    this.Height = newHeight;

                this.Invalidate(); // Redraw resize grip
            }
        }

        private void Form_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                isResizing = false;
                manager.SaveNotes();
            }
        }

        private void MenuPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !isResizing)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void ToggleFontStyle(FontStyle style)
        {
            if (noteTextBox.SelectionLength > 0)
            {
                FontStyle currentStyle = noteTextBox.SelectionFont?.Style ?? FontStyle.Regular;
                FontStyle newStyle = currentStyle ^ style; // Toggle the style
                noteTextBox.SelectionFont = new Font(noteTextBox.SelectionFont?.FontFamily ?? noteTextBox.Font.FontFamily,
                    noteTextBox.SelectionFont?.Size ?? noteTextBox.Font.Size, newStyle);
            }
            manager.SaveNotes();
        }

        private void ToggleBullets()
        {
            noteTextBox.SelectionBullet = !noteTextBox.SelectionBullet;
            manager.SaveNotes();
        }

        private void ChangeFontSize(int delta)
        {
            if (noteTextBox.SelectionLength > 0)
            {
                // Seçili metin varsa
                int selStart = noteTextBox.SelectionStart;
                int selLength = noteTextBox.SelectionLength;

                // Orijinal RTF'i sakla
                string originalRtf = noteTextBox.Rtf;

                try
                {
                    // Her karakter için ayrı ayrı font ayarla
                    for (int i = 0; i < selLength; i++)
                    {
                        noteTextBox.Select(selStart + i, 1);

                        Font currentFont = noteTextBox.SelectionFont;
                        if (currentFont != null)
                        {
                            float newSize = currentFont.Size + delta;
                            if (newSize >= 6 && newSize <= 72)
                            {
                                // Mevcut style'ı koru
                                FontStyle style = currentFont.Style;
                                noteTextBox.SelectionFont = new Font(
                                    currentFont.FontFamily,
                                    newSize,
                                    style
                                );
                            }
                        }
                    }
                }
                catch
                {
                    noteTextBox.Rtf = originalRtf;
                }

                // Seçimi geri yükle
                noteTextBox.Select(selStart, selLength);
            }
            else
            {
                // Seçili metin yoksa - TÜM metni değiştir
                string originalRtf = noteTextBox.Rtf;
                int originalPos = noteTextBox.SelectionStart;

                try
                {
                    // Tüm karakterleri değiştir
                    for (int i = 0; i < noteTextBox.TextLength; i++)
                    {
                        noteTextBox.Select(i, 1);

                        Font currentFont = noteTextBox.SelectionFont;
                        if (currentFont != null)
                        {
                            float newSize = currentFont.Size + delta;
                            if (newSize >= 6 && newSize <= 72)
                            {
                                noteTextBox.SelectionFont = new Font(
                                    currentFont.FontFamily,
                                    newSize,
                                    currentFont.Style
                                );
                            }
                        }
                    }

                    // Varsayılan fontu da güncelle
                    float newDefaultSize = noteTextBox.Font.Size + delta;
                    if (newDefaultSize >= 6 && newDefaultSize <= 72)
                    {
                        noteTextBox.Font = new Font(noteTextBox.Font.FontFamily, newDefaultSize, noteTextBox.Font.Style);
                    }
                }
                catch
                {
                    noteTextBox.Rtf = originalRtf;
                }

                // Cursor pozisyonunu geri yükle
                noteTextBox.Select(originalPos, 0);
            }
            manager.SaveNotes();
        }

        private void NoteTextBox_SelectionChanged(object? sender, EventArgs e)
        {
            // Yeni yazılan metin için mevcut cursor pozisyonundaki formatı kullan
            // Hiçbir şey yapma - RichTextBox otomatik olarak mevcut formatı kullanacak
        }

        private void HideAllNotes()
        {
            // Tüm notları gizle
            foreach (Form form in Application.OpenForms)
            {
                if (form is StickyNote && form != this)
                {
                    form.Hide();
                }
            }
            this.Hide();
        }

        public void SaveNote()
        {
            // Eğer daha önce kaydedilmişse, aynı yere kaydet
            if (!string.IsNullOrEmpty(lastSavedFilePath) && File.Exists(lastSavedFilePath))
            {
                try
                {
                    var data = GetNoteData();
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(lastSavedFilePath, json);

                    hasUnsavedChanges = false;
                    ShowSaveSuccess();
                    manager.SaveNotes();
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save note: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // İlk kez kaydediliyorsa dialog göster
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Save Note";
                dialog.FileName = noteName + ".json";
                dialog.InitialDirectory = manager.GetLastUsedDirectory();

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        lastSavedFilePath = dialog.FileName;
                        manager.SetLastUsedDirectory(Path.GetDirectoryName(dialog.FileName) ?? manager.GetLastUsedDirectory());

                        var data = GetNoteData();
                        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(lastSavedFilePath, json);

                        ShowSaveSuccess();
                        manager.SaveNotes();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to save note: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowSaveSuccess()
        {
            var saveStatus = toolStrip.Items.Find("saveStatus", false).FirstOrDefault() as ToolStripLabel;
            if (saveStatus != null)
            {
                saveStatus.Text = "✓";

                // Hide after 2 seconds
                saveIndicatorTimer?.Stop();
                saveIndicatorTimer = new System.Windows.Forms.Timer();
                saveIndicatorTimer.Interval = 2000;
                saveIndicatorTimer.Tick += (s, e) => {
                    saveStatus.Text = "";
                    saveIndicatorTimer?.Stop();
                };
                saveIndicatorTimer.Start();
            }
        }

        private void StickyNote_GlobalKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.Q)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    manager.DeleteNote(this);
                }
            }
        }

        private void NoteTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.D1: // Ctrl+1 - Header
                        e.SuppressKeyPress = true;
                        MakeHeader();
                        break;
                    case Keys.D2: // Ctrl+2 - Bullets
                        e.SuppressKeyPress = true;
                        ToggleBullets();
                        break;
                    case Keys.D3: // Ctrl+3 - Italic
                        e.SuppressKeyPress = true;
                        MakeItalic();
                        break;
                    case Keys.D4: // Ctrl+4 - Checkbox
                        e.SuppressKeyPress = true;
                        InsertCheckbox();
                        break;
                }
            }
        }

        private void MakeHeader()
        {
            int currentIndex = noteTextBox.SelectionStart;

            // Find the start and end of the current line
            string text = noteTextBox.Text;
            int lineStart = currentIndex;
            int lineEnd = currentIndex;

            // Find line start (go backwards to find newline or start of text)
            while (lineStart > 0 && text[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            // Find line end (go forwards to find newline or end of text)
            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
            {
                lineEnd++;
            }

            int lineLength = lineEnd - lineStart;
            if (lineLength <= 0) return;

            // Save relative cursor position
            int relativePos = currentIndex - lineStart;

            // Get the line text
            string lineText = text.Substring(lineStart, lineLength);

            // Remove the old line
            noteTextBox.Select(lineStart, lineLength);
            noteTextBox.SelectedText = "";

            // Insert the line with header formatting
            noteTextBox.Select(lineStart, 0);
            noteTextBox.SelectionFont = new Font(noteTextBox.Font.FontFamily, 16, FontStyle.Bold);
            noteTextBox.SelectedText = lineText;

            // Reset font to normal for the rest of the document
            // Move cursor to end of the header line
            int cursorPos = lineStart + lineText.Length;
            noteTextBox.Select(cursorPos, 0);

            // Set default font for future typing
            noteTextBox.SelectionFont = new Font(noteTextBox.Font.FontFamily, 10, FontStyle.Regular);

            // Restore cursor position within the header line
            noteTextBox.Select(lineStart + Math.Min(relativePos, lineText.Length), 0);

            manager.SaveNotes();
        }

        private void MakeItalic()
        {
            // Get current line
            int currentIndex = noteTextBox.SelectionStart;
            int lineStart = noteTextBox.GetFirstCharIndexOfCurrentLine();
            int lineEnd;

            // Find end of line
            int currentLine = noteTextBox.GetLineFromCharIndex(currentIndex);
            int nextLineStart = noteTextBox.GetFirstCharIndexFromLine(currentLine + 1);
            if (nextLineStart == -1)
            {
                lineEnd = noteTextBox.TextLength;
            }
            else
            {
                lineEnd = nextLineStart - 1;
                if (lineEnd > lineStart && noteTextBox.Text[lineEnd - 1] == '\n')
                    lineEnd--;
            }

            int lineLength = lineEnd - lineStart;
            if (lineLength <= 0) return;

            // Save current position relative to line start
            int relativePos = currentIndex - lineStart;

            // Select the line
            noteTextBox.Select(lineStart, lineLength);

            // Toggle italic
            FontStyle currentStyle = noteTextBox.SelectionFont?.Style ?? FontStyle.Regular;
            FontStyle newStyle = currentStyle ^ FontStyle.Italic;
            noteTextBox.SelectionFont = new Font(noteTextBox.SelectionFont?.FontFamily ?? noteTextBox.Font.FontFamily,
                noteTextBox.SelectionFont?.Size ?? noteTextBox.Font.Size, newStyle);

            // Restore cursor position
            noteTextBox.Select(lineStart + Math.Min(relativePos, lineLength), 0);
            manager.SaveNotes();
        }

        private void InsertCheckbox()
        {
            int currentPos = noteTextBox.SelectionStart;

            // Insert checkbox at current cursor position
            noteTextBox.Select(currentPos, 0);
            noteTextBox.SelectedText = "☐ ";

            // Move cursor after the checkbox
            noteTextBox.Select(currentPos + 2, 0);
            manager.SaveNotes();
        }

        private void NoteTextBox_LinkClicked(object? sender, LinkClickedEventArgs e)
        {
            try
            {
                string linkText = e.LinkText ?? "";

                // Check if it's a file:/// protocol
                if (linkText.StartsWith("file:///"))
                {
                    string filePath = linkText.Substring(8).Replace("/", "\\");

                    if (File.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        if (MessageBox.Show($"File not found:\n{filePath}\n\nDo you want to remove this link?",
                            "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                        {
                            // Remove the link text
                            int linkStart = noteTextBox.Text.IndexOf(linkText);
                            if (linkStart >= 0)
                            {
                                noteTextBox.Select(linkStart, linkText.Length);
                                noteTextBox.SelectedText = "";
                            }
                        }
                    }
                }
                else if (File.Exists(linkText))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = linkText,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Try as URL
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = linkText,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open: {e.LinkText}\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void NoteTextBox_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void NoteTextBox_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                int cursorPos = noteTextBox.SelectionStart;

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileName(filePath);
                    string uniqueKey = $" 📎 {fileName}";

                    // Store the file path mapping
                    fileLinks[uniqueKey] = filePath;

                    // Insert only the file name at cursor position
                    noteTextBox.Select(cursorPos, 0);
                    Font currentFont = noteTextBox.SelectionFont ?? noteTextBox.Font;
                    noteTextBox.SelectionColor = Color.LightBlue;
                    noteTextBox.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, FontStyle.Underline);
                    noteTextBox.SelectedText = uniqueKey + " ";
                    noteTextBox.SelectionColor = noteTextBox.ForeColor;
                    noteTextBox.SelectionFont = currentFont;

                    cursorPos = noteTextBox.SelectionStart;
                }

                manager.SaveNotes();
            }
        }

        private void StickyNote_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Delete)
            {
                manager.DeleteNote(this);
            }
        }

        private DateTime lastClickTime = DateTime.MinValue;
        private bool hasUnsavedChanges;

        private void NoteTextBox_MouseDown(object? sender, MouseEventArgs e)
        {
            // Only handle actual mouse clicks, not keyboard events
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 0) return; // Ignore if not a real click

            // Prevent double-click by checking time
            TimeSpan timeSinceLastClick = DateTime.Now - lastClickTime;
            if (timeSinceLastClick.TotalMilliseconds < 300)
            {
                return;
            }
            lastClickTime = DateTime.Now;

            // Check if click is on a checkbox
            int charIndex = noteTextBox.GetCharIndexFromPosition(e.Location);
            if (charIndex >= 0 && charIndex < noteTextBox.TextLength)
            {
                string clickedChar = noteTextBox.Text.Substring(charIndex, 1);

                if (clickedChar == "☐" || clickedChar == "☑")
                {
                    // Toggle checkbox
                    noteTextBox.Select(charIndex, 1);
                    noteTextBox.SelectedText = clickedChar == "☐" ? "☑" : "☐";
                    noteTextBox.Select(charIndex + 1, 0);
                    manager.SaveNotes();
                    return;
                }

                // Check if we clicked on a file link
                string text = noteTextBox.Text;

                // Check all file links to see if click is within any of them
                foreach (var kvp in fileLinks)
                {
                    int linkStart = text.IndexOf(kvp.Key);
                    if (linkStart >= 0)
                    {
                        int linkEnd = linkStart + kvp.Key.Length;

                        // Check if click is within this link
                        if (charIndex >= linkStart && charIndex < linkEnd)
                        {
                            string filePath = kvp.Value;

                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = filePath,
                                        UseShellExecute = true
                                    });
                                }
                                else
                                {
                                    if (MessageBox.Show($"File not found:\n{filePath}\n\nDo you want to remove this link?",
                                        "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                                    {
                                        noteTextBox.Select(linkStart, kvp.Key.Length);
                                        noteTextBox.SelectedText = "";
                                        fileLinks.Remove(kvp.Key);
                                        manager.SaveNotes();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Cannot open file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            return;
                        }
                    }
                }
            }
        }

        private void ChangeBackgroundColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = noteTextBox.BackColor;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    noteTextBox.BackColor = dialog.Color;
                    this.BackColor = dialog.Color;
                    manager.SaveNotes();
                }
            }
        }

        private void ChangeFontColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = noteTextBox.SelectionColor;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (noteTextBox.SelectionLength > 0)
                    {
                        noteTextBox.SelectionColor = dialog.Color;
                    }
                    else
                    {
                        noteTextBox.ForeColor = dialog.Color;
                    }
                    manager.SaveNotes();
                }
            }
        }

        public NoteData GetNoteData()
        {
            return new NoteData
            {
                RtfText = noteTextBox.Rtf,
                NoteName = noteName,
                LastSavedPath = lastSavedFilePath,
                FileLinks = fileLinks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                X = Location.X,
                Y = Location.Y,
                Width = Width,
                Height = Height,
                BackColor = ColorTranslator.ToHtml(noteTextBox.BackColor),
                ForeColor = ColorTranslator.ToHtml(noteTextBox.ForeColor)
            };
        }

        public void SetNoteName(string name)
        {
            noteName = name;
            // Update the textbox in the toolbar
            var nameTextBox = toolStrip.Items.OfType<ToolStripTextBox>().FirstOrDefault();
            if (nameTextBox != null)
            {
                nameTextBox.Text = name;
            }
        }

        public void SetLastSavedPath(string path)
        {
            lastSavedFilePath = path;
        }

        public bool HasUnsavedChanges()
        {
            return hasUnsavedChanges;
        }
    }

    // Custom renderer for dark theme toolbar
    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // No border
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(37, 37, 38);
        public override Color ImageMarginGradientBegin => Color.FromArgb(37, 37, 38);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(37, 37, 38);
        public override Color ImageMarginGradientEnd => Color.FromArgb(37, 37, 38);
        public override Color MenuBorder => Color.FromArgb(60, 60, 60);
        public override Color MenuItemBorder => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 64);
        public override Color MenuStripGradientBegin => Color.FromArgb(37, 37, 38);
        public override Color MenuStripGradientEnd => Color.FromArgb(37, 37, 38);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 64);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(37, 37, 38);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(37, 37, 38);
    }

    public class NoteData
    {
        public string RtfText { get; set; } = "";
        public string NoteName { get; set; } = "Unnamed";
        public string? LastSavedPath { get; set; }
        public Dictionary<string, string>? FileLinks { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string BackColor { get; set; } = "#2D2D30";
        public string ForeColor { get; set; } = "#FFFFFF";
    }

    // Keyboard hook for global hotkeys
    public sealed class KeyboardHook : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private class Window : NativeWindow, IDisposable
        {
            private static int WM_HOTKEY = 0x0312;

            public Window()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_HOTKEY)
                {
                    Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    KeyModifiers modifier = (KeyModifiers)((int)m.LParam & 0xFFFF);

                    KeyPressed?.Invoke(this, new KeyPressedEventArgs(modifier, key));
                }
            }

            public event EventHandler<KeyPressedEventArgs>? KeyPressed;

            public void Dispose()
            {
                DestroyHandle();
            }
        }

        private Window window = new Window();
        private int currentId;

        public KeyboardHook()
        {
            window.KeyPressed += (s, e) => KeyPressed?.Invoke(this, e);
        }

        public void RegisterHotKey(Keys key, KeyModifiers modifiers)
        {
            currentId++;
            if (!RegisterHotKey(window.Handle, currentId, (uint)modifiers, (uint)key))
            {
                throw new InvalidOperationException("Couldn't register the hot key.");
            }
        }

        public event EventHandler<KeyPressedEventArgs>? KeyPressed;

        public void Dispose()
        {
            for (int i = currentId; i > 0; i--)
            {
                UnregisterHotKey(window.Handle, i);
            }

            window.Dispose();
        }
    }

    public class KeyPressedEventArgs : EventArgs
    {
        public KeyModifiers Modifier { get; private set; }
        public Keys Key { get; private set; }

        internal KeyPressedEventArgs(KeyModifiers modifier, Keys key)
        {
            Modifier = modifier;
            Key = key;
        }
    }

    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }
}