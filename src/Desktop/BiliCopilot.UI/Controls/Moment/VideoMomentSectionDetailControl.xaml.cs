// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 视频动态详情控件.
/// </summary>
public sealed partial class VideoMomentSectionDetailControl : VideoMomentSectionDetailControlBase
{
    private bool _isActive;
    private bool _anchorUpdateQueued;
    private MomentItemViewModel? _lastAnchor;
    private ItemsRepeater? _itemsRepeater;
    private ItemsRepeater? _watchedRepeater;
    private long _paneWidthCallback;
    private bool _isApplyingPaneWidth;
    private bool _paneWidthDirty;
    private double _preferredLeftPaneRatio;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _savePaneWidthTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMomentSectionDetailControl"/> class.
    /// </summary>
    public VideoMomentSectionDetailControl()
    {
        InitializeComponent();
        _preferredLeftPaneRatio = SettingsToolkit.ReadLocalSetting(SettingNames.MomentVideoLeftPaneRatio, -1d);
        if (!double.IsFinite(_preferredLeftPaneRatio) || _preferredLeftPaneRatio <= 0 || _preferredLeftPaneRatio >= 1)
        {
            _preferredLeftPaneRatio = double.NaN;
        }
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.ListUpdated += OnListUpdatedAsync;
        MomentWatchHistoryToolkit.WatchedChanged += OnWatchedChanged;
        _isActive = true;
        _paneWidthCallback = ColumnResizer.RegisterPropertyChangedCallback(
            ColumnSplitter.ColumnWidthProperty, OnPaneWidthChanged);
        _savePaneWidthTimer = DispatcherQueue.CreateTimer();
        _savePaneWidthTimer.Interval = TimeSpan.FromMilliseconds(300);
        _savePaneWidthTimer.IsRepeating = false;
        _savePaneWidthTimer.Tick += OnSavePaneWidthTick;
        ApplyPaneWidths();
        ViewModel.SynchronizeWatchedItems();
        if (!ViewModel.IsLoading && (ViewModel.Items.Count > 0 || ViewModel.WatchedItems.Count > 0))
        {
            ViewModel.ResetHistoryCursor();
            ViewModel.SynchronizeHistoryCommand.Execute(default);
        }
        QueueAnchorUpdate();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.ListUpdated -= OnListUpdatedAsync;
        MomentWatchHistoryToolkit.WatchedChanged -= OnWatchedChanged;
        _isActive = false;
        ColumnResizer.UnregisterPropertyChangedCallback(ColumnSplitter.ColumnWidthProperty, _paneWidthCallback);
        if (_savePaneWidthTimer is not null)
        {
            _savePaneWidthTimer.Stop();
            _savePaneWidthTimer.Tick -= OnSavePaneWidthTick;
            _savePaneWidthTimer = null;
        }

        SavePaneWidth();
        if (_itemsRepeater is not null)
        {
            _itemsRepeater.ElementPrepared -= OnMomentElementPrepared;
        }

        if (_watchedRepeater is not null)
        {
            _watchedRepeater.ElementPrepared -= OnMomentElementPrepared;
        }

        _itemsRepeater = null;
        _watchedRepeater = null;
    }

    private async void OnListUpdatedAsync(object? sender, EventArgs e)
    {
        if (ViewModel.Items.Count > 0)
        {
            await View.DelayCheckItemsAsync();
        }
        else
        {
            CheckWatchedLoadMore();
        }

        QueueAnchorUpdate();
    }

    private void OnWatchedChanged(object? sender, EventArgs e)
    {
        ViewModel.SynchronizeWatchedItems();
        QueueAnchorUpdate();
        if (ViewModel.Items.Count > 0)
        {
            _ = View.DelayCheckItemsAsync();
        }
    }

