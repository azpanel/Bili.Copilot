// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 视频动态分区详情视图模型.
/// </summary>
public sealed partial class VideoMomentSectionDetailViewModel : ViewModelBase, IMomentSectionDetailViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMomentSectionDetailViewModel"/> class.
    /// </summary>
    public VideoMomentSectionDetailViewModel(
        IMomentDiscoveryService service)
    {
        _service = service;
        _logger = this.Get<ILogger<VideoMomentSectionDetailViewModel>>();
        _lastOpenedMomentId = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.LastOpenedMomentVideoId, string.Empty);
        _openedMarkStyle = SettingsToolkit.ReadLocalSetting(
            Models.Constants.SettingNames.MomentVideoOpenedMarkStyle,
            Models.Constants.MomentVideoOpenedMarkStyle.AccentBorder);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Items.Count > 0 || _preventLoadMore)
        {
            return;
        }

        await LoadItemsAsync();
        SaveLastTenMomentIds();
    }

    [RelayCommand]
    private void Refresh()
    {
        _offset = default;
        _baseline = default;
        IsEmpty = false;
        _preventLoadMore = false;
        Items.Clear();
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            await LoadItemsAsync();
            SaveLastTenMomentIds();
        });
    }

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        if (IsEmpty || IsLoading || _preventLoadMore)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var view = await _service.GetVideoMomentsAsync(_offset, _baseline);
            if (view is not null)
            {
                _offset = view.Offset;
                _baseline = view.UpdateBaseline;
                _preventLoadMore = view.HasMoreMoments != true;
                foreach (var item in view.Moments.Select(p => CreateItem(p)))
                {
                    Items.Add(item);
                }

                ListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _preventLoadMore = true;
            _logger.LogError(ex, "加载动态列表失败.");
        }
        finally
        {
            IsEmpty = Items.Count == 0;
            IsLoading = false;
        }
    }

    private void ShowComment(MomentItemViewModel item)
        => this.Get<MomentPageViewModel>().ShowCommentCommand.Execute(item.Data);

    /// <summary>
    /// 更新最近打开标记的显示样式.
    /// </summary>
    public void SetOpenedMarkStyle(Models.Constants.MomentVideoOpenedMarkStyle style)
    {
        _openedMarkStyle = style;
        foreach (var item in Items)
        {
            item.OpenedMarkStyle = style;
        }
    }

    private MomentItemViewModel CreateItem(Richasy.BiliKernel.Models.Moment.MomentInformation data)
    {
        return new MomentItemViewModel(data, Models.Constants.MomentCardStyle.Video, ShowComment, MarkOpened)
        {
            IsLastOpened = data.Id == _lastOpenedMomentId,
            OpenedMarkStyle = _openedMarkStyle,
        };
    }

    private void MarkOpened(MomentItemViewModel openedItem)
    {
        _lastOpenedMomentId = openedItem.Data.Id;
        SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.LastOpenedMomentVideoId, _lastOpenedMomentId);
        foreach (var item in Items)
        {
            item.IsLastOpened = ReferenceEquals(item, openedItem);
        }
    }

    private void SaveLastTenMomentIds()
    {
        var momentIds = Items.Take(10).Select(p => p.Data.Id).ToList();
        var lastTenMomentIds = string.Join(',', momentIds);
        SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.LastTenMomentIds, lastTenMomentIds);
    }
}
