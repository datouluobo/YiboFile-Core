using System;
using System.Collections.ObjectModel;
using System.Windows;
using YiboFile.Services.Favorite;
using YiboFile.ViewModels.Messaging;

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
            // 订阅 Service 事件
            _favoriteService.FavoritesLoaded += OnFavoritesLoaded;

            // 初始加载
            LoadFavorites();
        }

        protected override void OnShutdown()
        {
            _favoriteService.FavoritesLoaded -= OnFavoritesLoaded;
        }

        private void OnFavoritesLoaded(object sender, EventArgs e)
        {
            // 确保在 UI 线程执行
            Application.Current.Dispatcher.Invoke(LoadFavorites);
        }

        /// <summary>
        /// 加载收藏数据
        /// </summary>
        public void LoadFavorites()
        {
            try
            {
                var groups = _favoriteService.GetFavoriteGroups();
                FavoriteGroups.Clear();
                foreach (var group in groups)
                {
                    FavoriteGroups.Add(group);
                }
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
