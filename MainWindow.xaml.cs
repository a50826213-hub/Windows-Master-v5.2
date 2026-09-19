using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using WindowsMaster.Models;
using WindowsMaster.Services;
using WindowsMaster.Views;
using WinRT.Interop;

namespace WindowsMaster;

public partial class MainWindow : Window
{
    private const string OfficialUrl = "https://anas-mohamed-tech.surge.sh";

    private bool _dark = true;

    private readonly Dictionary<string, FrameworkElement> _pages = new();

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _searchDebounceTimer;

    private AppWindow? _appWindow;

    private string _pendingSearch = string.Empty;

    // Keeps separate System Information windows alive while they are open.
    private readonly List<SystemInfoWindow> _systemInfoWindows = new();

    // Keeps the modal execution-result window alive while it is open.
    private readonly List<ExecutionResultWindow> _executionResultWindows = new();

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public MainWindow()
    {
        InitializeComponent();

        _searchDebounceTimer =
            Microsoft.UI.Dispatching.DispatcherQueue
                .GetForCurrentThread()
                .CreateTimer();

        _searchDebounceTimer.Interval =
            TimeSpan.FromMilliseconds(250);

        _searchDebounceTimer.IsRepeating = false;

        _searchDebounceTimer.Tick += (_, _) =>
            ApplySearch(_pendingSearch);

        ConfigureWindow();

        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        NavigateTo("Home");
    }

    private void ConfigureWindow()
    {
        try
        {
            // Keep the normal native Windows title bar.
            // This prevents the system caption buttons from overlapping
            // the application content.

            ExtendsContentIntoTitleBar = false;

            _appWindow = AppWindow;

            _appWindow.Resize(
                new Windows.Graphics.SizeInt32(1240, 800)
            );

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 1040;
                presenter.PreferredMinimumHeight = 680;

                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }

            // Application icon
            var iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "logo.ico"
            );

