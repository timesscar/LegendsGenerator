﻿// -------------------------------------------------------------------------------------------------
// <copyright file="SyntaxHighlightBox.xaml.cs" company="Tom Luppi">
//     Copyright (c) Tom Luppi.  All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace LegendsGenerator.Editor.CodeEditor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    public partial class SyntaxHighlightBox : TextBox
    {

        // --------------------------------------------------------------------
        // Attributes
        // --------------------------------------------------------------------

        public double LineHeight
        {
            get
            {
                return this.lineHeight;
            }

            set
            {
                if (value != this.lineHeight)
                {
                    this.lineHeight = value;
                    this.blockHeight = this.MaxLineCountInBlock * value;
                    TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
                    TextBlock.SetLineHeight(this, this.lineHeight);
                }
            }
        }

        public int MaxLineCountInBlock
        {
            get
            {
                return this.maxLineCountInBlock;
            }

            set
            {
                this.maxLineCountInBlock = value > 0 ? value : 0;
                this.blockHeight = value * this.LineHeight;
            }
        }

        public IHighlighter CurrentHighlighter { get; set; }

        private DrawingControl renderCanvas;
        private DrawingControl lineNumbersCanvas;
        private ScrollViewer scrollViewer;
        private double lineHeight;
        private int totalLineCount;
        private List<InnerTextBlock> blocks;
        private double blockHeight;
        private int maxLineCountInBlock;

        // --------------------------------------------------------------------
        // Ctor and event handlers
        // --------------------------------------------------------------------

        public SyntaxHighlightBox()
        {
            this.InitializeComponent();

            this.MaxLineCountInBlock = 100;
            this.LineHeight = this.FontSize * 1.3;
            this.totalLineCount = 1;
            this.blocks = new List<InnerTextBlock>();

            this.CurrentHighlighter = HighlighterManager.Instance.Highlighters[this.SyntaxLanguage];

            this.Loaded += (s, e) =>
            {
                this.renderCanvas = (DrawingControl)this.Template.FindName("PART_RenderCanvas", this);
                this.lineNumbersCanvas = (DrawingControl)this.Template.FindName("PART_LineNumbersCanvas", this);
                this.scrollViewer = (ScrollViewer)this.Template.FindName("PART_ContentHost", this);

                this.lineNumbersCanvas.Width = this.GetFormattedTextWidth(string.Format("{0:0000}", this.totalLineCount)) + 5;

                this.scrollViewer.ScrollChanged += this.OnScrollChanged;

                this.InvalidateBlocks(0);
                this.InvalidateVisual();
            };

            this.SizeChanged += (s, e) =>
            {
                if (e.HeightChanged == false)
                {
                    return;
                }

                this.UpdateBlocks();
                this.InvalidateVisual();
            };

            this.TextChanged += (s, e) =>
            {
                this.UpdateTotalLineCount();
                this.InvalidateBlocks(e.Changes.First().Offset);
                this.InvalidateVisual();
            };
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            this.DrawBlocks();
            base.OnRender(drawingContext);
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                this.UpdateBlocks();
            }

            this.InvalidateVisual();
        }

        // -----------------------------------------------------------
        // Updating & Block managing
        // -----------------------------------------------------------

        private void UpdateTotalLineCount()
        {
            this.totalLineCount = TextUtilities.GetLineCount(this.Text);
        }

        private void UpdateBlocks()
        {
            if (this.blocks.Count == 0)
            {
                return;
            }

            // While something is visible after last block...
            while (!this.blocks.Last().IsLast && this.blocks.Last().Position.Y + this.blockHeight - this.VerticalOffset < this.ActualHeight)
            {
                int firstLineIndex = this.blocks.Last().LineEndIndex + 1;
                int lastLineIndex = firstLineIndex + this.maxLineCountInBlock - 1;
                lastLineIndex = lastLineIndex <= this.totalLineCount - 1 ? lastLineIndex : this.totalLineCount - 1;

                int fisrCharIndex = this.blocks.Last().CharEndIndex + 1;
                int lastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(this.Text, lastLineIndex); // to be optimized (forward search)

                if (lastCharIndex <= fisrCharIndex)
                {
                    this.blocks.Last().IsLast = true;
                    return;
                }

                InnerTextBlock block = new InnerTextBlock(
                    fisrCharIndex,
                    lastCharIndex,
                    this.blocks.Last().LineEndIndex + 1,
                    lastLineIndex,
                    this.LineHeight);
                block.RawText = block.GetSubString(this.Text);
                block.LineNumbers = this.GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                this.blocks.Add(block);
                this.FormatBlock(block, this.blocks.Count > 1 ? this.blocks[this.blocks.Count - 2] : null);
            }
        }

        private void InvalidateBlocks(int changeOffset)
        {
            InnerTextBlock blockChanged = null;
            for (int i = 0; i < this.blocks.Count; i++)
            {
                if (this.blocks[i].CharStartIndex <= changeOffset && changeOffset <= this.blocks[i].CharEndIndex + 1)
                {
                    blockChanged = this.blocks[i];
                    break;
                }
            }

            if (blockChanged == null && changeOffset > 0)
            {
                blockChanged = this.blocks.Last();
            }

            int fvline = blockChanged != null ? blockChanged.LineStartIndex : 0;
            int lvline = this.GetIndexOfLastVisibleLine();
            int fvchar = blockChanged != null ? blockChanged.CharStartIndex : 0;
            int lvchar = TextUtilities.GetLastCharIndexFromLineIndex(this.Text, lvline);

            if (blockChanged != null)
            {
                this.blocks.RemoveRange(this.blocks.IndexOf(blockChanged), this.blocks.Count - this.blocks.IndexOf(blockChanged));
            }

            int localLineCount = 1;
            int charStart = fvchar;
            int lineStart = fvline;
            for (int i = fvchar; i < this.Text.Length; i++)
            {
                if (this.Text[i] == '\n')
                {
                    localLineCount += 1;
                }

                if (i == this.Text.Length - 1)
                {
                    string blockText = this.Text.Substring(charStart);
                    InnerTextBlock block = new InnerTextBlock(
                        charStart,
                        i, lineStart,
                        lineStart + TextUtilities.GetLineCount(blockText) - 1,
                        this.LineHeight);
                    block.RawText = block.GetSubString(this.Text);
                    block.LineNumbers = this.GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                    block.IsLast = true;

                    foreach (InnerTextBlock b in this.blocks)
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception();
                        }

                    this.blocks.Add(block);
                    this.FormatBlock(block, this.blocks.Count > 1 ? this.blocks[this.blocks.Count - 2] : null);
                    break;
                }

                if (localLineCount > this.maxLineCountInBlock)
                {
                    InnerTextBlock block = new InnerTextBlock(
                        charStart,
                        i,
                        lineStart,
                        lineStart + this.maxLineCountInBlock - 1,
                        this.LineHeight);
                    block.RawText = block.GetSubString(this.Text);
                    block.LineNumbers = this.GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);

                    foreach (InnerTextBlock b in this.blocks)
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception();
                        }

                    this.blocks.Add(block);
                    this.FormatBlock(block, this.blocks.Count > 1 ? this.blocks[this.blocks.Count - 2] : null);

                    charStart = i + 1;
                    lineStart += this.maxLineCountInBlock;
                    localLineCount = 1;

                    if (i > lvchar)
                    {
                        break;
                    }
                }
            }
        }

        // -----------------------------------------------------------
        // Rendering
        // -----------------------------------------------------------

        private void DrawBlocks()
        {
            if (!this.IsLoaded || this.renderCanvas == null || this.lineNumbersCanvas == null)
            {
                return;
            }

            this.CurrentHighlighter = HighlighterManager.Instance.Highlighters[this.SyntaxLanguage];

            var dc = this.renderCanvas.GetContext();
            var dc2 = this.lineNumbersCanvas.GetContext();
            for (int i = 0; i < this.blocks.Count; i++)
            {
                InnerTextBlock block = this.blocks[i];
                Point blockPos = block.Position;
                double top = blockPos.Y - this.VerticalOffset;
                double bottom = top + this.blockHeight;
                if (top < this.ActualHeight && bottom > 0)
                {
                    try
                    {
                        dc.DrawText(block.FormattedText, new Point(2 - this.HorizontalOffset, block.Position.Y - this.VerticalOffset));
                        if (this.IsLineNumbersMarginVisible)
                        {
                            this.lineNumbersCanvas.Width = this.GetFormattedTextWidth(string.Format("{0:0000}", this.totalLineCount)) + 5;
                            dc2.DrawText(block.LineNumbers, new Point(this.lineNumbersCanvas.ActualWidth, 1 + block.Position.Y - this.VerticalOffset));
                        }
                    }
                    catch
                    {
                        // Don't know why this exception is raised sometimes.
                        // Reproduce steps:
                        // - Sets a valid syntax highlighter on the box.
                        // - Copy a large chunk of code in the clipboard.
                        // - Paste it using ctrl+v and keep these buttons pressed.
                    }
                }
            }

            dc.Close();
            dc2.Close();
        }

        // -----------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------

        /// <summary>
        /// Returns the index of the first visible text line.
        /// </summary>
        public int GetIndexOfFirstVisibleLine()
        {
            int guessedLine = (int)(this.VerticalOffset / this.lineHeight);
            return guessedLine > this.totalLineCount ? this.totalLineCount : guessedLine;
        }

        /// <summary>
        /// Returns the index of the last visible text line.
        /// </summary>
        public int GetIndexOfLastVisibleLine()
        {
            double height = this.VerticalOffset + this.ViewportHeight;
            int guessedLine = (int)(height / this.lineHeight);
            return guessedLine > this.totalLineCount - 1 ? this.totalLineCount - 1 : guessedLine;
        }

        /// <summary>
        /// Formats and Highlights the text of a block.
        /// </summary>
        private void FormatBlock(InnerTextBlock currentBlock, InnerTextBlock previousBlock)
        {
            currentBlock.FormattedText = this.GetFormattedText(currentBlock.RawText);
            ThreadPool.QueueUserWorkItem(p =>
            {
                int previousCode = previousBlock != null ? previousBlock.Code : -1;
                currentBlock.Code = this.CurrentHighlighter.Highlight(currentBlock.FormattedText, previousCode);
            });
        }

        /// <summary>
        /// Returns a formatted text object from the given string.
        /// </summary>
        private FormattedText GetFormattedText(string text)
        {
            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch),
                this.FontSize,
                Brushes.Black);

            ft.Trimming = TextTrimming.None;
            ft.LineHeight = this.lineHeight;

            return ft;
        }

        /// <summary>
        /// Returns a string containing a list of numbers separated with newlines.
        /// </summary>
        private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex)
        {
            string text = "";
            for (int i = firstIndex + 1; i <= lastIndex + 1; i++)
            {
                text += i.ToString() + "\n";
            }

            text = text.Trim();

            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch),
                this.FontSize,
                new SolidColorBrush(Color.FromRgb(0x21, 0xA1, 0xD8)))
            {
                Trimming = TextTrimming.None,
                LineHeight = this.lineHeight,
                TextAlignment = TextAlignment.Right
            };

            return ft;
        }

        /// <summary>
        /// Returns the width of a text once formatted.
        /// </summary>
        private double GetFormattedTextWidth(string text)
        {
            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch),
                this.FontSize,
                Brushes.Black);

            ft.Trimming = TextTrimming.None;
            ft.LineHeight = this.lineHeight;

            return ft.Width;
        }

        // -----------------------------------------------------------
        // Dependency Properties
        // -----------------------------------------------------------

        public static readonly DependencyProperty IsLineNumbersMarginVisibleProperty = DependencyProperty.Register(
            "IsLineNumbersMarginVisible", typeof(bool), typeof(SyntaxHighlightBox), new PropertyMetadata(true));

        public bool IsLineNumbersMarginVisible
        {
            get { return (bool)this.GetValue(IsLineNumbersMarginVisibleProperty); }
            set { this.SetValue(IsLineNumbersMarginVisibleProperty, value); }
        }

        public static readonly DependencyProperty SyntaxLanguageProperty = DependencyProperty.Register(
            "SyntaxLanguage", typeof(string), typeof(SyntaxHighlightBox), new PropertyMetadata("csharp"));

        public string SyntaxLanguage
        {
            get { return (string)this.GetValue(SyntaxLanguageProperty); }
            set { this.SetValue(SyntaxLanguageProperty, value); }
        }

        // -----------------------------------------------------------
        // Classes
        // -----------------------------------------------------------

        private class InnerTextBlock
        {
            public string RawText { get; set; }
            public FormattedText FormattedText { get; set; }
            public FormattedText LineNumbers { get; set; }
            public int CharStartIndex { get; private set; }
            public int CharEndIndex { get; private set; }
            public int LineStartIndex { get; private set; }
            public int LineEndIndex { get; private set; }
            public Point Position { get { return new Point(0, this.LineStartIndex * this.lineHeight); } }
            public bool IsLast { get; set; }
            public int Code { get; set; }

            private double lineHeight;

            public InnerTextBlock(int charStart, int charEnd, int lineStart, int lineEnd, double lineHeight)
            {
                this.CharStartIndex = charStart;
                this.CharEndIndex = charEnd;
                this.LineStartIndex = lineStart;
                this.LineEndIndex = lineEnd;
                this.lineHeight = lineHeight;
                this.IsLast = false;

            }

            public string GetSubString(string text)
            {
                return text.Substring(this.CharStartIndex, this.CharEndIndex - this.CharStartIndex + 1);
            }

            public override string ToString()
            {
                return string.Format("L:{0}/{1} C:{2}/{3} {4}",
                    this.LineStartIndex,
                    this.LineEndIndex,
                    this.CharStartIndex,
                    this.CharEndIndex,
                    this.FormattedText.Text);
            }
        }
    }
}
