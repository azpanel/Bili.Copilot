// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Windows.Storage;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 按账号保存动态视频的已看状态.
/// </summary>
internal static class MomentWatchHistoryToolkit
{
    private static ApplicationDataContainer? _container;
    private static string? _userId;

    public static event EventHandler? WatchedChanged;

    public static bool IsWatched(string mediaKey)
        => GetContainer()?.Values.ContainsKey(mediaKey) == true;

    public static void MarkWatched(string mediaKey, bool notify = true)
    {
        var container = GetContainer();
        if (container is null || container.Values.ContainsKey(mediaKey))
        {
            return;
        }

        container.Values[mediaKey] = true;
        if (notify)
        {
            WatchedChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    private static ApplicationDataContainer? GetContainer()
    {
        var userId = GlobalDependencies.Kernel.Get<AccountViewModel>().MyProfile?.User.Id;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (_container is null || userId != _userId)
        {
            _userId = userId;
            _container = ApplicationData.GetDefault().LocalSettings
                .CreateContainer("MomentWatchHistory", ApplicationDataCreateDisposition.Always)
                .CreateContainer(userId, ApplicationDataCreateDisposition.Always);
        }

        return _container;
    }
}