            if (File.Exists(iconPath))
            {
                try
                {
                    _appWindow.SetIcon(iconPath);
                }
                catch
                {
                    // Ignore icon errors so the program still starts.
                }

                try
                {
                    _appWindow.SetTaskbarIcon(iconPath);
                }
                catch
                {
                    // Ignore Taskbar icon errors.
                }

                try
                {
                    _appWindow.SetTitleBarIcon(iconPath);
                }
                catch
                {
                    // Ignore title bar icon errors.
                }
            }
        }
        catch
        {
            // Older Windows builds may not expose every windowing feature.
            // The application should still start normally.
        }
    }

    private void GlobalSearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        _pendingSearch = GlobalSearchBox.Text.Trim();

        ClearSearchButton.Visibility =
            string.IsNullOrWhiteSpace(_pendingSearch)
                ? Visibility.Collapsed
                : Visibility.Visible;

        _searchDebounceTimer.Stop();

        if (string.IsNullOrWhiteSpace(_pendingSearch))
        {
            NavigateTo(GetSelectedTag());
            return;
        }

        _searchDebounceTimer.Start();
    }

    private void ClearSearch_Click(
        object sender,
        RoutedEventArgs e)
    {
        GlobalSearchBox.Text = string.Empty;

        GlobalSearchBox.Focus(
            FocusState.Programmatic
        );
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            NavigateTo(GetSelectedTag());
            return;
        }

        var results = WindowsCatalog.All
            .Where(x =>
                x.Searchable &&
                (
                    x.Name.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    x.Description.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    x.Category.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            .ToList();

        ContentFrame.Content =
            new CatalogPage(
                "Search results",
                results.Count == 0
                    ? $"No matching tools for \"{query}\"."
                    : $"Results for \"{query}\"",
                results,
                RunToolAsync
            );
    }

    private string GetSelectedTag()
    {
        if (NavView.SelectedItem is NavigationViewItem item &&
            item.Tag is string tag)
        {
            return tag;
        }

        return "Home";
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item &&
            item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        // Use cached pages to prevent unnecessary recreation,
        // which keeps navigation smoother.

        if (_pages.TryGetValue(tag, out var cached))
        {
            ContentFrame.Content = cached;
            return;
        }

        FrameworkElement page = tag switch
        {
            "Home" =>
                new HomePage(
                    RunToolAsync
                ),

            "Settings" =>
                new CatalogPage(
                    "Windows Settings",
                    "Direct Windows Settings pages, organized like the native Settings app.",
                    WindowsCatalog.Settings,
                    RunToolAsync
                ),

            "ControlPanel" =>
                new CatalogPage(
                    "Control Panel",
                    "Classic Windows Control Panel entries and administrative shortcuts.",
                    WindowsCatalog.ControlPanel,
                    RunToolAsync
                ),

            "Repair" =>
                new CatalogPage(
                    "Repair",
                    "Windows integrity, component store, disk and network recovery tools.",
                    WindowsCatalog.Repair,
                    RunToolAsync
                ),

            "Maintenance" =>
                new CatalogPage(
                    "Maintenance",
                    "Routine cleanup and maintenance actions for Windows.",
                    WindowsCatalog.Maintenance,
                    RunToolAsync
                ),

            "Performance" =>
                new CatalogPage(
                    "Performance",
                    "Power and performance controls with clear impact warnings.",
                    WindowsCatalog.Performance,
                    RunToolAsync
                ),

            "SystemTools" =>
                new CatalogPage(
                    "System Tools",
                    "Native Windows management consoles and diagnostic tools.",
                    WindowsCatalog.SystemTools,
                    RunToolAsync
                ),

            "Commands" =>
                new CatalogPage(
                    "Command Center",
                    "Curated Windows commands with descriptions, permission levels and safety classification.",
                    WindowsCatalog.Commands,
                    RunToolAsync
                ),

            _ =>
                new HomePage(
                    RunToolAsync
                )
        };

        _pages[tag] = page;

        ContentFrame.Content = page;
    }

    private async void About_Click(
        object sender,
        RoutedEventArgs e)
    {
        var content = new StackPanel
        {
            Spacing = 12
        };

        // Logo
        content.Children.Add(
            new Image
            {
                Source =
                    new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                        new Uri(
                            "ms-appx:///Assets/logo.png"
                        )
                    ),

                Width = 72,
                Height = 72,

                HorizontalAlignment =
                    HorizontalAlignment.Center
            }
        );

        // Title
        content.Children.Add(
            new TextBlock
            {
                Text = "Windows Master v5.2",

                FontSize = 22,

                FontWeight =
                    Microsoft.UI.Text.FontWeights.SemiBold,

                HorizontalAlignment =
                    HorizontalAlignment.Center
            }
        );

        // Developer / description
        content.Children.Add(
            new TextBlock
            {
                Text =
                    "Anas Mohamed | Tech\n" +
                    "Native Windows control center for Settings, " +
                    "Control Panel, repair, maintenance and system tools.",

                TextWrapping =
                    TextWrapping.Wrap,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                TextAlignment =
                    TextAlignment.Center,

                Opacity = 0.78
            }
        );

        // Website text
        content.Children.Add(
            new TextBlock
            {
                Text = OfficialUrl,

                FontSize = 12,

                Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DeepSkyBlue
                    ),

                HorizontalAlignment =
                    HorizontalAlignment.Center
            }
        );

        // About dialog
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,

            Title = "About Windows Master",

            Content = content,

            PrimaryButtonText =
                "Open official website",

            CloseButtonText =
                "Close",

            DefaultButton =
                ContentDialogButton.Close
        };

        var result =
            await dialog.ShowAsync();

        if (result ==
            ContentDialogResult.Primary)
        {
            await Launcher.LaunchUriAsync(
                new Uri(OfficialUrl)
            );
        }
    }

    private void Theme_Click(
        object sender,
        RoutedEventArgs e)
    {
        _dark = !_dark;

        RootGrid.RequestedTheme =
            _dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
    }

    private static bool IsSystemInfoTool(ToolItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return false;
        }

        string name = item.Name.Trim();

        return
            name.Equals("System Info", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System Information", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("System Info", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("System Information", StringComparison.OrdinalIgnoreCase);
    }

    private async Task OpenSystemInfoWindowAsync()
    {
        var window = new SystemInfoWindow(_dark);

        _systemInfoWindows.Add(window);

        window.Closed += (_, _) =>
        {
            _systemInfoWindows.Remove(window);
        };

        window.Activate();

        await window.LoadSystemInfoAsync();
    }

    private async Task RunToolAsync(
        ToolItem item)
    {
        // Information-only System Info actions are displayed in a separate
        // Windows Master window. The console process stays completely hidden.
        if (IsSystemInfoTool(item))
        {
            await OpenSystemInfoWindowAsync();
            return;
        }

        // Ask for confirmation when the action:
        // - changes system state
        // - is administrative
        // - has warning / critical risk

        if (item.Risk != RiskLevel.Safe ||
            item.RequiresAdmin)
        {
            string warning =
                item.Risk switch
                {
                    RiskLevel.Critical =>
                        "High-impact action. It can change Windows state and, if misused, may cause data loss or interruption.",

                    RiskLevel.Warning =>
                        "This action changes Windows configuration or system state.",

                    RiskLevel.Admin =>
                        "This action may request Administrator permission.",

                    _ =>
                        "This action may request Administrator permission."
                };

            var content =
                new StackPanel
                {
                    Spacing = 8
                };

            content.Children.Add(
                new TextBlock
                {
                    Text = item.Description,

                    TextWrapping =
                        TextWrapping.Wrap
                }
            );

            content.Children.Add(
                new TextBlock
                {
                    Text = warning,

                    Foreground =
                        (Microsoft.UI.Xaml.Media.Brush)
                        Application.Current.Resources[
                            "WMWarningBrush"
                        ],

                    TextWrapping =
                        TextWrapping.Wrap,

                    FontWeight =
                        Microsoft.UI.Text.FontWeights.SemiBold
                }
            );

            if (!string.IsNullOrWhiteSpace(
                item.Notes))
            {
                content.Children.Add(
                    new TextBlock
                    {
                        Text = item.Notes,

                        Opacity = 0.75,

                        TextWrapping =
                            TextWrapping.Wrap
                    }
                );
            }

            var dialog =
                new ContentDialog
                {
                    XamlRoot =
                        RootGrid.XamlRoot,

                    Title =
                        item.Name,

                    Content =
                        content,

                    PrimaryButtonText =
                        "Continue",

                    CloseButtonText =
                        "Cancel",

                    DefaultButton =
                        ContentDialogButton.Close
                };

            var result =
                await dialog.ShowAsync();

            if (result !=
                ContentDialogResult.Primary)
            {
                return;
            }
        }

        // Run the action and wait for an actual exit code when the action is
        // a command-line operation. GUI/Settings items are reported as
        // successfully launched.
        var launchResult =
            await WindowsLauncher.ExecuteWithResultAsync(item);

        // Keep successful operations silent. Only show the separate
        // result window when Windows reports a failure.
        if (!launchResult.Succeeded)
        {
            await ShowExecutionResultWindowAsync(
                item,
                launchResult
            );
        }
    }

    private async Task ShowExecutionResultWindowAsync(
        ToolItem item,
        WindowsLauncher.LaunchResult result)
    {
        var window = new ExecutionResultWindow(
            _dark,
            item.Name,
            result);

        _executionResultWindows.Add(window);

        IntPtr mainHandle =
            WindowNative.GetWindowHandle(this);

        // Make the result window genuinely modal from the user's point of view:
        // the main Windows Master window cannot be clicked until Close is used.
        EnableWindow(mainHandle, false);

        window.Closed += (_, _) =>
        {
            _executionResultWindows.Remove(window);

            EnableWindow(mainHandle, true);
            SetForegroundWindow(mainHandle);
        };

        window.Activate();

        // Keep this method asynchronous so RunToolAsync remains awaitable while
        // the result window is open.
        await Task.CompletedTask;
    }

}

