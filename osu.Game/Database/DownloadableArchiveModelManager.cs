// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Humanizer;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Overlays.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;

namespace osu.Game.Database
{
    /// <summary>
    /// An <see cref="ArchiveModelManager{TModel, TFileModel}"/> that has the ability to download models using an <see cref="IAPIProvider"/> and
    /// import them into the store.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TFileModel">The associated file join type.</typeparam>
    public abstract class DownloadableArchiveModelManager<TModel, TFileModel> : ArchiveModelManager<TModel, TFileModel>, IModelDownloader<TModel>
        where TModel : class, IHasFiles<TFileModel>, IHasPrimaryKey, ISoftDelete, IEquatable<TModel>
        where TFileModel : class, INamedFileInfo, new()
    {
        public IBindable<WeakReference<ArchiveDownloadRequest<TModel>>> DownloadBegan => downloadBegan;

        private readonly Bindable<WeakReference<ArchiveDownloadRequest<TModel>>> downloadBegan = new Bindable<WeakReference<ArchiveDownloadRequest<TModel>>>();

        public IBindable<WeakReference<ArchiveDownloadRequest<TModel>>> DownloadFailed => downloadFailed;

        private readonly Bindable<WeakReference<ArchiveDownloadRequest<TModel>>> downloadFailed = new Bindable<WeakReference<ArchiveDownloadRequest<TModel>>>();

        private readonly IAPIProvider api;

        private readonly List<ArchiveDownloadRequest<TModel>> currentDownloads = new List<ArchiveDownloadRequest<TModel>>();

        private readonly MutableDatabaseBackedStoreWithFileIncludes<TModel, TFileModel> modelStore;

        protected DownloadableArchiveModelManager(Storage storage, IDatabaseContextFactory contextFactory, IAPIProvider api, MutableDatabaseBackedStoreWithFileIncludes<TModel, TFileModel> modelStore,
                                                  IIpcHost importHost = null)
            : base(storage, contextFactory, modelStore, importHost)
        {
            this.api = api;
            this.modelStore = modelStore;
        }

        /// <summary>
        /// Creates the download request for this <typeparamref name="TModel"/>.
        /// </summary>
        /// <param name="model">The <typeparamref name="TModel"/> to be downloaded.</param>
        /// <param name="UseSayobot">Decides whether to use sayobot to download.</param>
        /// <param name="noVideo">Whether this download should be optimised for slow connections. Generally means Videos are not included in the download bundle.</param>
        /// <param name="IsMini">Whether this downlaod should be optimised for very slow connections. Generally means any extra files are not included in the download bundle.</param>
        /// <returns>The request object.</returns>
        protected abstract ArchiveDownloadRequest<TModel> CreateDownloadRequest(TModel model, bool UseSayobot, bool noVideo, bool IsMini);

        /// <summary>
        /// Begin a download for the requested <typeparamref name="TModel"/>.
        /// </summary>
        /// <param name="model">The <typeparamref name="TModel"/> to be downloaded.</param>
        /// <param name="UseSayobot">Decides whether to use sayobot to download.</param>
        /// <param name="noVideo">Whether this download should be optimised for slow connections. Generally means Videos are not included in the download bundle.</param>
        /// <param name="IsMini">Whether this downlaod should be optimised for very slow connections. Generally means any extra files are not included in the download bundle.</param>
        /// <returns>Whether the download was started.</returns>
        public bool Download(TModel model, bool UseSayobot, bool noVideo = false, bool IsMini = false)
        {
            if (!canDownload(model)) return false;

            var request = CreateDownloadRequest(model, UseSayobot, noVideo, IsMini);

            DownloadNotification notification = new DownloadNotification
            {
                Text = $"正在下载 {request.Model}",
            };

            request.DownloadProgressed += progress =>
            {
                notification.State = ProgressNotificationState.Active;
                notification.Progress = progress;
            };

            request.Success += filename =>
            {
                Task.Factory.StartNew(async () =>
                {
                    // This gets scheduled back to the update thread, but we want the import to run in the background.
                    var imported = await Import(notification, filename);

                    // for now a failed import will be marked as a failed download for simplicity.
                    if (!imported.Any())
                        downloadFailed.Value = new WeakReference<ArchiveDownloadRequest<TModel>>(request);

                    currentDownloads.Remove(request);
                }, TaskCreationOptions.LongRunning);
            };

            request.Failure += triggerFailure;

            notification.CancelRequested += () =>
            {
                request.Cancel();
                return true;
            };

            currentDownloads.Add(request);
            PostNotification?.Invoke(notification);

            api.PerformAsync(request);

            downloadBegan.Value = new WeakReference<ArchiveDownloadRequest<TModel>>(request);
            return true;

            void triggerFailure(Exception error)
            {
                currentDownloads.Remove(request);

                downloadFailed.Value = new WeakReference<ArchiveDownloadRequest<TModel>>(request);

                downloadBegan.Value = new WeakReference<ArchiveDownloadRequest<TModel>>(request);
                
                notification.State = ProgressNotificationState.Cancelled;

                if (!(error is OperationCanceledException))
                    Logger.Error(error, $"{HumanisedModelName.Titleize()}下载失败!");
            }
        }

        public bool IsAvailableLocally(TModel model) => CheckLocalAvailability(model, modelStore.ConsumableItems.Where(m => !m.DeletePending));

        /// <summary>
        /// Performs implementation specific comparisons to determine whether a given model is present in the local store.
        /// </summary>
        /// <param name="model">The <typeparamref name="TModel"/> whose existence needs to be checked.</param>
        /// <param name="items">The usable items present in the store.</param>
        /// <returns>Whether the <typeparamref name="TModel"/> exists.</returns>
        protected virtual bool CheckLocalAvailability(TModel model, IQueryable<TModel> items)
            => model.ID > 0 && items.Any(i => i.ID == model.ID && i.Files.Any());

        public ArchiveDownloadRequest<TModel> GetExistingDownload(TModel model) => currentDownloads.Find(r => r.Model.Equals(model));

        private bool canDownload(TModel model) => GetExistingDownload(model) == null && api != null;

        private class DownloadNotification : ProgressNotification
        {
            public override bool IsImportant => false;

            protected override Notification CreateCompletionNotification() => new SilencedProgressCompletionNotification
            {
                Activated = CompletionClickAction,
                Text = CompletionText
            };

            private class SilencedProgressCompletionNotification : ProgressCompletionNotification
            {
                public override bool IsImportant => false;
            }
        }
    }
}
