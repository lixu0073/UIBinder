namespace GuiSolution.ScrollView
{
    using System;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.Serialization;

    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class ScrollViewEx : ScrollView
    {
        [SerializeField]
        [FormerlySerializedAs("m_pageSize")]
        private int pageSize = 50;

        private int startOffset = 0;

        private Func<int> realItemCountFunc;

        private Vector2 lastPosition;

        private bool reloadFlag = false;

        private bool suppressValueChanged; //避免onvaluechang重入

        private int realDataCount = 0;

        private int cachedRealDataCount = 0;

        private Func<int> cachedPageCountFunc;

        private Action<int, RectTransform> sourceUpdateFunc;
        private Func<int, Vector2> sourceItemSizeFunc;
        private bool useCachedRealDataCountOnce;

        #region 绑定接口重写

        public override void SetUpdateFunc(Action<int, RectTransform> func)
        {
            this.sourceUpdateFunc = func;
            base.SetUpdateFunc(func == null ? null : this.UpdateItemWithOffset);
        }

        public override void SetItemSizeFunc(Func<int, Vector2> func)
        {
            this.sourceItemSizeFunc = func;
            base.SetItemSizeFunc(func == null ? null : this.GetItemSizeWithOffset);
        }

        public override void SetItemCountFunc(Func<int> func)
        {
            this.realItemCountFunc = func;

            if (func != null)
            {
                base.SetItemCountFunc(this.GetCachedPageCountFunc());
            } else
            {
                this.startOffset = 0;
                this.realDataCount = 0;
                this.cachedRealDataCount = 0;
                base.SetItemCountFunc(null);
            }
        }

        private void UpdateItemWithOffset(int index, RectTransform rect)
        {
            this.sourceUpdateFunc?.Invoke(index + this.startOffset, rect);
        }

        private Vector2 GetItemSizeWithOffset(int index)
        {
            return this.sourceItemSizeFunc != null
                ? this.sourceItemSizeFunc(index + this.startOffset)
                : this.defaultItemSize;
        }

        private int GetPageItemCount()
        {
            return Mathf.Max(0, Mathf.Min(this.cachedRealDataCount, this.pageSize));
        }

        private Func<int> GetCachedPageCountFunc()
        {
            if (this.cachedPageCountFunc == null)
            {
                this.cachedPageCountFunc = this.GetPageItemCount;
            }

            return this.cachedPageCountFunc;
        }

        #endregion

        #region Unity 生命周期与输入

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            this.pageSize = Mathf.Max(1, this.pageSize);
        }
#endif

        public override void OnDrag(PointerEventData eventData)
        {
            if (this.reloadFlag)
            {
                this.reloadFlag = false;
                this.OnEndDrag(eventData);
                this.OnBeginDrag(eventData);

                return;
            }

            base.OnDrag(eventData);
        }

        protected override void Awake()
        {
            base.Awake();

            this.lastPosition = this.normalizedPosition;
            this.onValueChanged.AddListener(this.OnValueChanged);
        }

        protected override void OnDestroy()
        {
            this.onValueChanged.RemoveListener(this.OnValueChanged);
            this.realItemCountFunc = null;
            this.cachedPageCountFunc = null;
            this.sourceUpdateFunc = null;
            this.sourceItemSizeFunc = null;
            base.OnDestroy();
        }


        #endregion

        #region ScrollTo 与数据数量

        protected override void InternalScrollTo(int index)
        {
            var count = this.GetRealDataCountSafe();
            this.cachedRealDataCount = count;

            if (count <= 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, count - 1);

            var pageCount = Mathf.Min(count, this.pageSize);
            this.startOffset = Mathf.Clamp(index - this.pageSize / 2, 0, Mathf.Max(count - pageCount, 0));

            this.reloadFlag = false;
            this.suppressValueChanged = true;

            try
            {
                // ScrollTo 是程序化跳转，不能让 onValueChanged 触发分页逻辑。
                // 否则 startOffset 会在 base.InternalScrollTo 设置 content 位置时再次变化，导致整页错位。
                this.useCachedRealDataCountOnce = true;
                this.UpdateData(true);
                base.InternalScrollTo(index - this.startOffset);
            } finally
            {
                this.suppressValueChanged = false;
                this.lastPosition = this.normalizedPosition;
                this.velocity = Vector2.zero;
            }
        }

        protected override void CheckDataCountChange()
        {
            if (this.useCachedRealDataCountOnce)
            {
                this.useCachedRealDataCountOnce = false;
            } else
            {
                this.cachedRealDataCount = this.GetRealDataCountSafe();
            }

            if (this.cachedRealDataCount < this.realDataCount)
            {
                this.startOffset = Mathf.Clamp(this.startOffset, 0, Mathf.Max(this.cachedRealDataCount - this.pageSize, 0));
            }

            this.realDataCount = this.cachedRealDataCount;

            base.CheckDataCountChange();
        }

        #endregion

        #region 分页切换

        private void OnValueChanged(Vector2 position)
        {
            if (this.content == null)
            {
                return;
            }

            if (this.suppressValueChanged)
            {
                this.lastPosition = position;
                return;
            }

            if (this.IsElasticOutOfBounds())
            {
                this.lastPosition = position;
                return;
            }

            Vector2 delta = position - this.lastPosition;
            this.lastPosition = position;

            // 判断是否触发分页切换，并计算用于视觉锚定的 pin 元素
            if (!this.TryGetPageTurnInfo(delta, out var backward, out var pin))
            {
                return;
            }

            // 每次翻半页，保留部分重叠数据
            // [0~49] -> [25~74] -> [50~99]
            var step = Mathf.Max(1, this.pageSize / 2);

            var oldStartOffset = this.startOffset;

            var realDataCount = this.GetRealDataCountSafe();
            this.cachedRealDataCount = realDataCount;

            // 数据量不足一页时无需分页
            if (realDataCount <= this.pageSize)
            {
                return;
            }

            // backward:
            // true  = 加载后面的数据
            // false = 加载前面的数据
            var dataCount = this.GetSafeItemCount();
            if (dataCount <= 0)
            {
                return;
            }

            int maxStartOffset = Mathf.Max(realDataCount - this.pageSize, 0);

            int targetStartOffset = Mathf.Clamp(
                this.startOffset + (backward ? step : -step),
                0,
                maxStartOffset);

            // 分页偏移没有变化，说明已经到真实数据边界
            if (targetStartOffset == this.startOffset)
            {
                this.StopScrollMovement();
                return;
            }

            pin = Mathf.Clamp(pin, 0, dataCount - 1);

            Rect oldRect = this.GetItemLocalRect(pin);
            Vector2 oldWorld = this.content.TransformPoint(oldRect.position);

            this.startOffset = targetStartOffset;

            // 翻页后 pin 对应的新索引
            //
            // 例如:
            //
            // oldStartOffset = 100
            // startOffset    = 125
            // pin            = 10
            //
            // 原来显示:
            // data[110]
            //
            // 翻页后:
            // data[110] 应该仍然保持在同一个屏幕位置
            var newPin = pin + oldStartOffset - this.startOffset;
            newPin = Mathf.Clamp(newPin, 0, dataCount - 1);

            // 保存惯性速度
            Vector2 oldVelocity = this.velocity;

            this.useCachedRealDataCountOnce = true;
            this.RebuildLayoutCacheOnly();
            dataCount = this.GetSafeItemCount();
            if (dataCount <= 0)
            {
                return;
            }

            newPin = Mathf.Clamp(newPin, 0, dataCount - 1);
            this.reloadFlag = true;

            Rect newRect = this.GetItemLocalRect(newPin);
            Vector2 newWorld = this.content.TransformPoint(newRect.position);

            Vector2 deltaWorld = newWorld - oldWorld;

            Vector2 deltaLocal = this.content.InverseTransformVector(deltaWorld);

            this.suppressValueChanged = true;

            try
            {
                // 先修正 Content 位置，再刷新数据，避免整页重绑时闪一下
                this.SetContentAnchoredPosition(this.content.anchoredPosition - deltaLocal);

                this.RefreshVisibleItemsImmediately();
            } finally
            {
                this.suppressValueChanged = false;
                this.lastPosition = this.normalizedPosition;
                this.velocity = oldVelocity;
            }
        }

        private bool TryGetPageTurnInfo(Vector2 delta, out bool backward, out int pin)
        {
            backward = false;
            pin = 0;

            var vertical = (((int)this.layoutType & flagScrollDirection) == 1);

            if (vertical)
            {
                if (delta.y < 0f)
                {
                    // 向上滚，数据向后翻页
                    var toShow = this.criticalItemIndex[CriticalItemType.DownToShow];
                    var critical = this.pageSize - 1;

                    if (toShow < critical)
                    {
                        return false;
                    }

                    pin = Mathf.Max(0, critical - 1);
                    backward = true;
                    return true;
                }

                if (delta.y > 0f)
                {
                    // 向下滚，数据向前翻页
                    var toShow = this.criticalItemIndex[CriticalItemType.UpToShow];

                    if (toShow > 0)
                    {
                        return false;
                    }

                    pin = Mathf.Min(this.pageSize - 1, 1);
                    backward = false;
                    return true;
                }

                return false;
            }

            if (delta.x < 0f)
            {
                // 向左滚，数据向后翻页
                var toShow = this.criticalItemIndex[CriticalItemType.DownToShow];
                var critical = this.pageSize - 1;

                if (toShow < critical)
                {
                    return false;
                }

                pin = Mathf.Max(0, critical - 1);
                backward = true;
                return true;
            }

            if (delta.x > 0f)
            {
                // 向右滚，数据向前翻页
                var toShow = this.criticalItemIndex[CriticalItemType.UpToShow];

                if (toShow > 0)
                {
                    return false;
                }

                pin = Mathf.Min(this.pageSize - 1, 1);
                backward = false;
                return true;
            }

            return false;
        }

        #endregion

        #region 安全回调

        private int GetSafeItemCount()
        {
            if (this.itemCountFunc == null)
            {
                return 0;
            }

            try
            {
                return this.itemCountFunc();
            } catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return 0;
            }
        }

        private int GetRealDataCountSafe()
        {
            if (this.realItemCountFunc == null)
            {
                return 0;
            }

            try
            {
                return Mathf.Max(0, this.realItemCountFunc());
            } catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return 0;
            }
        }

        #endregion
    }

}
