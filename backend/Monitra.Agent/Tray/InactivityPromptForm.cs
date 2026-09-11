using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Monitra.Agent.Tray;

/// <summary>
/// The interactive prompt shown when idle time crosses the tenant's configured threshold.
/// This is the whole reason the agent had to stop being a Windows Service - Session 0
/// isolation would block a service from showing this at all.
/// </summary>
[SupportedOSPlatform("windows")]
public class InactivityPromptForm : Form
{
    public string? SelectedReason { get; private set; }

    private static readonly string[] QuickReasons =
    {
        "In a meeting",
        "On a break",
        "Technical issue"
    };

    public InactivityPromptForm(int idleMinutes)
    {
        Text = "Monitra";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(360, 260);

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        Location = new Point(workingArea.Right - Width - 16, workingArea.Bottom - Height - 16);

        var heading = new Label
        {
            Text = $"You've been inactive for {idleMinutes} minutes.",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = false,
            Size = new Size(320, 40),
            Location = new Point(20, 16)
        };

        var subheading = new Label
        {
            Text = "Let us know why, so this isn't flagged as unexplained:",
            AutoSize = false,
            Size = new Size(320, 20),
            Location = new Point(20, 56)
        };

        Controls.Add(heading);
        Controls.Add(subheading);

        var y = 84;
        foreach (var reason in QuickReasons)
        {
            var button = new Button
            {
                Text = reason,
                Size = new Size(320, 32),
                Location = new Point(20, y)
            };
            button.Click += (_, _) => { SelectedReason = reason; Close(); };
            Controls.Add(button);
            y += 38;
        }

        var otherBox = new TextBox
        {
            PlaceholderText = "Other reason…",
            Size = new Size(230, 24),
            Location = new Point(20, y + 4)
        };
        var submitOther = new Button
        {
            Text = "Submit",
            Size = new Size(90, 26),
            Location = new Point(250, y + 2)
        };
        submitOther.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(otherBox.Text))
            {
                SelectedReason = otherBox.Text.Trim();
                Close();
            }
        };
        Controls.Add(otherBox);
        Controls.Add(submitOther);

        var dismiss = new LinkLabel
        {
            Text = "Dismiss without answering",
            Size = new Size(320, 20),
            Location = new Point(20, y + 36)
        };
        dismiss.Click += (_, _) => { SelectedReason = null; Close(); };
        Controls.Add(dismiss);
    }
}