// ============================================================================
// Separate System Information window
// ============================================================================
//
// This window is intentionally implemented in C# so the user does not need
// to create or replace any additional XAML file. It does not alter the main
// Windows Master UI.
//
internal sealed class SystemInfoWindow : Window
{
    private readonly TextBox _outputBox;
    private readonly Button _copyButton;
    private readonly Grid _root;

    public SystemInfoWindow(bool darkMode)
    {
        Title = "Windows Master — System Information";

        _root = new Grid
        {
            Padding = new Thickness(18),
            RequestedTheme = darkMode
                ? ElementTheme.Dark
                : ElementTheme.Light,
            Background = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMWindowBrush"]
        };

        _root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        _root.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(2, 0, 2, 12)
        };

        header.Children.Add(
            new TextBlock
            {
                Text = "System Information",
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["WMTextBrush"]
            });

        header.Children.Add(
            new TextBlock
            {
                Text = "Windows Master • Read-only Windows information",
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["WMSecondaryTextBrush"]
            });

        Grid.SetRow(header, 0);
        _root.Children.Add(header);

        var outputBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMStrokeBrush"],
            Background = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMCardBrush"],
            Padding = new Thickness(10)
        };

        _outputBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 13,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMTextBrush"]
        };

        // WinUI 3 exposes TextBox scrolling through ScrollViewer attached properties.
        ScrollViewer.SetVerticalScrollBarVisibility(
            _outputBox,
            ScrollBarVisibility.Auto);

        ScrollViewer.SetHorizontalScrollBarVisibility(
            _outputBox,
            ScrollBarVisibility.Auto);

        outputBorder.Child = _outputBox;

        Grid.SetRow(outputBorder, 1);
        _root.Children.Add(outputBorder);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        _copyButton = new Button
        {
            Content = "Copy",
            MinWidth = 90
        };
        _copyButton.Click += CopyButton_Click;

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 90
        };
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(_copyButton);
        buttons.Children.Add(closeButton);

        Grid.SetRow(buttons, 2);
        _root.Children.Add(buttons);

        Content = _root;

        try
        {
            AppWindow.Resize(
                new Windows.Graphics.SizeInt32(920, 640));

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 720;
                presenter.PreferredMinimumHeight = 480;
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }

            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "logo.ico");

            if (File.Exists(iconPath))
            {
                try
                {
                    AppWindow.SetIcon(iconPath);
                }
                catch
                {
                    // Optional icon only.
                }
            }
        }
        catch
        {
            // Keep the information window functional on older Windows builds.
        }
    }

    public async Task LoadSystemInfoAsync()
    {
        try
        {
            _outputBox.Text = "Loading system information…";

            string systemInfoExe = Path.Combine(
                Environment.SystemDirectory,
                "systeminfo.exe");

            string result = await RunHiddenCommandAsync(
                systemInfoExe);

            _outputBox.Text = string.IsNullOrWhiteSpace(result)
                ? "No information was returned by Windows."
                : result;

            _outputBox.Focus(FocusState.Programmatic);
        }
        catch (Exception ex)
        {
            _outputBox.Text =
                "Windows could not return system information."
                + Environment.NewLine
                + Environment.NewLine
                + ex.Message;
        }
    }

    private static async Task<string> RunHiddenCommandAsync(
        string executable,
        string arguments = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        if (string.IsNullOrWhiteSpace(output))
        {
            return string.IsNullOrWhiteSpace(error)
                ? "No information was returned."
                : error.Trim();
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return output.TrimEnd()
                   + Environment.NewLine
                   + Environment.NewLine
                   + "[Error]"
                   + Environment.NewLine
                   + error.Trim();
        }

        return output.TrimEnd();
    }

    private async void CopyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var package =
                new Windows.ApplicationModel.DataTransfer.DataPackage();

            package.SetText(_outputBox.Text);

            Windows.ApplicationModel.DataTransfer.Clipboard
                .SetContent(package);

            _copyButton.Content = "Copied";

            await Task.Delay(900);

            _copyButton.Content = "Copy";
        }
        catch
        {
            _copyButton.Content = "Copy";
        }
    }
}

