using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AdonisUI.Controls;
using FModel.Extensions;
using FModel.Services;
using FModel.Settings;
using FModel.ViewModels;
using FModel.Views;
using FModel.Views.Resources.Controls;
using ICSharpCode.AvalonEdit.Editing;

namespace FModel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public static MainWindow YesWeCats;
    private ThreadWorkerViewModel _threadWorkerView => ApplicationService.ThreadWorkerView;
    private ApplicationViewModel _applicationView => ApplicationService.ApplicationView;
    private DiscordHandler _discordHandler => DiscordService.DiscordHandler;

    public MainWindow()
    {
        CommandBindings.Add(new CommandBinding(new RoutedCommand("ReloadMappings", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.F12) }), OnMappingsReload));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (_, _) => OnOpenAvalonFinder()));

        DataContext = _applicationView;
        InitializeComponent();

        FLogger.Logger = LogRtbName;
        YesWeCats = this;
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        _discordHandler.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var newOrUpdated = FModel.Settings.UserSettings.Default.ShowChangelog;
#if !DEBUG
        ApplicationService.ApiEndpointView.FModelApi.CheckForUpdates(true);
#endif

        switch (FModel.Settings.UserSettings.Default.AesReload)
        {
            case EAesReload.Always:
                await _applicationView.CUE4Parse.RefreshAes();
                break;
            case EAesReload.OncePerDay when FModel.Settings.UserSettings.Default.CurrentDir.LastAesReload != DateTime.Today:
                FModel.Settings.UserSettings.Default.CurrentDir.LastAesReload = DateTime.Today;
                await _applicationView.CUE4Parse.RefreshAes();
                break;
        }

        await ApplicationViewModel.InitOodle();
        await ApplicationViewModel.InitZlib();
        await _applicationView.CUE4Parse.Initialize();
        await _applicationView.AesManager.InitAes();
        await _applicationView.UpdateProvider(true);
#if !DEBUG
        await _applicationView.CUE4Parse.InitInformation();
#endif
        await Task.WhenAll(
            _applicationView.CUE4Parse.VerifyConsoleVariables(),
            _applicationView.CUE4Parse.VerifyOnDemandArchives(),
            _applicationView.CUE4Parse.InitMappings(),
            ApplicationViewModel.InitVgmStream(),
            ApplicationViewModel.InitImGuiSettings(newOrUpdated),
            Task.Run(() =>
            {
                if (FModel.Settings.UserSettings.Default.DiscordRpc == EDiscordRpc.Always)
                    _discordHandler.Initialize(_applicationView.GameDisplayName);
            })
        ).ConfigureAwait(false);

        // Set up accessibility enhancements after initialization
        // Make sure we're on the UI thread
        Application.Current.Dispatcher.Invoke(() => {
            EnhanceAccessibility();

            // Use a delay to ensure UI is fully loaded
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                BruteForceFixLoadingModeAccessibility();
            }));
        });

#if DEBUG
        // await _threadWorkerView.Begin(cancellationToken =>
        //     _applicationView.CUE4Parse.Extract(cancellationToken,
        //         "FortniteGame/Content/Athena/Apollo/Maps/UI/Apollo_Terrain_Minimap.uasset"));
        // await _threadWorkerView.Begin(cancellationToken =>
        //     _applicationView.CUE4Parse.Extract(cancellationToken,
        //         "FortniteGame/Content/Environments/Helios/Props/GlacierHotel/GlacierHotel_Globe_A/Meshes/SM_GlacierHotel_Globe_A.uasset"));
