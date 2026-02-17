using System;
using System.Collections.ObjectModel;
using System.Windows;
using YiboFile.Services.Favorite;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using System.Linq;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 收藏夹模块
    /// MVVM 架构下的收藏管理
    /// </summary>
    public class FavoritesModule : ModuleBase
    {
        private readonly FavoriteService _favoriteService;

        /// <summary>
        /// 收藏分组集合
        /// </summary>
        public ObservableCollection<FavoriteService.FavoriteGroupItem> FavoriteGroups { get; } = new();

        public override string Name => "Favorites";

        public FavoritesModule(IMessageBus messageBus, FavoriteService favoriteService) : base(messageBus)
        {
            _favoriteService = favoriteService ?? throw new ArgumentNullException(nameof(favoriteService));
        }

        protected override void OnInitialize()
        {
            Subscribe<FavoritesUpdatedMessage>(msg => LoadFavorites());

            Subscribe<AddFavoriteRequestMessage>(OnAddFavoriteRequest);
            Subscribe<CreateFavoriteGroupRequestMessage>(OnCreateFavoriteGroupRequest);

            // 初始加载
            LoadFavorites();
        }

        private void OnAddFavoriteRequest(AddFavoriteRequestMessage msg)
        {
            if (msg.Items == null || msg.Items.Count == 0) return;
            _favoriteService.AddFavorite(msg.Items, msg.GroupId);
        }

        private void OnCreateFavoriteGroupRequest(CreateFavoriteGroupRequestMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Name)) return;
            int newGroupId = _favoriteService.CreateGroup(msg.Name.Trim());
            if (newGroupId != -1 && msg.InitialItems != null && msg.InitialItems.Count > 0)
            {
                _favoriteService.AddFavorite(msg.InitialItems, newGroupId);
            }
        }

        protected override void OnShutdown()
        {
            // Do nothing
        }

        /// <summary>
        /// 加载收藏数据
        /// </summary>
        public void LoadFavorites()
        {
            try
            {
                var groups = _favoriteService.GetFavoriteGroups();
                // 在 UI 线程更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FavoriteGroups.Clear();
                    foreach (var group in groups)
                    {
                        FavoriteGroups.Add(group);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FavoritesModule] Load failed: {ex.Message}");
            }
        }

        #region 公开操作

        public void AddFavorite(System.Collections.Generic.List<YiboFile.Models.FileSystemItem> items, int groupId = 1)
        {
            _favoriteService.AddFavorite(items, groupId);
        }

        public void RenameGroup(int id, string newName)
        {
            _favoriteService.RenameGroup(id, newName);
        }

        public void DeleteGroup(int id)
        {
            _favoriteService.DeleteGroup(id);
        }

        #endregion
    }
}
