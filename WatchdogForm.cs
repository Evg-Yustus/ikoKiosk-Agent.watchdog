using System.Drawing;
using System.Windows.Forms;

internal sealed class WatchdogForm : Form
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _checkNowItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly WatchdogEngine _engine = new();
    private bool _autostartUpdateInProgress;
    private bool _checkInProgress;

    public WatchdogForm()
    {
        Text = "iikoKiosk Agent watchdog";
        ShowInTaskbar = false;
        ShowIcon = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        Opacity = 0;

        _statusItem = new ToolStripMenuItem("Статус: запуск...") { Enabled = false };
        _autostartItem = new ToolStripMenuItem("Добавить в автозапуск")
        {
            CheckOnClick = true
        };
        _autostartItem.CheckedChanged += AutostartItem_CheckedChanged;

        _checkNowItem = new ToolStripMenuItem("Проверить сейчас");
        _checkNowItem.Click += async (_, _) => await CheckAsync();

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => Close();

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_autostartItem);
        _contextMenu.Items.Add(_checkNowItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "iiko Agent watchdog",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer
        {
            Interval = (int)WatchdogSettings.PollInterval.TotalMilliseconds
        };
        _timer.Tick += async (_, _) => await CheckAsync();

        Shown += WatchdogForm_Shown;
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        };
    }

    private async void WatchdogForm_Shown(object? sender, EventArgs e)
    {
        Hide();

        _autostartUpdateInProgress = true;
        _autostartItem.Checked = await Task.Run(StartupTaskManager.IsEnabled);
        _autostartUpdateInProgress = false;

        _timer.Start();
        await CheckAsync();
    }

    private async void AutostartItem_CheckedChanged(object? sender, EventArgs e)
    {
        if (_autostartUpdateInProgress)
        {
            return;
        }

        var requestedState = _autostartItem.Checked;
        _autostartItem.Enabled = false;

        try
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь к EXE.");

            if (requestedState)
            {
                await Task.Run(() => StartupTaskManager.Enable(executablePath));
                SetStatus("Автозапуск включён.");
            }
            else
            {
                await Task.Run(StartupTaskManager.Disable);
                SetStatus("Автозапуск выключен.");
            }
        }
        catch (Exception exception)
        {
            _autostartUpdateInProgress = true;
            _autostartItem.Checked = !requestedState;
            _autostartUpdateInProgress = false;
            SetStatus("Ошибка настройки автозапуска.");
            MessageBox.Show(
                exception.Message,
                "iikoKiosk Agent watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _autostartItem.Enabled = true;
        }
    }

    private async Task CheckAsync()
    {
        if (_checkInProgress || IsDisposed)
        {
            return;
        }

        _checkInProgress = true;
        _checkNowItem.Enabled = false;

        try
        {
            var result = await Task.Run(_engine.CheckOnce);
            SetStatus(result.Message);
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка проверки: {exception.Message}");
        }
        finally
        {
            _checkNowItem.Enabled = true;
            _checkInProgress = false;
        }
    }

    private void SetStatus(string message)
    {
        _statusItem.Text = $"Статус: {message}";
        _notifyIcon.Text = Truncate($"iiko Agent: {message}", 63);
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }
}
