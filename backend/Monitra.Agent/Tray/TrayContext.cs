using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Monitra.Agent.Tray;

/// <summary>
/// Owns the tray icon, its context menu, and a hidden form used only to give background
/// services a UI-thread handle to marshal onto (NotifyIcon itself isn't a Control and has no
/// Invoke method). This is the one piece of the agent that runs on the WinForms message loop
/// started in Program.cs; everything else (heartbeat, health, policy refresh) runs on the
/// generic host's thread pool and calls back into this class to show something to the employee.
/// </summary>
[SupportedOSPlatform("windows")]
public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Form _uiThreadHandle;
    private readonly ToolStripMenuItem _breakMenuItem;

    public event Func<Task>? BreakRequested;

    public TrayContext()
    {
        _uiThreadHandle = new Form
        {
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized,
            Visible = false
        };
        // Force handle creation now so Invoke/BeginInvoke work immediately, before the
        // background services' first callback.
        _ = _uiThreadHandle.Handle;

        _breakMenuItem = new ToolStripMenuItem("Request a break", null, OnBreakMenuClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_breakMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Status: Starting…") { Enabled = false, Name = "StatusItem" });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Monitra",
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    private async void OnBreakMenuClicked(object? sender, EventArgs e)
    {
        _breakMenuItem.Enabled = false;
        try
        {
            if (BreakRequested != null)
            {
                await BreakRequested.Invoke();
            }
        }
        finally
        {
            _breakMenuItem.Enabled = true;
        }
    }

    public void UpdateStatus(string text)
    {
        RunOnUiThread(() =>
        {
            _notifyIcon.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon.Text is capped at 63 chars
            if (_notifyIcon.ContextMenuStrip?.Items["StatusItem"] is ToolStripMenuItem item)
            {
                item.Text = $"Status: {text}";
            }
        });
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        RunOnUiThread(() => _notifyIcon.ShowBalloonTip(8000, title, text, icon));
    }

    /// <summary>
    /// Shows the inactivity-justification prompt and returns the employee's answer, or null if
    /// they dismissed it without answering (recorded server-side as PendingResponse).
    /// </summary>
    public Task<string?> ShowInactivityPromptAsync(int idleMinutes)
    {
        var tcs = new TaskCompletionSource<string?>();
        RunOnUiThread(() =>
        {
            using var form = new InactivityPromptForm(idleMinutes);
            form.ShowDialog();
            tcs.SetResult(form.SelectedReason);
        });
        return tcs.Task;
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiThreadHandle.IsDisposed) return;
        if (_uiThreadHandle.InvokeRequired)
        {
            _uiThreadHandle.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _uiThreadHandle.Dispose();
        base.ExitThreadCore();
    }
}
