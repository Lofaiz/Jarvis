// Тёмная тема: цвета и кнопки — как в Милли, чтобы Джарвис выглядел знакомо.
using System.Windows.Forms;

namespace Jarvis;

static class Theme
{
    public static readonly Color Bg     = Color.FromArgb(30, 31, 34);
    public static readonly Color Panel  = Color.FromArgb(43, 45, 48);
    public static readonly Color Text   = Color.FromArgb(226, 226, 226);
    public static readonly Color Dim    = Color.FromArgb(150, 155, 160);
    public static readonly Color Accent = Color.FromArgb(108, 182, 255);
    public static readonly Color User   = Color.FromArgb(150, 155, 160);
    public static readonly Color Error  = Color.FromArgb(255, 107, 107);
    public static readonly Color Ok     = Color.FromArgb(126, 217, 130);

    public static Button DarkButton(string text)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Text,
            Margin = new Padding(3),
            Padding = new Padding(6, 3, 6, 3),
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 84, 88);
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 65, 68);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(52, 55, 58);
        return b;
    }
}

class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(62, 65, 68);
    public override Color MenuItemBorder => Color.FromArgb(62, 65, 68);
    public override Color MenuBorder => Color.FromArgb(80, 80, 80);
    public override Color ToolStripDropDownBackground => Color.FromArgb(43, 45, 48);
    public override Color ImageMarginGradientBegin => Color.FromArgb(43, 45, 48);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 45, 48);
    public override Color ImageMarginGradientEnd => Color.FromArgb(43, 45, 48);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 65, 68);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 65, 68);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(43, 45, 48);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(43, 45, 48);
    public override Color SeparatorDark => Color.FromArgb(80, 80, 80);
    public override Color SeparatorLight => Color.FromArgb(80, 80, 80);
}

class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }
}
