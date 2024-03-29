﻿using IATClient.Messages;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace IATClient
{
    public class ItemSlidePanel : Panel
    {
        private int NumSlides;
        private List<ItemSlideThumbnailPanel> ThumbnailPanels = new List<ItemSlideThumbnailPanel>();
        private List<Label> ThumbLabels = new List<Label>();
        private ItemSlideDisplayPanel FullSizedSlide;
        private static Padding ThumbnailPadding = new Padding(10, 10, 10, 5);
        private Panel ThumbPanel = new Panel();
        private int nCols, nColsWithSlide;
        private int nRows, nRowsWithSlide;
        private int nImagesReceived;
        private CItemSlideContainer ItemSlideContainer;
        private delegate void UpdateImageHandler(Image img);
        private int FullSizedNdx = -1;
        private bool _IsInitialized = false;
        private int CollapsedPanelWidth = 0;
        private ResultData.ResultData ResultData = null;
        private int _ResultSet = -1;

        public int ResultSet
        {
            get
            {
                return _ResultSet;
            }
            set
            {
                _ResultSet = value;
                if (FullSizedNdx != -1)
                {
                    FullSizedSlide.SetResultData(ItemSlideContainer.GetSlideLatencies(FullSizedNdx + 1), ItemSlideContainer.GetMeanSlideLatency(FullSizedNdx + 1),
                        ItemSlideContainer.GetMeanNumErrors(FullSizedNdx + 1), value + 1);
                    FullSizedSlide.Invalidate();
                }
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _IsInitialized;
            }
        }

        public ItemSlidePanel(Size sz, ResultData.ResultData rData, int resultSet)
        {
            this.Size = sz;
            this.ResultData = rData;
            this._ResultSet = resultSet;
        }

        public void Initialize(CItemSlideContainer itemSlideContainer)
        {
            ItemSlideContainer = itemSlideContainer;
            ItemSlideContainer.SetResultData(ResultData);
            NumSlides = ItemSlideContainer.NumSlides;
            FullSizedSlide = new ItemSlideDisplayPanel(new ItemSlideDisplayPanel.CloseEventHandler(HideFullSizedSlide), ItemSlideContainer.DisplaySize);
            FullSizedSlide.Location = new Point(CollapsedPanelWidth + ((this.Width - FullSizedSlide.Width - CollapsedPanelWidth) >> 1), (this.Height - FullSizedSlide.Height) >> 1);
            ThumbPanel.Size = this.Size;
            ThumbPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
            ThumbPanel.AutoScroll = true;
            Controls.Add(ThumbPanel);
            for (int ctr = 0; ctr < NumSlides; ctr++)
            {
                ItemSlideThumbnailPanel thumbPanel = new ItemSlideThumbnailPanel(new EventHandler(ItemSlide_Click), ItemSlideContainer.ThumbnailSize);
                ThumbnailPanels.Add(thumbPanel);
                Label l = new Label();
                l.Text = String.Format("Slide #{0}", ctr + 1);
                l.Font = System.Drawing.SystemFonts.DefaultFont;
                l.BackColor = System.Drawing.Color.White;
                l.ForeColor = System.Drawing.Color.Black;
                l.Size = TextRenderer.MeasureText(l.Text, l.Font);
                ThumbLabels.Add(l);
                ThumbPanel.Controls.Add(thumbPanel);
                var slide = itemSlideContainer.SlideDictionary[ctr];
                slide.FullSizedUpdate = (ndx) =>
                {
                    if (slide.DisplayImage == null)
                    {
                        var evt = new ManualResetEvent(false);
                        ItemSlideContainer.RequestDisplayImage(ndx, evt);
                        evt.WaitOne();
                    }
                    var img = slide.DisplayImage;
                    FullSizedSlide.BeginInvoke(
                    new Action<Image>((actionImage) =>
                    FullSizedSlide.SetImage(actionImage)
                        ), new object[] { img });
                };
                foreach (var reference in itemSlideContainer.SlideManifest.Contents.Cast<ManifestFile>().Select(f => f.ReferenceIds).Cast<IEnumerable<int>>()
                    .Aggregate((l1, l2) => l1.Concat(l2)))
                {
                    slide.ThumbnailRequesters[Convert.ToInt32(reference)] = (actionImage) => thumbPanel.BeginInvoke(new Action<Image>((img) => thumbPanel.SetBackgroundImage(img)), new object[] { actionImage });
                }
                ItemSlideContainer.ProcessSlides();
            }
            nCols = this.Width / (ItemSlideContainer.ThumbnailSize.Width + ThumbnailPadding.Horizontal);
            nRows = (NumSlides / nCols);
            if (NumSlides % nCols != 0)
                nRows++;
            nColsWithSlide = (this.Width - (ItemSlideContainer.DisplaySize.Width + ItemSlideDisplayPanel.DisplayPadding.Horizontal)) / (ItemSlideContainer.ThumbnailSize.Width + ThumbnailPadding.Horizontal);
            nRowsWithSlide = (NumSlides / nColsWithSlide);
            if (NumSlides % nColsWithSlide != 0)
                nRowsWithSlide++;
            CollapsedPanelWidth = (nColsWithSlide * (ItemSlideContainer.ThumbnailSize.Width + ThumbnailPadding.Horizontal)) + 20;
            LayoutThumbPanel(nCols, nRows);
            this.VerticalScroll.Value = 0;
            int nRowHeight = System.Drawing.SystemFonts.DefaultFont.Height + ItemSlideContainer.ThumbnailSize.Height + ThumbnailPadding.Vertical;
            _IsInitialized = true;
        }

        private void LayoutThumbPanel(int Cols, int Rows)
        {
            int xOffset = 0, yOffset = 0;
            ThumbPanel.Controls.Clear();
            for (int ctr1 = 0; ctr1 < Rows; ctr1++)
            {
                yOffset += ThumbnailPadding.Top;
                int cols;
                if (ctr1 != Rows - 1)
                    cols = Cols;
                else
                    cols = NumSlides - ((Rows - 1) * Cols);
                xOffset = 0;
                for (int ctr2 = 0; ctr2 < cols; ctr2++)
                {
                    xOffset += ThumbnailPadding.Left;
                    ThumbnailPanels[ctr1 * Cols + ctr2].Location = new Point(xOffset, yOffset);
                    ThumbPanel.Controls.Add(ThumbnailPanels[ctr1 * Cols + ctr2]);
                    Label l = ThumbLabels[ctr1 * Cols + ctr2];
                    l.Location = new Point(ThumbnailPanels[ctr1 * Cols + ctr2].Left + ((ThumbnailPanels[ctr1 * Cols + ctr2].Width - l.Size.Width) >> 1),
                        ThumbnailPanels[ctr1 * Cols + ctr2].Bottom + ThumbnailPadding.Bottom);
                    ThumbPanel.Controls.Add(l);
                    xOffset += ThumbnailPadding.Horizontal + ItemSlideContainer.ThumbnailSize.Width;
                }
                yOffset += System.Drawing.SystemFonts.DefaultFont.Height + ItemSlideContainer.ThumbnailSize.Height;
            }
            ThumbPanel.Invalidate();
        }

        private void ShowFullSizedSlide(int ndx)
        {
            SuspendLayout();
            if (FullSizedNdx == -1)
            {
                ItemSlideThumbnailPanel thumbPanel = ThumbnailPanels[ndx];
                int nRow = ndx / nRows;
                int yPos = thumbPanel.Top - ThumbPanel.VerticalScroll.Value;
                int nNewRow = ndx / nRowsWithSlide;
                ThumbPanel.Width = CollapsedPanelWidth;
                LayoutThumbPanel(nColsWithSlide, nRowsWithSlide);
                //                ThumbPanel.VerticalScroll.Value = thumbPanel.Top - yPos;
                Controls.Add(FullSizedSlide);
            }
            FullSizedSlide.SetResultData(ItemSlideContainer.GetSlideLatencies(ndx + 1), ItemSlideContainer.GetMeanSlideLatency(ndx + 1), ItemSlideContainer.GetMeanNumErrors(ndx + 1), ResultSet + 1);
            var fileRefs = ItemSlideContainer.SlideManifest.Contents.Cast<ManifestFile>().Select(f => new { res = f.ResourceId, refs = f.ReferenceIds });
            var slideNum = fileRefs.Where(fr => fr.refs.Contains(ndx)).Select(fr => fr.res).First();
            var slide = ItemSlideContainer.SlideDictionary[slideNum];
            slide.FullSizedUpdate(ndx);
            FullSizedSlide.Invalidate();
            ItemSlideContainer.RequestDisplayImage(ndx + 1);
            ResumeLayout(false);
            FullSizedNdx = ndx;
        }

        private void HideFullSizedSlide()
        {
            SuspendLayout();
            Controls.Remove(FullSizedSlide);
            ItemSlideThumbnailPanel thumbPanel = ThumbnailPanels[FullSizedNdx];
            int nRow = FullSizedNdx / nRowsWithSlide;
            int yPos = thumbPanel.Top - ThumbPanel.VerticalScroll.Value;
            ThumbPanel.Width = this.ClientRectangle.Width;
            LayoutThumbPanel(nCols, nRows);
            FullSizedNdx = -1;
            ResumeLayout(false);
        }

        public void OnDisplaySlideClose(object sender, EventArgs e)
        {
            HideFullSizedSlide();
        }

        public void ItemSlide_Click(object sender, EventArgs e)
        {
            if (FullSizedNdx == -1)
            {
                int ndx = ThumbnailPanels.ToList<object>().IndexOf(sender);
                ShowFullSizedSlide(ndx);
            }
            else
                HideFullSizedSlide();
        }
    }
}