    private void OnSectionSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyPaneWidths();
        QueueAnchorUpdate();
    }

    private void ApplyPaneWidths(double? requestedLeftWidth = null)
    {
        var available = Math.Max(0, LayoutRoot.ActualWidth - 12);
        if (available <= 0)
        {
            return;
        }

        var minLeft = Math.Min(300, available * 0.55);
        var minRight = Math.Min(240, available * 0.45);
        var preferred = double.IsNaN(_preferredLeftPaneRatio)
            ? available - Math.Clamp(available * 0.28, 220, 360)
            : available * _preferredLeftPaneRatio;
        var maxLeft = Math.Max(minLeft, available - minRight);
        var leftWidth = Math.Clamp(requestedLeftWidth ?? preferred, minLeft, maxLeft);
        _isApplyingPaneWidth = true;
        try
        {
            ColumnResizer.MinColumnWidth = minLeft;
            ColumnResizer.MaxColumnWidth = maxLeft;
            ColumnResizer.ColumnWidth = leftWidth;
            WatchedColumn.Width = new GridLength(available - leftWidth);
        }
        finally
        {
            _isApplyingPaneWidth = false;
        }

        QueueAnchorUpdate();
    }

    private void OnPaneWidthChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_isApplyingPaneWidth || !_isActive || LayoutRoot.ActualWidth <= 12)
        {
            return;
        }

        ApplyPaneWidths(ColumnResizer.ColumnWidth);
        _preferredLeftPaneRatio = ColumnResizer.ColumnWidth / (LayoutRoot.ActualWidth - 12);
        _paneWidthDirty = true;
        _savePaneWidthTimer?.Stop();
        _savePaneWidthTimer?.Start();
    }

    private void OnSavePaneWidthTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        => SavePaneWidth();

    private void SavePaneWidth()
    {
        if (_paneWidthDirty)
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.MomentVideoLeftPaneRatio, _preferredLeftPaneRatio);
            _paneWidthDirty = false;
        }
    }

    private void OnSplitterDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        _preferredLeftPaneRatio = double.NaN;
        _paneWidthDirty = false;
        _savePaneWidthTimer?.Stop();
        SettingsToolkit.DeleteLocalSetting(SettingNames.MomentVideoLeftPaneRatio);
        ApplyPaneWidths();
        e.Handled = true;
    }

    private void OnSplitterKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right)
        {
            ColumnResizer.ColumnWidth += e.Key == Windows.System.VirtualKey.Left ? -16 : 16;
            e.Handled = true;
        }
    }

    private void OnScrollPositionChanged(object? sender, EventArgs e) => QueueAnchorUpdate();

    private void OnWatchedViewChanged(ScrollView sender, object e) => CheckWatchedLoadMore();

    private void CheckWatchedLoadMore()
    {
        if (_isActive && ViewModel.Items.Count == 0 && !ViewModel.IsHistoryLoading && !ViewModel.IsLoading
            && WatchedView.ExtentHeight - WatchedView.ViewportHeight - WatchedView.VerticalOffset <= 400)
        {
            ViewModel.LoadItemsCommand.Execute(default);
        }
    }

    private void OnMomentLayoutUpdated(object? sender, object e) => QueueAnchorUpdate();

    private void QueueAnchorUpdate()
    {
        if (!_isActive || _anchorUpdateQueued)
        {
            return;
        }

        _anchorUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _anchorUpdateQueued = false;
            if (_isActive)
            {
                UpdateTimelineAnchor();
            }
        });
    }

    private void UpdateTimelineAnchor()
    {
        if (_itemsRepeater is null)
        {
            _itemsRepeater = FindRepeater(MomentRepeater);
            ConfigureItemAnimations(_itemsRepeater);
        }

        if (_watchedRepeater is null)
        {
            _watchedRepeater = FindRepeater(WatchedRepeater);
            ConfigureItemAnimations(_watchedRepeater);
        }

        if (_itemsRepeater is null)
        {
            return;
        }

        MomentItemViewModel? anchor = null;
        var top = double.MaxValue;
        var right = double.MinValue;
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(_itemsRepeater); i++)
        {
            if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(_itemsRepeater, i) is not FrameworkElement element
                || element.ActualHeight <= 0)
            {
                continue;
            }

            var index = _itemsRepeater.GetElementIndex(element);
            if (index < 0 || index >= ViewModel.Items.Count)
            {
                continue;
            }

            var point = element.TransformToVisual(View).TransformPoint(new Windows.Foundation.Point());
            if (point.Y + element.ActualHeight <= 0 || point.Y >= View.ActualHeight)
            {
                continue;
            }

            if (point.Y < top - 1 || (Math.Abs(point.Y - top) <= 1 && point.X > right))
            {
                top = point.Y;
                right = point.X;
                anchor = ViewModel.Items[index];
            }
        }

        ViewModel.UpdateTimelineAnchor(anchor, View.GetScrollPosition() < 1);
        if (!ReferenceEquals(anchor, _lastAnchor))
        {
            _lastAnchor = anchor;
            WatchedView.ScrollTo(0, 0);
        }
    }

    private void ConfigureItemAnimations(ItemsRepeater? repeater)
    {
        if (repeater is null)
        {
            return;
        }

        repeater.ElementPrepared += OnMomentElementPrepared;
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(repeater); i++)
        {
            if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(repeater, i) is UIElement element)
            {
                SetItemTransitions(element);
            }
        }
    }

    private void OnMomentElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        => SetItemTransitions(args.Element);

    private static void SetItemTransitions(UIElement element)
    {
        element.Transitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection
        {
            new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition
            {
                FromHorizontalOffset = 0,
                FromVerticalOffset = 12,
                IsStaggeringEnabled = false,
            },
        };
    }

    private static ItemsRepeater? FindRepeater(DependencyObject parent)
    {
        if (parent is ItemsRepeater repeater)
        {
            return repeater;
        }

        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (FindRepeater(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i)) is { } child)
            {
                return child;
            }
        }

        return null;
    }
}

/// <summary>
/// 视频动态详情控件基类.
/// </summary>
public abstract class VideoMomentSectionDetailControlBase : LayoutUserControlBase<VideoMomentSectionDetailViewModel>
{
}