// ============================================================================
// Modal external execution result window
// ============================================================================
//
// This is a normal top-level WinUI window. The main Windows Master window is
// disabled while it is open, so the user must close this window before using
// the main interface again.
//
internal sealed class ExecutionResultWindow : Window
{
    private readonly Button _closeButton;

    public ExecutionResultWindow(
        bool darkMode,
        string itemName,
        WindowsLauncher.LaunchResult result)
    {
        Title = "Windows Master — Operation Result";

        bool success = result.Succeeded;

        var root = new Grid
        {
            Padding = new Thickness(22),
            RequestedTheme = darkMode
                ? ElementTheme.Dark
                : ElementTheme.Light,
            Background =
                (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMWindowBrush"]
        };

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        var statusIcon = new TextBlock
        {
            Text = success ? "✓" : "✕",
            FontSize = 34,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground =
                success
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LimeGreen)
                : (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["WMWarningBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        Grid.SetRow(statusIcon, 0);
        root.Children.Add(statusIcon);

        var title = new TextBlock
        {
            Text = success
                ? "Operation completed successfully"
                : "Operation failed",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground =
                (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMTextBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        Grid.SetRow(title, 1);
        root.Children.Add(title);

        var detail = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 12, 0, 18)
        };

        detail.Children.Add(new TextBlock
        {
            Text = itemName,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground =
                (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMTextBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        detail.Children.Add(new TextBlock
        {
            Text = result.Message,
            FontSize = 13,
            Foreground =
                (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["WMSecondaryTextBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        if (result.ExitCode.HasValue)
        {
            detail.Children.Add(new TextBlock
            {
                Text = $"Exit code: {result.ExitCode.Value}",
                FontSize = 11,
                Foreground =
                    (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["WMSecondaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Opacity = 0.75
            });
        }

        Grid.SetRow(detail, 2);
        root.Children.Add(detail);

        _closeButton = new Button
        {
            Content = "Close",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _closeButton.Click += (_, _) => Close();

        Grid.SetRow(_closeButton, 3);
        root.Children.Add(_closeButton);

        Content = root;

        try
        {
            AppWindow.Resize(
                new Windows.Graphics.SizeInt32(520, 310));

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 420;
                presenter.PreferredMinimumHeight = 260;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "logo.ico");

            if (File.Exists(iconPath))
            {
                try
                {
                    AppWindow.SetIcon(iconPath);
                }
                catch
                {
                    // Optional icon only.
                }
            }
        }
        catch
        {
            // The result window remains functional even if window sizing fails.
        }
    }
}

