// Настройки: модель распознавания, клавиши, город, задержка выключения.
using System.Drawing;
using System.Windows.Forms;

namespace Jarvis;

class SettingsForm : Form
{
    readonly CheckBox autoSendBox = new();
    readonly ComboBox modelCombo = new();
    readonly TextBox cityBox = new();
    readonly NumericUpDown shutdownBox = new();
    readonly TextBox voiceKeyBox = new();
    readonly TextBox windowKeyBox = new();

    public bool AutoSend => autoSendBox.Checked;
    public string City => cityBox.Text.Trim();
    public int ShutdownDelay => (int)shutdownBox.Value;
    public string SttModel => modelCombo.SelectedItem is ModelItem m ? m.File : null;
    public string VoiceHotkey => voiceKeyBox.Text.Trim();
    public string WindowHotkey => windowKeyBox.Text.Trim();

    sealed class ModelItem
    {
        public string File;
        public bool Embedded;
        public override string ToString() => Embedded ? File + " (встроенная)" : File;
    }

    public SettingsForm(AppConfig cfg, string modelDir, string voiceHotkey, string windowHotkey)
    {
        Text = "Настройки Джарвиса";
        Size = new Size(520, 380);
        MinimumSize = new Size(480, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(14),
            BackColor = Theme.Bg,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        autoSendBox.Text = "Сразу выполнять распознанное";
        autoSendBox.Checked = cfg.autoSendVoice;
        autoSendBox.ForeColor = Theme.Text;
        autoSendBox.AutoSize = true;

        modelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        modelCombo.BackColor = Theme.Panel;
        modelCombo.ForeColor = Theme.Text;
        foreach (var m in SttEngine.ListModels(modelDir))
            modelCombo.Items.Add(new ModelItem { File = m.File, Embedded = m.Embedded });
        for (int i = 0; i < modelCombo.Items.Count; i++)
            if (modelCombo.Items[i] is ModelItem mi && mi.File == cfg.sttModel)
                modelCombo.SelectedIndex = i;
        if (modelCombo.SelectedIndex < 0 && modelCombo.Items.Count > 0)
            modelCombo.SelectedIndex = 0;

        cityBox.Text = cfg.city;
        cityBox.BackColor = Theme.Panel;
        cityBox.ForeColor = Theme.Text;

        shutdownBox.Minimum = 5;
        shutdownBox.Maximum = 600;
        shutdownBox.Value = Math.Clamp(cfg.shutdownDelay, 5, 600);
        shutdownBox.BackColor = Theme.Panel;
        shutdownBox.ForeColor = Theme.Text;

        voiceKeyBox.Text = voiceHotkey;
        voiceKeyBox.BackColor = Theme.Panel;
        voiceKeyBox.ForeColor = Theme.Text;

        windowKeyBox.Text = windowHotkey;
        windowKeyBox.BackColor = Theme.Panel;
        windowKeyBox.ForeColor = Theme.Text;

        AddRow(table, "", autoSendBox);
        AddRow(table, "Модель распознавания", modelCombo);
        AddRow(table, "Город для погоды", cityBox);
        AddRow(table, "Задержка выключения, сек", shutdownBox);
        AddRow(table, "Клавиша «говорить»", voiceKeyBox);
        AddRow(table, "Клавиша окна", windowKeyBox);

        var hint = new Label
        {
            Text = "Клавиши: F1–F24, Num0–Num9 или сочетание с Ctrl, Alt, Shift. Пример: Num9, Ctrl+Alt+J.",
            ForeColor = Theme.Dim,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 36,
        };
        int hintRow = table.RowCount;
        table.RowCount = hintRow + 1;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.Controls.Add(hint, 0, hintRow);
        table.SetColumnSpan(hint, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(10),
            BackColor = Theme.Bg,
        };
        var ok = Theme.DarkButton("Сохранить");
        var cancel = Theme.DarkButton("Отмена");
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount;
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var l = new Label
        {
            Text = label,
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 4, 3, 4);
        table.Controls.Add(l, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