#endif
    }

    private void BruteForceFixLoadingModeAccessibility()
    {
        // Find the specific loading mode text from the template
        var loadingModeLabel = LogicalTreeHelper.FindLogicalNode(this, "LeftTabControl") as TabControl;
        if (loadingModeLabel != null)
        {
            // First find the text element showing "Loading Mode"
            var loadingModeText = FindVisualChildren<TextBlock>(loadingModeLabel)
                .FirstOrDefault(t => t.Text == "Loading Mode");

            if (loadingModeText != null)
            {
                // Navigate up and then to siblings to find the ComboBox
                var parent = VisualTreeHelper.GetParent(loadingModeText);
                if (parent != null)
                {
                    var parentPanel = VisualTreeHelper.GetParent(parent);
                    if (parentPanel != null)
                    {
                        // Try to find the combo box in this panel
                        var comboBox = FindVisualChildren<ComboBox>(parentPanel).FirstOrDefault();
                        if (comboBox != null)
                        {
                            // Apply aggressive accessibility fixes
                            comboBox.IsTabStop = true;
                            comboBox.Focusable = true;
                            comboBox.IsEnabled = true;
                            comboBox.TabIndex = 1;

                            // UI Automation properties
                            AutomationProperties.SetIsRequiredForForm(comboBox, true);
                            AutomationProperties.SetName(comboBox, "Loading Mode Selector");
                            AutomationProperties.SetHelpText(comboBox, "Tab to focus, arrow keys to select mode");
                            AutomationProperties.SetAutomationId(comboBox, "LoadingModeComboBox");

                            // Make sure screen readers can interact with it
                            KeyboardNavigation.SetIsTabStop(comboBox, true);
                            KeyboardNavigation.SetTabIndex(comboBox, 1);
                            KeyboardNavigation.SetDirectionalNavigation(comboBox, KeyboardNavigationMode.Contained);

                            // Add special event handler to ensure it receives focus
                            comboBox.GotKeyboardFocus += (s, e) => {
                                // Announce to screen readers
                                AutomationProperties.SetItemStatus(comboBox, "Loading Mode ComboBox focused");
                            };

                            // Special handling to open dropdown with Space or Enter
                            comboBox.KeyDown += (s, e) => {
                                if ((e.Key == Key.Space || e.Key == Key.Enter) && !comboBox.IsDropDownOpen)
                                {
                                    comboBox.IsDropDownOpen = true;
                                    e.Handled = true;
                                }
                            };

                            // Fix accessibility for all items in the dropdown
                            comboBox.DropDownOpened += (s, e) => {
                                var items = comboBox.Items;
                                foreach (var item in items)
                                {
                                    var container = comboBox.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem;
                                    if (container != null)
                                    {
                                        AutomationProperties.SetName(container, item.ToString());
                                        container.IsTabStop = true;
                                        KeyboardNavigation.SetIsTabStop(container, true);
                                    }
                                }
                            };
                        }
                    }
                }
            }

            // Also find and fix the Load button
            var loadButton = FindVisualChildren<Button>(loadingModeLabel)
                .FirstOrDefault(b => b.Content != null && b.Content.ToString() == "Load");

            if (loadButton != null)
            {
                loadButton.IsTabStop = true;
                loadButton.Focusable = true;
                loadButton.TabIndex = 2;
                AutomationProperties.SetName(loadButton, "Load Archives Button");
                AutomationProperties.SetHelpText(loadButton, "Press to load selected archives");
                AutomationProperties.SetAutomationId(loadButton, "LoadButton");
                KeyboardNavigation.SetIsTabStop(loadButton, true);
                KeyboardNavigation.SetTabIndex(loadButton, 2);

                // Make the whole parent grid properly support tab navigation
                var parentGrid = VisualTreeHelper.GetParent(loadButton) as UIElement;
                if (parentGrid != null)
                {
                    KeyboardNavigation.SetTabNavigation(parentGrid, KeyboardNavigationMode.Local);
                    KeyboardNavigation.SetDirectionalNavigation(parentGrid, KeyboardNavigationMode.Local);
                }
            }
        }
    }

    private void EnhanceAccessibility()
    {
        // Ensure we're on the UI thread
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(EnhanceAccessibility);
            return;
        }

        // Find the Loading Mode ComboBox
        var loadingModeComboBox = FindVisualChild<ComboBox>(
            FindVisualChild<TextBlock>(RootGrid, tb => tb?.Text == "Loading Mode")?.Parent as DependencyObject);

        // Find the Load button
        var loadButton = FindVisualChild<Button>(RootGrid, b =>
            b.Content != null && b.Content.ToString() == "Load" &&
            b.Command == _applicationView.LoadingModes.LoadCommand);

        if (loadingModeComboBox != null)
        {
            // Make it accessible and tabbable
            AccessibilityHelper.MakeElementAccessible(loadingModeComboBox,
                "Loading Mode Selector",
                "Choose how to load game archives: multiple, all, or filtered modes");
            loadingModeComboBox.IsTabStop = true;
            loadingModeComboBox.TabIndex = 1;
            KeyboardNavigation.SetTabNavigation(loadingModeComboBox, KeyboardNavigationMode.Local);
            KeyboardNavigation.SetIsTabStop(loadingModeComboBox, true);
        }

        if (loadButton != null)
        {
            // Make it accessible and tabbable
            AccessibilityHelper.MakeElementAccessible(loadButton,
                "Load Archives Button",
                "Load selected archives using the chosen loading mode");
            loadButton.IsTabStop = true;
            loadButton.TabIndex = 2;
            KeyboardNavigation.SetTabNavigation(loadButton, KeyboardNavigationMode.Local);
            KeyboardNavigation.SetIsTabStop(loadButton, true);

            // Setting parent container to enable proper tab navigation flow
            var parent = VisualTreeHelper.GetParent(loadButton) as DependencyObject;
            if (parent != null)
            {
                KeyboardNavigation.SetTabNavigation(parent, KeyboardNavigationMode.Local);
            }
        }

        // Fix keyboard navigation in the overall layout
        KeyboardNavigation.SetTabNavigation(RootGrid, KeyboardNavigationMode.Continue);
        KeyboardNavigation.SetDirectionalNavigation(RootGrid, KeyboardNavigationMode.Continue);

        // Set tab order explicitly for important containers
        if (LeftTabControl != null)
        {
            LeftTabControl.TabIndex = 0;
            KeyboardNavigation.SetTabNavigation(LeftTabControl, KeyboardNavigationMode.Continue);
        }

        if (TabControlName != null)
        {
            TabControlName.TabIndex = 10;
            KeyboardNavigation.SetTabNavigation(TabControlName, KeyboardNavigationMode.Cycle);
        }

        // Set accessibility properties for main controls
        AccessibilityHelper.MakeElementAccessible(DirectoryFilesListBox,
            "Archive Files List",
            "List of game archives available for loading");
        DirectoryFilesListBox.TabIndex = 3;
        DirectoryFilesListBox.IsTabStop = true;

        AccessibilityHelper.MakeElementAccessible(AssetsFolderName,
            "Folder Structure Tree",
            "Navigate through the folder structure of the loaded archives");
        AssetsFolderName.TabIndex = 4;
        AssetsFolderName.IsTabStop = true;

        AccessibilityHelper.MakeElementAccessible(AssetsListName,
            "Assets List",
            "List of assets in the selected folder");
        AssetsListName.TabIndex = 5;
        AssetsListName.IsTabStop = true;

        // Enhance TabControl to allow exiting with F6
        AccessibilityHelper.EnhanceTabControl(TabControlName,
            loadingModeComboBox ?? (UIElement) DirectoryFilesListBox);

        AccessibilityHelper.MakeElementAccessible(LogRtbName,
            "Log Output",
            "Displays application log messages and output");
        LogRtbName.TabIndex = 20;
        LogRtbName.IsTabStop = true;

        // We need to safely set up the events - don't directly hook them
        // but use the dispatcher to ensure they're called on the UI thread
        Application.Current.Dispatcher.InvokeAsync(() => {
            // Set up item container generation event handlers for dynamic items
            DirectoryFilesListBox.ItemContainerGenerator.StatusChanged += DirectoryFilesListBox_ItemContainerGenerator_StatusChanged;
            AssetsListName.ItemContainerGenerator.StatusChanged += AssetsListName_ItemContainerGenerator_StatusChanged;
            AssetsFolderName.AddHandler(TreeViewItem.LoadedEvent, new RoutedEventHandler(TreeViewItem_Loaded));

            // Ensure TabControlName can be exited
            AddTabControlAccessibilitySupport();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AddTabControlAccessibilitySupport()
    {
        // Add a custom exit handler for the TabControl
        TabControlName.KeyDown += (s, e) => {
            if (e.Key == Key.F6)
            {
                e.Handled = true;
                // Try to focus on Archives tab
                if (LeftTabControl.Items.Count > 0 && LeftTabControl.Items[0] is System.Windows.Controls.TabItem archivesTab)
                {
                    LeftTabControl.SelectedItem = archivesTab;
                    DirectoryFilesListBox.Focus();
                }
            }
        };

        // Add accessibility instruction to tab headers
        foreach (System.Windows.Controls.TabItem tab in TabControlName.Items)
        {
            if (tab != null)
            {
                string existingHelp = AutomationProperties.GetHelpText(tab) ?? "";
                if (!existingHelp.Contains("F6"))
                {
                    AutomationProperties.SetHelpText(tab,
                        existingHelp + (string.IsNullOrEmpty(existingHelp) ? "" : " ") +
                        "Press F6 to exit content area.");
                }
            }
        }

        // Fix: Use ItemContainerGenerator to monitor new tabs instead of the non-existent Items.Changed event
        TabControlName.ItemContainerGenerator.StatusChanged += (sender, e) => {
            if (TabControlName.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                // Process newly added tabs
                foreach (var item in TabControlName.Items)
                {
                    var container = TabControlName.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.TabItem;
                    if (container != null)
                    {
                        string existingHelp = AutomationProperties.GetHelpText(container) ?? "";
                        if (!existingHelp.Contains("F6"))
                        {
                            AutomationProperties.SetHelpText(container,
                                existingHelp + (string.IsNullOrEmpty(existingHelp) ? "" : " ") +
                                "Press F6 to exit content area.");
                        }
                    }
                }
            }
        };

        // Make the content area properly navigate via tabs
        var contentHost = FindVisualChild<ContentPresenter>(TabControlName, cp =>
            cp.Name == "PART_SelectedContentHost" || cp.Name.Contains("ContentHost"));

        if (contentHost != null)
        {
            KeyboardNavigation.SetTabNavigation(contentHost, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(contentHost, KeyboardNavigationMode.Cycle);
            AutomationProperties.SetHelpText(contentHost, "Press F6 to exit content area");
        }
    }

    // Helper methods to find elements in the visual tree
    private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
    {
        if (parent == null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && (predicate == null || predicate(typedChild)))
                return typedChild;

            var result = FindVisualChild<T>(child, predicate);
            if (result != null)
                return result;
        }

        return null;
    }

    private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
            yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                yield return typedChild;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    private void DirectoryFilesListBox_ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
    {
        if (DirectoryFilesListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            // Enhance each item
            foreach (var item in DirectoryFilesListBox.Items)
            {
                var container = DirectoryFilesListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    dynamic assetItem = item;
                    AccessibilityHelper.EnhanceListBoxItem(container, assetItem.Name,
                        $"Size: {assetItem.Length}, Archive: {(assetItem.IsEnabled ? "Enabled" : "Disabled")}");
                }
            }
        }
    }

    private void AssetsListName_ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
    {
        if (AssetsListName.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            // Enhance each item
            foreach (var item in AssetsListName.Items)
            {
                var container = AssetsListName.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null && item is AssetItem assetItem)
                {
                    AccessibilityHelper.EnhanceListBoxItem(container,
                        assetItem.FullPath.SubstringAfterLast('/'),
                        $"Path: {assetItem.FullPath}, Size: {assetItem.Size}");
                }
            }
        }
    }

    private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext != null)
        {
            dynamic folder = item.DataContext;
            try
            {
                string headerText = folder.Header;
                int assetCount = folder.AssetsList?.Assets?.Count ?? 0;
                int folderCount = folder.FoldersView?.Count ?? 0;

                AccessibilityHelper.EnhanceTreeViewItem(item,
                    headerText,
                    $"Folder contains {folderCount} subfolders and {assetCount} assets");
            }
            catch (Exception)
            {
                // Fail silently if dynamic properties aren't available
            }
        }
    }

    private void OnGridSplitterDoubleClick(object sender, MouseButtonEventArgs e)
    {
        RootGrid.ColumnDefinitions[0].Width = GridLength.Auto;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || e.OriginalSource is TextArea && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (_threadWorkerView.CanBeCanceled && e.Key == Key.Escape)
        {
            _applicationView.Status.SetStatus(EStatusKind.Stopping);
            _threadWorkerView.Cancel();
        }
        else if (_applicationView.Status.IsReady && e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            OnSearchViewClick(null, null);
        else if (e.Key == Key.Left && _applicationView.CUE4Parse.TabControl.SelectedTab.HasImage)
            _applicationView.CUE4Parse.TabControl.SelectedTab.GoPreviousImage();
        else if (e.Key == Key.Right && _applicationView.CUE4Parse.TabControl.SelectedTab.HasImage)
            _applicationView.CUE4Parse.TabControl.SelectedTab.GoNextImage();
        else if (FModel.Settings.UserSettings.Default.AssetAddTab.IsTriggered(e.Key))
            _applicationView.CUE4Parse.TabControl.AddTab();
        else if (FModel.Settings.UserSettings.Default.AssetRemoveTab.IsTriggered(e.Key))
            _applicationView.CUE4Parse.TabControl.RemoveTab();
        else if (FModel.Settings.UserSettings.Default.AssetLeftTab.IsTriggered(e.Key))
            _applicationView.CUE4Parse.TabControl.GoLeftTab();
        else if (FModel.Settings.UserSettings.Default.AssetRightTab.IsTriggered(e.Key))
            _applicationView.CUE4Parse.TabControl.GoRightTab();
        else if (FModel.Settings.UserSettings.Default.DirLeftTab.IsTriggered(e.Key) && LeftTabControl.SelectedIndex > 0)
            LeftTabControl.SelectedIndex--;
        else if (FModel.Settings.UserSettings.Default.DirRightTab.IsTriggered(e.Key) && LeftTabControl.SelectedIndex < LeftTabControl.Items.Count - 1)
            LeftTabControl.SelectedIndex++;
    }

    private void OnSearchViewClick(object sender, RoutedEventArgs e)
    {
        Helper.OpenWindow<AdonisWindow>("Search View", () => new SearchView().Show());
    }

    private void OnTabItemChange(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not TabControl tabControl)
            return;

        (tabControl.SelectedItem as System.Windows.Controls.TabItem)?.Focus();
    }

    private async void OnMappingsReload(object sender, ExecutedRoutedEventArgs e)
    {
        await _applicationView.CUE4Parse.InitMappings(true);
    }

    private void OnOpenAvalonFinder()
    {
        _applicationView.CUE4Parse.TabControl.SelectedTab.HasSearchOpen = true;
        AvalonEditor.YesWeSearch.Focus();
        AvalonEditor.YesWeSearch.SelectAll();
    }

    private void OnAssetsTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeView { SelectedItem: TreeItem treeItem } || treeItem.Folders.Count > 0)
            return;

        LeftTabControl.SelectedIndex++;
    }

    private async void OnAssetsListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        var selectedItems = listBox.SelectedItems.Cast<AssetItem>().ToList();
        await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.ExtractSelected(cancellationToken, selectedItems); });
    }

    private async void OnFolderExtractClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.ExtractFolder(cancellationToken, folder); });
        }
    }

    private async void OnFolderExportClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.ExportFolder(cancellationToken, folder); });
            FLogger.Append(ELog.Information, () =>
            {
                FLogger.Text("Successfully exported ", Constants.WHITE);
                FLogger.Link(folder.PathAtThisPoint, UserSettings.Default.RawDataDirectory, true);
            });
        }
    }

    private async void OnFolderSaveClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.SaveFolder(cancellationToken, folder); });
            FLogger.Append(ELog.Information, () =>
            {
                FLogger.Text("Successfully saved ", Constants.WHITE);
                FLogger.Link(folder.PathAtThisPoint, UserSettings.Default.PropertiesDirectory, true);
            });
        }
    }

    private async void OnFolderTextureClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.TextureFolder(cancellationToken, folder); });
            FLogger.Append(ELog.Information, () =>
            {
                FLogger.Text("Successfully saved ", Constants.WHITE);
                FLogger.Link(folder.PathAtThisPoint, UserSettings.Default.TextureDirectory, true);
            });
        }
    }

    private async void OnFolderModelClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.ModelFolder(cancellationToken, folder); });
        }
    }

    private async void OnFolderAnimationClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is TreeItem folder)
        {
            await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.AnimationFolder(cancellationToken, folder); });
        }
    }

    private void OnFavoriteDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is not TreeItem folder)
            return;

        _applicationView.CustomDirectories.Add(new CustomDirectory(folder.Header, folder.PathAtThisPoint));
        FLogger.Append(ELog.Information, () =>
            FLogger.Text($"Successfully saved '{folder.PathAtThisPoint}' as a new favorite directory", Constants.WHITE, true));
    }

    private void OnCopyDirectoryPathClick(object sender, RoutedEventArgs e)
    {
        if (AssetsFolderName.SelectedItem is not TreeItem folder)
            return;
        Clipboard.SetText(folder.PathAtThisPoint);
    }

    private void OnDeleteSearchClick(object sender, RoutedEventArgs e)
    {
        AssetsSearchName.Text = string.Empty;
        AssetsListName.ScrollIntoView(AssetsListName.SelectedItem);
    }

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || AssetsFolderName.SelectedItem is not TreeItem folder)
            return;

        var filters = textBox.Text.Trim().Split(' ');
        folder.AssetsList.AssetsView.Filter = o => { return o is AssetItem assetItem && filters.All(x => assetItem.FullPath.SubstringAfterLast('/').Contains(x, StringComparison.OrdinalIgnoreCase)); };
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_applicationView.Status.IsReady || sender is not ListBox listBox)
            return;
        FModel.Settings.UserSettings.Default.LoadingMode = ELoadingMode.Multiple;
        _applicationView.LoadingModes.LoadCommand.Execute(listBox.SelectedItems);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_applicationView.Status.IsReady || sender is not ListBox listBox)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                var selectedItems = listBox.SelectedItems.Cast<AssetItem>().ToList();
                await _threadWorkerView.Begin(cancellationToken => { _applicationView.CUE4Parse.ExtractSelected(cancellationToken, selectedItems); });
                break;
        }
    }
}
