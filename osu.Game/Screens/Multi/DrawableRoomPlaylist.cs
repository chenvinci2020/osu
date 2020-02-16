// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Online.Multiplayer;
using osuTK;

namespace osu.Game.Screens.Multi
{
    public class DrawableRoomPlaylist : OsuRearrangeableListContainer<PlaylistItem>
    {
        public readonly Bindable<PlaylistItem> SelectedItem = new Bindable<PlaylistItem>();

        private readonly bool allowEdit;
        private readonly bool allowSelection;

        public DrawableRoomPlaylist(bool allowEdit, bool allowSelection)
        {
            this.allowEdit = allowEdit;
            this.allowSelection = allowSelection;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Scheduled since items are removed and re-added upon rearrangement
            Items.ItemsRemoved += items => Schedule(() =>
            {
                if (!Items.Contains(SelectedItem.Value))
                    SelectedItem.Value = null;
            });
        }

        protected override ScrollContainer<Drawable> CreateScrollContainer() => base.CreateScrollContainer().With(d =>
        {
            d.ScrollbarVisible = false;
        });

        protected override FillFlowContainer<RearrangeableListItem<PlaylistItem>> CreateListFillFlowContainer() => new FillFlowContainer<RearrangeableListItem<PlaylistItem>>
        {
            LayoutDuration = 200,
            LayoutEasing = Easing.OutQuint,
            Spacing = new Vector2(0, 2)
        };

        protected override OsuRearrangeableListItem<PlaylistItem> CreateOsuDrawable(PlaylistItem item) => new DrawableRoomPlaylistItem(item, allowEdit, allowSelection)
        {
            SelectedItem = { BindTarget = SelectedItem },
            RequestDeletion = requestDeletion
        };

        private void requestSelection(PlaylistItem item) => SelectedItem.Value = item;

        private void requestDeletion(PlaylistItem item)
        {
            if (SelectedItem.Value == item)
            {
                if (Items.Count == 1)
                    SelectedItem.Value = null;
                else
                    SelectedItem.Value = Items.GetNext(item) ?? Items[^2];
            }

            Items.Remove(item);
        }
    }
}