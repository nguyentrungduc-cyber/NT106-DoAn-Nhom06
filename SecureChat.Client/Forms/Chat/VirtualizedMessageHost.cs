using SecureChat.Client;
using SecureChat.Client.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Chat
{
    /// <summary>
    /// Mutable state object stored in each pooled Panel's Tag.
    /// The Paint lambda reads all message-specific data from this object,
    /// allowing a single Panel to be rebound to different messages without recreation.
    /// </summary>
    internal class BubbleState
    {
        public string? MessageType; // "text" | "recalled" | "voice" | "file"
        public string? MessageId;
        public bool IsOut;
        public bool IsGroup;
        public string? Sender;

        // --- Text & Recalled fields ---
        public string? ActualText;
        public string? Time;
        public int Bw, Bh;
        public int LeftOffset;
        public int Pad = 12;
        public int MaxW;
        public int TextWidth;
        public int TextHeight;
        public string? ReplySender;
        public string? ReplyText;
        public string? ForwardSender;
        public string? ForwardDisplayName;
        public bool ForwardNameWraps;
        public int ForwardHeaderHeight;
        public float ForwardPrefixWidth;
        public float ForwardPrefixLineH;
        public float ForwardNameMeasuredW;
        public float ForwardNameMeasuredH;
        public int SenderHeight;
        public int ReplyBlockHeight;
        public bool HasExpiryTimer;

        // --- Recalled ---
        public int RecallBw, RecallBh;
        public string? RecallText;
        public int RecallMaxW;
        public int RecallLeft;

        // --- Voice ---
        public string? VoiceUrl;
        public string? VoiceFileName;
        public string? VoiceDuration;
        public string? VoiceExpectedSha256;

        // --- File ---
        public string? FileUrl;
        public string? FileName;
        public string? FileSize;
        public string? FileExpectedSha256;

    }

    /// <summary>
    /// Virtualized container that keeps ~35 Panel controls and recycles them
    /// for any number of messages. Preserves wallpaper rendering
    /// and WndProc instrumentation.
    /// </summary>
    public class VirtualizedMessageHost : ChatPanel
    {
        private const int PoolCapacity = 35;
        private const int OverScanCount = 3;

        // ── Virtual item list ──────────────────────────────────────────
        public struct VirtualItem
        {
            public string Id; // msgId for messages, "date::yyyy-MM-dd" for separators
            public int Height;
            public int Y;
        }

        private List<VirtualItem> _virtualItems = new();
        private int _totalContentHeight;

        // ── Pool ───────────────────────────────────────────────────────
        private readonly Stack<Panel> _freePool = new(PoolCapacity);
        private readonly List<(Panel Ctl, string ItemId)> _activeSlots = new(PoolCapacity);

        // ── Callbacks set by frmMainChat ──────────────────────────────
        /// <summary>
        /// Creates a new Panel skeleton (the Paint handler reads from BubbleState in Tag).
        /// Called when the pool is empty and a new control is needed.
        /// </summary>
        public Func<Panel> CreatePanelSkeleton;

        /// <summary>
        /// Resets a Panel for reuse: clears children, resets Tag, removes context menu.
        /// </summary>
        public Action<Panel> ResetPanelSkeleton;

        /// <summary>
        /// Binds a panel to a message virtual item.
        /// panel.Tag is a BubbleState; implementor fills it with message data.
        /// </summary>
        public Action<Panel, int, VirtualItem> BindMessageToPanel;

        /// <summary>
        /// Binds a panel to a date separator.
        /// </summary>
        public Action<Panel, string, VirtualItem> BindDateSeparatorToPanel;

        // ── Construction ──────────────────────────────────────────────
        public VirtualizedMessageHost()
        {
            AutoScroll = true;
            Padding = new Padding(12, 8, 12, 8);
        }

        public IReadOnlyList<VirtualItem> VirtualItems => _virtualItems;
        public int VirtualItemCount => _virtualItems.Count;
        public int TotalContentHeight => _totalContentHeight;

        // ── Public API ────────────────────────────────────────────────
        public void BuildVirtualList(List<VirtualItem> items)
        {
            foreach (var (ctl, _) in _activeSlots)
                ReturnToPool(ctl);
            _activeSlots.Clear();
            _virtualItems = items;

            _totalContentHeight = items.Count > 0
                ? items[^1].Y + items[^1].Height + 16
                : 0;
            AutoScrollMinSize = new Size(0, _totalContentHeight);
            AutoScrollPosition = Point.Empty;

            UpdateVisibleRange();
        }

        public void AppendVirtualItem(VirtualItem item)
        {
            _virtualItems.Add(item);
            if (_virtualItems.Count > 0)
                _totalContentHeight = _virtualItems[^1].Y + _virtualItems[^1].Height + 16;
            AutoScrollMinSize = new Size(0, _totalContentHeight);
            UpdateVisibleRange();
        }

        public void RemoveVirtualItem(string itemId)
        {
            int idx = _virtualItems.FindIndex(v => v.Id == itemId);
            if (idx < 0) return;

            // Remove active slot if visible
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                if (_activeSlots[i].ItemId == itemId)
                {
                    ReturnToPool(_activeSlots[i].Ctl);
                    _activeSlots.RemoveAt(i);
                    break;
                }
            }

            _virtualItems.RemoveAt(idx);

            // Recalculate Y positions for items after the removed one
            RecalcVirtualPositions();

            _totalContentHeight = _virtualItems.Count > 0
                ? _virtualItems[^1].Y + _virtualItems[^1].Height + 16
                : 0;
            AutoScrollMinSize = new Size(0, _totalContentHeight);
            UpdateVisibleRange();
        }

        public void ReplaceVirtualItem(string oldId, VirtualItem newItem)
        {
            int idx = _virtualItems.FindIndex(v => v.Id == oldId);
            if (idx < 0)
            {
                // Not found — could be hidden message added on replace
                return;
            }

            // Remove old visible slot
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                if (_activeSlots[i].ItemId == oldId)
                {
                    ReturnToPool(_activeSlots[i].Ctl);
                    _activeSlots.RemoveAt(i);
                    break;
                }
            }

            _virtualItems[idx] = newItem;

            // Recalc Y for items after this position
            for (int i = idx + 1; i < _virtualItems.Count; i++)
            {
                _virtualItems[i] = new VirtualItem
                {
                    Id = _virtualItems[i].Id,
                    Height = _virtualItems[i].Height,
                    Y = _virtualItems[i - 1].Y + _virtualItems[i - 1].Height + 4
                };
            }

            _totalContentHeight = _virtualItems.Count > 0
                ? _virtualItems[^1].Y + _virtualItems[^1].Height + 16
                : 0;
            AutoScrollMinSize = new Size(0, _totalContentHeight);
            UpdateVisibleRange();
        }

        public void InvalidateItem(string itemId)
        {
            foreach (var (ctl, id) in _activeSlots)
            {
                if (id == itemId)
                {
                    ctl.Invalidate();
                    return;
                }
            }
        }

        public Panel FindActivePanel(string itemId)
        {
            foreach (var (ctl, id) in _activeSlots)
                if (id == itemId) return ctl;
            return null;
        }

        public bool IsItemVisible(string itemId)
        {
            foreach (var (_, id) in _activeSlots)
                if (id == itemId) return true;
            return false;
        }

        public void ScrollToItem(string itemId)
        {
            int idx = _virtualItems.FindIndex(v => v.Id == itemId);
            if (idx < 0) return;
            int targetY = _virtualItems[idx].Y;
            // Scroll so the item is at the top with some padding
            int scrollOffset = targetY - Padding.Top;
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, _totalContentHeight - ClientSize.Height));
            AutoScrollPosition = new Point(0, scrollOffset);
            UpdateVisibleRange();
        }

        public void ScrollToBottom()
        {
            if (_virtualItems.Count == 0) return;
            AutoScrollPosition = new Point(0, _totalContentHeight - ClientSize.Height + Padding.Bottom);
            UpdateVisibleRange();
        }

        // ── Scroll handling ───────────────────────────────────────────
        protected override void WndProc(ref Message m)
        {
            const int WM_VSCROLL = 0x0115;
            const int WM_MOUSEWHEEL = 0x020A;

            base.WndProc(ref m);

            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                UpdateVisibleRange();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Re-bind visible controls with new width
            UpdateVisibleRange();
        }

        // ── Viewport calculation ──────────────────────────────────────
        public void UpdateVisibleRange()
        {
            if (_virtualItems.Count == 0) return;

            int clientW = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            int scrollY = -DisplayRectangle.Y; // positive when scrolled down
            int viewTop = scrollY - (OverScanCount * 120); // rough over-scan
            int viewBottom = scrollY + ClientSize.Height + (OverScanCount * 120);

            // Binary search for first visible
            int firstVisible = VirtualItemIndexAtY(viewTop);
            int lastVisible = VirtualItemIndexAtY(viewBottom);

            // Expand to include items fully or partially visible within over-scan
            firstVisible = Math.Max(0, firstVisible);
            lastVisible = Math.Min(_virtualItems.Count - 1, lastVisible + 1);

            // Guard: if no items in range, recycle all
            if (firstVisible > lastVisible)
            {
                foreach (var (ctl, _) in _activeSlots.ToList())
                {
                    ReturnToPool(ctl);
                }
                _activeSlots.Clear();
                return;
            }

            // Recycle controls that are now outside visible range
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                var (ctl, id) = _activeSlots[i];
                int vi = _virtualItems.FindIndex(v => v.Id == id);
                if (vi < firstVisible || vi > lastVisible)
                {
                    ReturnToPool(ctl);
                    _activeSlots.RemoveAt(i);
                }
            }

            // Acquire controls for items that are now visible but have no slot
            for (int vi = firstVisible; vi <= lastVisible && vi < _virtualItems.Count; vi++)
            {
                var item = _virtualItems[vi];
                if (_activeSlots.Any(a => a.ItemId == item.Id))
                    continue;

                Panel? p = AcquireFromPool();
                if (p == null) continue;

                // Position and size
                p.Location = new Point(0, item.Y);
                p.Width = clientW;
                p.Height = item.Height;
                p.Visible = true;

                if (!Controls.Contains(p))
                    Controls.Add(p);

                // Bind
                if (item.Id.StartsWith("date::"))
                    BindDateSeparatorToPanel?.Invoke(p, item.Id, item);
                else
                    BindMessageToPanel?.Invoke(p, vi, item);

                _activeSlots.Add((p, item.Id));
            }
        }

        // ── Pool management ───────────────────────────────────────────
        private Panel? AcquireFromPool()
        {
            if (_freePool.Count > 0)
                return _freePool.Pop();
            var newPanel = CreatePanelSkeleton?.Invoke();
            if (newPanel == null) return null;
            return newPanel;
        }

        private void ReturnToPool(Panel p)
        {
            p.Visible = false;
            ResetPanelSkeleton?.Invoke(p);
            if (_freePool.Count < PoolCapacity && !_freePool.Contains(p))
                _freePool.Push(p);
            else
            {
                Controls.Remove(p);
                p.Dispose();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private int VirtualItemIndexAtY(int y)
        {
            if (_virtualItems.Count == 0) return 0;
            if (y <= _virtualItems[0].Y) return 0;

            int lo = 0, hi = _virtualItems.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_virtualItems[mid].Y <= y)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }

        private void RecalcVirtualPositions()
        {
            if (_virtualItems.Count == 0) return;
            int y = _virtualItems[0].Y;
            // Calculate Y from scratch based on item before
            // Actually we just rebuild Y sequentially:
            int runningY = 8;
            var newList = new List<VirtualItem>(_virtualItems.Count);
            foreach (var item in _virtualItems)
            {
                newList.Add(new VirtualItem
                {
                    Id = item.Id,
                    Height = item.Height,
                    Y = runningY
                });
                runningY += item.Height + 4;
            }
            _virtualItems = newList;
            _totalContentHeight = runningY + 8;
            AutoScrollMinSize = new Size(0, _totalContentHeight);
        }
    }
}
