using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Collections;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Mvis.BottomBar.Buttons;
using osuTK;

namespace osu.Game.Screens.Mvis.Modules.v2
{
    public class CollectionSelectPanel : Container, ISidebarContent
    {
        [Resolved]
        private CollectionManager collectionManager { get; set; }

        [Resolved]
        private CollectionHelper collectionHelper { get; set; }

        private Bindable<BeatmapCollection> SelectedCollection = new Bindable<BeatmapCollection>();
        private Bindable<CollectionPanel> SelectedPanel = new Bindable<CollectionPanel>();

        private FillFlowContainer<CollectionPanel> collectionsFillFlow;
        private CollectionPanel selectedpanel;
        private CollectionPanel prevPanel;
        private OsuScrollContainer collectionScroll;
        private CollectionInfo info;

        public float ResizeWidth => 0.85f;

        public CollectionSelectPanel()
        {
            RelativeSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new Container
                {
                    Name = "收藏夹选择界面",
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.3f,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Children = new Drawable[]
                    {
                        collectionScroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = collectionsFillFlow = new FillFlowContainer<CollectionPanel>
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                                Spacing = new Vector2(10),
                                Padding = new MarginPadding(25),
                                Margin = new MarginPadding{Bottom = 40}
                            }
                        },
                        new BottomBarButton()
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Size = new Vector2(90, 30),
                            Text = "刷新列表",
                            NoIcon = true,
                            Action = () => RefreshCollectionList(),
                            Margin = new MarginPadding(5),
                        }
                    }
                },
                info = new CollectionInfo()
                {
                    Name = "收藏夹信息界面",
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.7f,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            collectionHelper.CurrentCollection.BindValueChanged(OnCurrentCollectionChanged);
            SelectedCollection.BindValueChanged(UpdateSelection);
            SelectedPanel.BindValueChanged(UpdateSelectedPanel);

            RefreshCollectionList();
        }

        private void OnCurrentCollectionChanged(ValueChangedEvent<BeatmapCollection> v)
        {
            if (v.NewValue == null) return;

            info.UpdateCollection(v.NewValue, true);

            SearchForCurrentSelection();
        }

        /// <summary>
        /// 当<see cref="CollectionPanel"/>被选中时执行
        /// </summary>
        private void UpdateSelection(ValueChangedEvent<BeatmapCollection> v)
        {
            if (v.NewValue == null) return;

            //如果选择的收藏夹为正在播放的收藏夹，则更新isCurrent为true
            if (v.NewValue == collectionHelper.CurrentCollection.Value)
                info.UpdateCollection(v.NewValue, true);
            else
                info.UpdateCollection(v.NewValue, false);
        }

        private void UpdateSelectedPanel(ValueChangedEvent<CollectionPanel> v)
        {
            if (v.NewValue == null) return;
            selectedpanel?.Reset();
            selectedpanel = v.NewValue;
        }

        private void SearchForCurrentSelection()
        {
            prevPanel?.Reset(true);

            foreach (var p in collectionsFillFlow)
                if (p.collection == collectionHelper.CurrentCollection.Value)
                    selectedpanel = prevPanel = p;

            if (selectedpanel != null
                    && collectionHelper.CurrentCollection.Value.Beatmaps.Count != 0 )
                selectedpanel.state.Value = ActiveState.Active;
        }

        public void RefreshCollectionList()
        {
            var oldCollection = collectionHelper.CurrentCollection.Value;

            //清空界面
            collectionsFillFlow.Clear();
            info.UpdateCollection(null, false);
            selectedpanel = null;

            SelectedCollection.Value = null;

            //如果收藏夹被删除，则留null
            if (!collectionManager.Collections.Contains(oldCollection))
                oldCollection = null;

            //如果收藏夹为0，则淡出sollectionScroll
            //否则，添加CollectionPanel
            if (collectionManager.Collections.Count == 0)
            {
                collectionScroll.FadeOut(300);
            }
            else
            {
                collectionsFillFlow.AddRange(collectionManager.Collections.Select(c => new CollectionPanel(c, MakeCurrentSelected)
                {
                    SelectedCollection = { BindTarget = this.SelectedCollection },
                    SelectedPanel = { BindTarget = this.SelectedPanel }
                }));
                collectionScroll.FadeIn(300);
            }

            //重新赋值
            collectionHelper.CurrentCollection.Value = SelectedCollection.Value = oldCollection;

            //根据选中的收藏夹寻找对应的BeatmapPanel
            SearchForCurrentSelection();
        }

        private void MakeCurrentSelected()
        {
            collectionHelper.CurrentCollection.Value = SelectedCollection.Value;
        }
    }
}