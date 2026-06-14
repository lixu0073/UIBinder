namespace GuiSolution.ScrollView
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.UI;

    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class ScrollView : ScrollRect
    {
        [Tooltip("默认item尺寸")]
        public Vector2 defaultItemSize;

        [Tooltip("item的模板")]
        public RectTransform itemTemplate;

        // 0001
        protected const int flagScrollDirection = 1;

        [SerializeField]
        [FormerlySerializedAs("m_layoutType")]
        protected ItemLayoutType layoutType = ItemLayoutType.Vertical;

        // 只保存4个临界index
        protected int[] criticalItemIndex = new int[4];

        // callbacks for items
        protected Action<int, RectTransform> updateFunc;
        protected Func<int, Vector2> itemSizeFunc;
        protected Func<int> itemCountFunc;
        protected Func<int, RectTransform> itemGetFunc;
        protected Action<RectTransform> itemRecycleFunc;

        private readonly List<ScrollItemWithRect> managedItems = new List<ScrollItemWithRect>();
        private readonly List<int> activeItemIndexes = new List<int>(64);

        private Rect refRect;
        private Vector2 lastRefRectSize;
        private bool hasRefRect;

        // resource management
        private ScrollItemPool itemPool = null;
        private Transform poolRoot;

        private int dataCount = 0;

        [Tooltip("初始化时池内item数量")]
        [SerializeField]
        private int poolSize;

        [Header("缓存释放")]
        [Tooltip("Disable 时是否回收当前可见 Item。窗口会频繁隐藏/显示时建议关闭，长列表临时界面可以开启。")]
        [SerializeField]
        private bool recycleVisibleItemsOnDisable = false;

        [Tooltip("数据量大幅缩小时是否释放多余的 item 元数据缓存。")]
        [SerializeField]
        private bool trimManagedItemCacheOnShrink = true;

        [Tooltip("保留多少个多余元数据节点，避免数据量轻微波动时反复分配。")]
        [SerializeField]
        private int trimManagedItemCacheExtra = 256;

        // status
        private bool initialized = false;
        private int willUpdateData = 0;

        private static readonly Vector3[] viewWorldCorners = new Vector3[4];
        private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        private readonly Vector3[] rectCorners = new Vector3[2];
        private Rect visibleRectCache;

        #region 自定义部分，不使用scrollrect的惯性和弹性

        [Header("滚动反向")]
        [SerializeField]
        private bool reverseScrollDirection = false;
        public enum InertiaMode
        {
            Native,
            Custom,
        }

        [Header("惯性模式")]
        [SerializeField]
        private InertiaMode inertiaMode = InertiaMode.Custom;

        [Header("自定义惯性")]
        [SerializeField]
        private float customDeceleration = 8f;

        [SerializeField]
        private float customStopVelocity = 40f; // 低于该速度时停止惯性

        [SerializeField]
        private float customMaxVelocity = 4000f;

        [SerializeField]
        private float customSlowVelocity = 120f;

        [SerializeField]
        private float customSlowDamping = 0.65f;

        [SerializeField]
        private bool customPixelSnap = true;

        [Header("自定义弹性")]
        [SerializeField]
        private float customElasticity = 0.135f;

        [Header("输入灵敏度")]
        [SerializeField]
        private float dragSensitivity = 1f;
        [SerializeField]
        private float dragElasticLimit = 0.3f;

        [SerializeField]
        private float wheelSensitivity = 50f;

        [SerializeField]
        private float wheelInertiaMultiplier = 8f;

        private bool isDraggingByUser;
        // 当前自定义滚动速度。
        // 拖拽时由 UpdateCustomDragVelocity 采样，滚轮时由 OnScroll 累加，LateUpdate 中持续衰减
        private Vector2 customVelocity;
        private Vector2 lastDragPosition;
        private Canvas rootCanvas;
        private bool criticalItemsDirty;
        private bool suppressNormalizedPositionRefresh;
        private Vector2 lastInvokedNormalizedPosition;
        #endregion

        // for hide and show
        public enum ItemLayoutType
        {
            // 最后一位表示滚动方向
            Vertical = 0b0001,                   // 0001
            Horizontal = 0b0010,                 // 0010
            VerticalThenHorizontal = 0b0100,     // 0100
            HorizontalThenVertical = 0b0101,     // 0101
        }

        #region 配置接口

        public virtual void SetUpdateFunc(Action<int, RectTransform> func)
        {
            this.updateFunc = func;
        }

        public virtual void SetItemSizeFunc(Func<int, Vector2> func)
        {
            this.itemSizeFunc = func;
        }

        public virtual void SetItemCountFunc(Func<int> func)
        {
            this.itemCountFunc = func;
        }

        public void SetItemGetAndRecycleFunc(Func<int, RectTransform> getFunc, Action<RectTransform> recycleFunc)
        {
            if (getFunc != null && recycleFunc != null)
            {
                this.itemGetFunc = getFunc;
                this.itemRecycleFunc = recycleFunc;
            } else
            {
                this.itemGetFunc = null;
                this.itemRecycleFunc = null;
            }
        }

        #endregion

        #region Unity 生命周期 - 初始化

        protected override void Awake()
        {
            base.Awake();

            this.rootCanvas = this.GetComponentInParent<Canvas>();

            if (this.inertiaMode == InertiaMode.Custom)
            {
                this.inertia = false;
                this.velocity = Vector2.zero;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (this.inertiaMode == InertiaMode.Custom)
            {
                this.inertia = false;
            }

            this.customDeceleration = Mathf.Max(0.01f, this.customDeceleration);
            this.customStopVelocity = Mathf.Max(0f, this.customStopVelocity);
            this.customMaxVelocity = Mathf.Max(1f, this.customMaxVelocity);
            this.customSlowVelocity = Mathf.Max(0f, this.customSlowVelocity);
            this.customSlowDamping = Mathf.Clamp01(this.customSlowDamping);
            this.customElasticity = Mathf.Max(0.001f, this.customElasticity);
            this.dragElasticLimit = Mathf.Clamp01(this.dragElasticLimit);
            this.dragSensitivity = Mathf.Max(0.01f, this.dragSensitivity);
            this.wheelSensitivity = Mathf.Max(0.01f, this.wheelSensitivity);
            this.wheelInertiaMultiplier = Mathf.Max(0f, this.wheelInertiaMultiplier);
            this.trimManagedItemCacheExtra = Mathf.Max(0, this.trimManagedItemCacheExtra);
        }
#endif

        #endregion

        #region 输入与滚动驱动

        public override void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            this.isDraggingByUser = true;
            this.customVelocity = Vector2.zero;

            if (this.content != null)
            {
                this.lastDragPosition = this.content.anchoredPosition;
            }

            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (this.inertiaMode == InertiaMode.Native)
            {
                base.OnDrag(eventData);
                this.FlushCriticalItems(); //刷新item
                return;
            }

            if (this.content == null)
            {
                return;
            }

            Vector2 oldPosition = this.content.anchoredPosition;

            base.OnDrag(eventData);

            if (!Mathf.Approximately(this.dragSensitivity, 1f))
            {
                Vector2 currentPosition = this.content.anchoredPosition;
                Vector2 dragDelta = currentPosition - oldPosition;
                this.SetContentAnchoredPosition(oldPosition + dragDelta * this.dragSensitivity);
            }

            if (this.movementType == MovementType.Elastic)
            {
                this.LimitDragElastic();
            }

            Vector2 finalPosition = this.content.anchoredPosition;

            // 采样拖拽速度，供 OnEndDrag 后的 LateUpdate 惯性滚动使用
            this.UpdateCustomDragVelocity(finalPosition);

            this.FlushCriticalItems(); //刷新item
        }

        // 限制拖拽时的 Elastic 越界范围
        private void LimitDragElastic()
        {
            Vector2 offset = this.CalculateContentOffset(Vector2.zero);

            if (offset == Vector2.zero)
            {
                return;
            }

            Rect viewRect = this.viewport != null
                ? this.viewport.rect
                : ((RectTransform)this.transform).rect;

            Vector2 position = this.content.anchoredPosition;

            if (this.vertical && Mathf.Abs(offset.y) > 0.001f)
            {
                var maxOver = viewRect.height * this.dragElasticLimit;
                var over = Mathf.Clamp(-offset.y, -maxOver, maxOver);
                position.y += offset.y + over;
            }

            if (this.horizontal && Mathf.Abs(offset.x) > 0.001f)
            {
                var maxOver = viewRect.width * this.dragElasticLimit;
                var over = Mathf.Clamp(-offset.x, -maxOver, maxOver);
                position.x += offset.x + over;
            }

            this.SetContentAnchoredPosition(position);
        }

        // 根据本帧拖拽位移计算惯性速度，同时限制最大速度
        private void UpdateCustomDragVelocity(Vector2 currentPosition)
        {
            var dt = Time.unscaledDeltaTime;

            if (dt <= 0f)
            {
                return;
            }

            this.customVelocity = (currentPosition - this.lastDragPosition) / dt;
            this.customVelocity = Vector2.ClampMagnitude(this.customVelocity, this.customMaxVelocity);
            this.lastDragPosition = currentPosition;
        }

        public override void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnEndDrag(eventData);

            this.isDraggingByUser = false;

            if (this.inertiaMode == InertiaMode.Custom)
            {
                this.velocity = Vector2.zero;
                this.customVelocity = Vector2.ClampMagnitude(this.customVelocity, this.customMaxVelocity);
            }

            if (this.movementType == MovementType.Elastic)
            {
                Vector2 offset = this.CalculateContentOffset(Vector2.zero);
                if (offset != Vector2.zero)
                {
                    this.RemoveOutwardCustomVelocity(offset);
                }
            }
        }

        public override void OnScroll(UnityEngine.EventSystems.PointerEventData data)
        {
            if (!this.IsActive() || this.content == null)
            {
                return;
            } 

            if (this.inertiaMode == InertiaMode.Native)
            {
                base.OnScroll(data);
                this.FlushCriticalItems(); //刷新item
                return;
            }

            Vector2 delta = data.scrollDelta;

            var sensitivity = this.scrollSensitivity * this.wheelSensitivity;

            Vector2 move = Vector2.zero;

            var dir = this.reverseScrollDirection ? -1f : 1f;

            if (this.vertical)
            {
                move.y = -delta.y * sensitivity * dir;
            }

            if (this.horizontal)
            {
                var xDelta = delta.x != 0f ? delta.x : delta.y;
                move.x = xDelta * sensitivity * dir;
            }

            Vector2 position = this.content.anchoredPosition + move;

            if (this.movementType == MovementType.Elastic)
            {
                Vector2 offset = this.CalculateContentOffset(position - this.content.anchoredPosition);
                position += offset;

                Rect viewRect = this.viewport != null
                    ? this.viewport.rect
                    : ((RectTransform)this.transform).rect;

                if (this.vertical && Mathf.Abs(offset.y) > 0.001f)
                {
                    position.y -= RubberDelta(offset.y, viewRect.height);
                }

                if (this.horizontal && Mathf.Abs(offset.x) > 0.001f)
                {
                    position.x -= RubberDelta(offset.x, viewRect.width);
                }
            } else if (this.movementType == MovementType.Clamped)
            {
                Vector2 offset = this.CalculateContentOffset(position - this.content.anchoredPosition);
                position += offset;
            }

            if (this.customPixelSnap)
            {
                position = this.PixelSnapPosition(position);
            }

            this.SetContentAnchoredPosition(position);

            this.customVelocity += move * this.wheelInertiaMultiplier;
            this.customVelocity = Vector2.ClampMagnitude(this.customVelocity, this.customMaxVelocity);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (this.inertiaMode != InertiaMode.Custom)
            {
                this.FlushCriticalItems(); //刷新item
                return;
            }

            if (this.content == null || this.isDraggingByUser)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;

            if (dt <= 0f)
            {
                return;
            }

            Vector2 position = this.content.anchoredPosition;
            Vector2 offset = this.CalculateContentOffset(Vector2.zero);

            if (this.movementType == MovementType.Elastic && offset != Vector2.zero)
            {
                this.RemoveOutwardCustomVelocity(offset);

                for (var axis = 0; axis < 2; axis++)
                {
                    if (Mathf.Abs(offset[axis]) <= 0.001f)
                    {
                        continue;
                    }

                    var speed = this.customVelocity[axis];

                    position[axis] = Mathf.SmoothDamp(
                        this.content.anchoredPosition[axis],
                        this.content.anchoredPosition[axis] + offset[axis],
                        ref speed,
                        this.customElasticity,
                        Mathf.Infinity,
                        dt);

                    if (Mathf.Abs(offset[axis]) < 1f && Mathf.Abs(speed) < this.customStopVelocity)
                    {
                        position[axis] = this.content.anchoredPosition[axis] + offset[axis];
                        speed = 0f;
                    }

                    this.customVelocity[axis] = speed;
                }
            } else
            {
                var speed = this.customVelocity.magnitude;

                if (speed <= this.customStopVelocity)
                {
                    this.customVelocity = Vector2.zero;

                    if (this.customPixelSnap)
                    {
                        this.SnapContentToPixel();
                    }

                    this.FlushCriticalItems(); // 确保低速停止时也刷新item
                    return;
                }

                if (speed > this.customMaxVelocity)
                {
                    this.customVelocity = this.customVelocity.normalized * this.customMaxVelocity;
                }

                if (speed < this.customSlowVelocity)
                {
                    this.customVelocity *= this.customSlowDamping;
                }

                position += this.customVelocity * dt;

                if (this.movementType == MovementType.Clamped)
                {
                    Vector2 clampOffset = this.CalculateContentOffset(position - this.content.anchoredPosition);
                    position += clampOffset;
                }

                var damping = Mathf.Exp(-this.customDeceleration * dt);
                this.customVelocity *= damping;
            }

            if (this.customPixelSnap && !(this.movementType == MovementType.Elastic && offset != Vector2.zero))
            {
                position = this.PixelSnapPosition(position);
            }

            this.SetContentAnchoredPosition(position);
            this.FlushCriticalItems(); //刷新item
        }

        #endregion

        #region 自定义惯性与弹性辅助

        private void RemoveOutwardCustomVelocity(Vector2 offset)
        {
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(offset[axis]) <= 0.001f)
                {
                    continue;
                }

                float speed = this.customVelocity[axis];

                if (Mathf.Abs(speed) <= 0.001f)
                {
                    continue;
                }

                // offset 是回弹方向
                // speed 如果和 offset 反向，说明还在继续往越界方向冲
                if (Mathf.Sign(speed) != Mathf.Sign(offset[axis]))
                {
                    this.customVelocity[axis] = 0f;
                }
            }
        }

        private void FlushCriticalItems()
        {
            if (!this.criticalItemsDirty || this.willUpdateData != 0)
            {
                return;
            }

            var outOfBounds =
                this.movementType == MovementType.Elastic &&
                this.CalculateContentOffset(Vector2.zero).sqrMagnitude > 0.01f;

            this.criticalItemsDirty = false;

            if (outOfBounds)
            {
                // 越界回弹中：只补显示，不回收，避免整行被回收掉
                this.CheckAndShowItem(CriticalItemType.UpToShow);
                this.CheckAndShowItem(CriticalItemType.DownToShow);
            } else
            {
                // 正常范围：完整刷新，允许回收
                this.UpdateCriticalItems();
            }
        }

        // 计算content超出合法范围的偏移量
        private Vector2 CalculateContentOffset(Vector2 delta)
        {
            Vector2 offset = Vector2.zero;

            if (this.content == null)
            {
                return offset;
            }

            Rect viewRect = this.viewport != null
                ? this.viewport.rect
                : ((RectTransform)this.transform).rect;

            Rect contentRect = this.content.rect;
            Vector2 position = this.content.anchoredPosition + delta;

            if (this.vertical)
            {
                var contentHeight = contentRect.height;
                var viewHeight = viewRect.height;

                var minY = 0f;
                var maxY = Mathf.Max(0f, contentHeight - viewHeight);

                if (position.y < minY)
                {
                    offset.y = minY - position.y;
                } else if (position.y > maxY)
                {
                    offset.y = maxY - position.y;
                }
            }

            if (this.horizontal)
            {
                var contentWidth = contentRect.width;
                var viewWidth = viewRect.width;

                var minX = -Mathf.Max(0f, contentWidth - viewWidth);
                var maxX = 0f;

                if (position.x < minX)
                {
                    offset.x = minX - position.x;
                } else if (position.x > maxX)
                {
                    offset.x = maxX - position.x;
                }
            }

            return offset;
        }

        // 越界越远，阻力越大
        private static float RubberDelta(float overStretching, float viewSize)
        {
            if (viewSize <= 0f)
            {
                return 0f;
            }

            return (1f - (1f / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1f)))
                   * viewSize
                   * Mathf.Sign(overStretching);
        }

        // 半像素时对齐像素
        private Vector2 PixelSnapPosition(Vector2 position)
        {
            if (this.rootCanvas == null)
            {
                this.rootCanvas = this.GetComponentInParent<Canvas>();
            }

            var scale = this.rootCanvas != null ? this.rootCanvas.scaleFactor : 1f;

            if (scale <= 0f)
            {
                scale = 1f;
            }

            if (this.vertical)
            {
                position.y = Mathf.Round(position.y * scale) / scale;
            }

            if (this.horizontal)
            {
                position.x = Mathf.Round(position.x * scale) / scale;
            }

            return position;
        }

        private void SnapContentToPixel()
        {
            if (this.content == null)
            {
                return;
            }

            Vector2 pos = this.PixelSnapPosition(this.content.anchoredPosition);
            this.SetContentAnchoredPosition(pos);
        }

        protected bool IsElasticOutOfBounds(float sqrThreshold = 0.01f)
        {
            if (this.movementType != MovementType.Elastic || this.content == null)
            {
                return false;
            }

            return this.CalculateContentOffset(Vector2.zero).sqrMagnitude > sqrThreshold;
        }

        #endregion

        #region 对外接口与缓存释放

        public void ResetAllDelegates()
        {
            this.SetUpdateFunc(null);
            this.SetItemSizeFunc(null);
            this.SetItemCountFunc(null);
            this.SetItemGetAndRecycleFunc(null, null);
        }

        /// <summary>
        /// 主动释放运行时缓存
        /// 
        /// 适合临时界面关闭、长列表数据源切换、或者超大列表缩回小列表时调用
        /// </summary>
        public void ReleaseRuntimeCache(bool recycleVisibleItems = true)
        {
            if (recycleVisibleItems)
            {
                this.RecycleVisibleItems();
            }

            this.TrimManagedItemCacheTo(this.dataCount);

            if (this.itemPool != null)
            {
                this.itemPool.TrimCache(this.poolSize);
            }

            this.criticalItemsDirty = false;
            this.hasRefRect = false;
        }

        /// <summary>
        /// 手动压缩 item 元数据缓存
        /// </summary>
        public void TrimManagedItemCache()
        {
            this.TrimManagedItemCacheTo(this.dataCount);
        }

        #endregion

        #region 数据刷新入口

        public void UpdateData(bool immediately = true)
        {
            if (immediately)
            {
                this.willUpdateData |= 3; // 0011
                this.InternalUpdateData();
            } else
            {
                if (this.willUpdateData == 0 && this.IsActive())
                {
                    this.StartCoroutine(this.DelayUpdateData());
                }

                this.willUpdateData |= 3;
            }
        }

        public void UpdateDataIncrementally(bool immediately = true)
        {
            if (immediately)
            {
                this.willUpdateData |= 1; // 0001
                this.InternalUpdateData();
            } else
            {
                if (this.willUpdateData == 0 && this.IsActive())
                {
                    this.StartCoroutine(this.DelayUpdateData());
                }

                this.willUpdateData |= 1;
            }
        }

        #endregion

        #region ScrollTo 跳转入口

        public void ScrollTo(int index)
        {
            this.InternalScrollTo(index);
        }

        #endregion

        #region Unity 生命周期 - 启用禁用与尺寸变化

        protected override void OnEnable()
        {
            base.OnEnable();
            if (this.willUpdateData != 0)
            {
                this.StartCoroutine(this.DelayUpdateData());
            }
        }

        protected override void OnDisable()
        {
            if (this.recycleVisibleItemsOnDisable)
            {
                this.RecycleVisibleItems();
                this.willUpdateData |= 1;
            }

            this.initialized = false;
            this.hasRefRect = false;
            this.criticalItemsDirty = false;
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (!this.initialized || this.content == null || !this.IsActive())
            {
                return;
            }

            this.RefreshRefRectIfNeeded(true);
        }

        #endregion

        #region ScrollTo 跳转实现

        protected virtual void InternalScrollTo(int index)
        {
            if (!this.PrepareScrollTo(ref index))
            {
                return;
            }

            Rect itemRect = this.managedItems[index].rect;
            Vector2 targetPosition = this.CalculateContentPositionForScrollTo(itemRect);
            targetPosition = this.ClampContentPosition(targetPosition);

            if (this.customPixelSnap)
            {
                targetPosition = this.PixelSnapPosition(targetPosition);
            }

            this.StopScrollMovement();

            this.suppressNormalizedPositionRefresh = true;

            try
            {
                // 不再通过 SetNormalizedPosition 跳转。
                // ScrollRect 内部会基于 content bounds 反推 anchoredPosition，
                // 当 Content 尺寸、Pivot、Bounds 还没稳定时容易得到极端值。
                // 这里直接使用本列表自己的坐标体系：
                // 垂直列表 item 往负 Y 排列，content.y 应该等于 -item.yMax。
                // 水平列表 item 往正 X 排列，content.x 应该等于 -item.xMin。
                this.SetContentAnchoredPosition(targetPosition);
            } finally
            {
                this.suppressNormalizedPositionRefresh = false;
            }

            this.lastInvokedNormalizedPosition = this.normalizedPosition;
            this.ResetCriticalItems();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            this.ValidateScrollToResult(index, itemRect, targetPosition);
#endif
        }

        private bool PrepareScrollTo(ref int index)
        {
            if (this.content == null)
            {
                return false;
            }

            if (!this.initialized)
            {
                this.UpdateData(true);
            }

            if (this.dataCount <= 0 || this.content == null)
            {
                return false;
            }

            this.RefreshRefRectIfNeeded(false);

            if (index < 0)
            {
                index += this.dataCount;
            }

            index = Mathf.Clamp(index, 0, this.dataCount - 1);

            // ScrollTo 必须先确保最后一个 Item 的 Rect 已经计算过。
            // 否则 content.sizeDelta 可能只是“算到目标 index 为止”的临时尺寸，
            // 后续 ClampContentPosition 会基于错误尺寸裁剪，导致整体错位。
            this.EnsureItemRect(this.dataCount - 1);
            this.EnsureItemRect(index);

            return true;
        }

        private Vector2 CalculateContentPositionForScrollTo(Rect itemRect)
        {
            Vector2 position = this.content.anchoredPosition;
            var dir = (int)this.layoutType & flagScrollDirection;

            if (dir == 1)
            {
                // visibleRect.yMax = refRect.yMax - content.anchoredPosition.y
                // 让目标 item 的顶部对齐可视区域顶部：
                // refRect.yMax - targetY = itemRect.yMax
                position.y = this.refRect.yMax - itemRect.yMax;
            } else
            {
                // visibleRect.xMin = refRect.xMin - content.anchoredPosition.x
                // 让目标 item 的左边对齐可视区域左边：
                // refRect.xMin - targetX = itemRect.xMin
                position.x = this.refRect.xMin - itemRect.xMin;
            }

            return position;
        }

        private Vector2 ClampContentPosition(Vector2 position)
        {
            if (this.content == null)
            {
                return position;
            }

            if (this.vertical)
            {
                float maxY = Mathf.Max(0f, this.content.sizeDelta.y - this.refRect.height);
                position.y = Mathf.Clamp(position.y, 0f, maxY);
            }

            if (this.horizontal)
            {
                float maxX = Mathf.Max(0f, this.content.sizeDelta.x - this.refRect.width);
                position.x = Mathf.Clamp(position.x, -maxX, 0f);
            }

            return position;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ValidateScrollToResult(int index, Rect itemRect, Vector2 targetPosition)
        {
            this.UpdateVisibleRectCache();

            if (this.visibleRectCache.Overlaps(itemRect))
            {
                return;
            }

            Debug.LogWarning(
                $"ScrollView ScrollTo validation failed. " +
                $"index={index}, contentPos={this.content.anchoredPosition}, targetPos={targetPosition}, " +
                $"itemRect={itemRect}, visibleRect={this.visibleRectCache}, contentSize={this.content.sizeDelta}, refRect={this.refRect}",
                this);
        }
#endif

        protected override void SetContentAnchoredPosition(Vector2 position)
        {
            base.SetContentAnchoredPosition(position);

            if (this.willUpdateData != 0)
            {
                return;
            }

            this.criticalItemsDirty = true;
        }

        protected override void SetNormalizedPosition(float value, int axis)
        {
            base.SetNormalizedPosition(value, axis);

            if (this.willUpdateData != 0 || this.suppressNormalizedPositionRefresh)
            {
                return;
            }

            Vector2 currentPos = this.normalizedPosition;
            if (Mathf.Abs(currentPos.x - lastInvokedNormalizedPosition.x) > 0.01f ||
                Mathf.Abs(currentPos.y - lastInvokedNormalizedPosition.y) > 0.01f)
            {
                lastInvokedNormalizedPosition = currentPos;
                this.ResetCriticalItems();
            }
        }

        protected void StopScrollMovement()
        {
            this.velocity = Vector2.zero;
            this.customVelocity = Vector2.zero;
            this.criticalItemsDirty = false;
        }

        #endregion

        #region Rect 计算

        protected void EnsureItemRect(int index)
        {
            if (this.content == null || this.managedItems.Count == 0)
            {
                return;
            }

            if (index < 0 || index >= this.managedItems.Count)
            {
                return;
            }


            if (!this.managedItems[index].rectDirty)
            {
                // 已经是干净的了 
                return;
            }

            ScrollItemWithRect firstItem = this.managedItems[0];
            if (firstItem.rectDirty)
            {
                Vector2 firstSize = this.GetItemSize(0);
                firstItem.rect = CreateWithLeftTopAndSize(Vector2.zero, firstSize);
                firstItem.rectDirty = false;
            }

            // 当前item之前的最近的已更新的rect
            var nearestClean = 0;
            for (var i = index; i >= 0; --i)
            {
                if (!this.managedItems[i].rectDirty)
                {
                    nearestClean = i;
                    break;
                }
            }

            // 需要更新 从 nearestClean 到 index 的尺寸
            Rect nearestCleanRect = this.managedItems[nearestClean].rect;
            Vector2 curPos = GetLeftTop(nearestCleanRect);
            Vector2 size = nearestCleanRect.size;
            this.MovePos(ref curPos, size);

            for (var i = nearestClean + 1; i <= index; i++)
            {
                size = this.GetItemSize(i);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (size.x <= 0 || size.y <= 0)
                {
                    Debug.LogWarning($"item {i} size is {size}, both x and y should be greater than 0");
                }
#endif
                this.managedItems[i].rect = CreateWithLeftTopAndSize(curPos, size);
                this.managedItems[i].rectDirty = false;
                this.MovePos(ref curPos, size);
            }

            var range = new Vector2(Mathf.Abs(curPos.x), Mathf.Abs(curPos.y));
            switch (this.layoutType)
            {
                case ItemLayoutType.VerticalThenHorizontal:
                    range.x += size.x;
                    range.y = this.refRect.height;
                    break;
                case ItemLayoutType.HorizontalThenVertical:
                    range.x = this.refRect.width;
                    if (curPos.x != 0)
                    {
                        range.y += size.y;
                    }

                    break;
                default:
                    break;
            }

            this.content.sizeDelta = range;
        }

        #endregion

        #region Unity 生命周期 - 销毁

        protected override void OnDestroy()
        {
            this.RecycleVisibleItems();

            if (this.itemPool != null)
            {
                this.itemPool.Dispose();
                this.itemPool = null;
            }

            this.ResetAllDelegates();

            this.managedItems.Clear();
            this.activeItemIndexes.Clear();

            this.poolRoot = null;
            this.rootCanvas = null;

            this.customVelocity = Vector2.zero;
            this.lastDragPosition = Vector2.zero;
            this.isDraggingByUser = false;
            this.criticalItemsDirty = false;

            base.OnDestroy();
        }

        #endregion

        #region 可见 Item 回收

        private void RecycleVisibleItems()
        {
            if (this.activeItemIndexes.Count > 0)
            {
                for (int i = this.activeItemIndexes.Count - 1; i >= 0; i--)
                {
                    int index = this.activeItemIndexes[i];
                    if (index < 0 || index >= this.managedItems.Count)
                    {
                        continue;
                    }

                    RectTransform item = this.managedItems[index].item;
                    if (item == null)
                    {
                        continue;
                    }

                    this.RecycleOldItem(item);
                    this.managedItems[index].item = null;
                }

                this.activeItemIndexes.Clear();
                return;
            }

            // 兼容旧数据或异常状态：activeItemIndexes 为空但 managedItems 里仍可能残留 item
            for (int i = 0; i < this.managedItems.Count; i++)
            {
                RectTransform item = this.managedItems[i].item;
                if (item == null) continue;

                this.RecycleOldItem(item);
                this.managedItems[i].item = null;
            }
        }

        #endregion

        #region Rect 查询与基础坐标工具

        protected Rect GetItemLocalRect(int index)
        {
            if (index >= 0 && index < this.dataCount)
            {
                this.EnsureItemRect(index);
                return this.managedItems[index].rect;
            }

            return (Rect)default;
        }

        private static Vector2 GetLeftTop(Rect rect)
        {
            Vector2 ret = rect.position;
            ret.y += rect.size.y;
            return ret;
        }

        private static Rect CreateWithLeftTopAndSize(Vector2 leftTop, Vector2 size)
        {
            Vector2 leftBottom = leftTop - new Vector2(0, size.y);
            return new Rect(leftBottom, size);
        }

        #endregion

        #region 数据刷新实现

        private IEnumerator DelayUpdateData()
        {
            yield return waitForEndOfFrame;

            this.InternalUpdateData();
        }

        private void InternalUpdateData()
        {
            if (!this.IsActive())
            {
                this.willUpdateData |= 3;
                return;
            }

            if (!this.initialized)
            {
                if (!this.InitScrollView())
                {
                    return;
                }
            }

            this.CheckDataCountChange();

            this.ResetCriticalItems();

            this.willUpdateData = 0;
        }

        /// <summary>
        /// 只更新数据数量和 Rect 缓存，不刷新可见 Item。
        ///
        /// 用于 ScrollViewEx 翻页时：
        ///
        /// 1. startOffset 已经变化
        /// 2. 需要基于新 startOffset 重新计算 Item Rect
        /// 3. 但此时 Content 位置还没修正
        ///
        /// 所以不能直接 ResetCriticalItems，
        /// 否则会出现整页数据先刷新到错误位置，造成闪烁。
        /// </summary>
        protected void RebuildLayoutCacheOnly()
        {
            if (!this.IsActive())
            {
                this.willUpdateData |= 3;
                return;
            }

            if (!this.initialized)
            {
                if (!this.InitScrollView())
                {
                    return;
                }
            }

            // 标记为完整刷新，让 CheckDataCountChange 重置 rectDirty。
            this.willUpdateData |= 3;

            this.CheckDataCountChange();

            // 后续 SetContentAnchoredPosition 需要能够设置 criticalItemsDirty。
            this.willUpdateData = 0;
        }

        /// <summary>
        /// 立即刷新当前可见 Item。
        ///
        /// 和 UpdateData(true) 不同：
        ///
        /// 不重新 CheckDataCountChange，
        /// 只根据当前 Content 位置刷新可见区域。
        /// </summary>
        protected void RefreshVisibleItemsImmediately()
        {
            this.ResetCriticalItems();
        }

        protected virtual void CheckDataCountChange()
        {
            var newDataCount = 0;

            if (this.itemCountFunc != null)
            {
                try
                {
                    newDataCount = this.itemCountFunc();
                } catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }

            var keepOldItems = (this.willUpdateData & 2) == 0;
            int oldDataCount = this.dataCount;

            if (newDataCount != oldDataCount)
            {
                if (this.managedItems.Count < newDataCount)
                {
                    if (!keepOldItems)
                    {
                        this.MarkItemRectsDirty(0, this.managedItems.Count);
                    }

                    while (this.managedItems.Count < newDataCount)
                    {
                        this.managedItems.Add(new ScrollItemWithRect());
                    }
                }

                if (!keepOldItems)
                {
                    this.MarkItemRectsDirty(0, newDataCount);
                } else if (newDataCount > oldDataCount)
                {
                    this.MarkItemRectsDirty(oldDataCount, newDataCount);
                }

                if (newDataCount > 0 && newDataCount <= this.managedItems.Count)
                {
                    this.managedItems[newDataCount - 1].rectDirty = true;
                }

                if (newDataCount < oldDataCount)
                {
                    this.RecycleActiveItemsOutOfDataRange(newDataCount);
                    this.TrimManagedItemCacheIfNeeded(newDataCount);
                }
            } else if (!keepOldItems)
            {
                this.MarkItemRectsDirty(0, newDataCount);
            }

            this.dataCount = newDataCount;
        }

        #endregion

        #region 可见区域刷新

        private void ResetCriticalItems()
        {
            this.criticalItemsDirty = false;

            if (this.dataCount <= 0 || this.content == null)
            {
                this.criticalItemIndex[CriticalItemType.UpToHide] = -1;
                this.criticalItemIndex[CriticalItemType.DownToHide] = -1;
                this.criticalItemIndex[CriticalItemType.UpToShow] = -1;
                this.criticalItemIndex[CriticalItemType.DownToShow] = -1;
                this.RecycleVisibleItems();
                return;
            }

            this.UpdateVisibleRectCache();

            bool hasItem, shouldShow;
            int firstIndex = -1, lastIndex = -1;
            bool canBreakAfterVisible = this.CanBreakResetScanAfterVisibleRange();

            for (var i = 0; i < this.dataCount; i++)
            {
                hasItem = this.managedItems[i].item != null;
                shouldShow = this.ShouldItemSeenAtIndex(i);

                if (shouldShow)
                {
                    if (firstIndex == -1)
                    {
                        firstIndex = i;
                    }

                    lastIndex = i;
                }

                if (hasItem && shouldShow)
                {
                    // 应显示且已显示
                    this.SetDataForItemAtIndex(this.managedItems[i].item, i);
                    continue;
                }

                if (hasItem && !shouldShow)
                {
                    // 不该显示 但是有
                    this.RecycleOldItem(this.managedItems[i].item);
                    this.managedItems[i].item = null;
                    this.RemoveActiveItemIndex(i);

                    if (canBreakAfterVisible && firstIndex != -1 && this.IsItemPastVisibleRange(i))
                    {
                        break;
                    }

                    continue;
                }

                if (shouldShow && !hasItem)
                {
                    // 需要显示 但是没有
                    RectTransform item = this.GetNewItem(i);
                    if (item == null) continue;

                    this.OnGetItemForDataIndex(item, i);
                    this.managedItems[i].item = item;
                    this.AddActiveItemIndex(i);
                    continue;
                }

                if (canBreakAfterVisible && firstIndex != -1 && this.IsItemPastVisibleRange(i))
                {
                    break;
                }
            }

            if (firstIndex == -1)
            {
                this.TryRecoverVisibleRangeWhenEmpty();
                return;
            }

            this.RecycleActiveItemsOutsideVisibleRect();

            // content.localPosition = Vector2.zero;
            this.criticalItemIndex[CriticalItemType.UpToHide] = firstIndex;
            this.criticalItemIndex[CriticalItemType.DownToHide] = lastIndex;
            this.criticalItemIndex[CriticalItemType.UpToShow] = Mathf.Max(firstIndex - 1, 0);
            this.criticalItemIndex[CriticalItemType.DownToShow] = Mathf.Min(lastIndex + 1, this.dataCount - 1);
        }

        private void TryRecoverVisibleRangeWhenEmpty()
        {
            if (this.content == null)
            {
                return;
            }

            Vector2 offset = this.CalculateContentOffset(Vector2.zero);
            if (offset.sqrMagnitude <= 0.0001f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("ScrollView ResetCriticalItems found no visible item while content is inside bounds.", this);
#endif
                this.criticalItemIndex[CriticalItemType.UpToHide] = -1;
                this.criticalItemIndex[CriticalItemType.DownToHide] = -1;
                this.criticalItemIndex[CriticalItemType.UpToShow] = 0;
                this.criticalItemIndex[CriticalItemType.DownToShow] = Mathf.Min(1, this.dataCount - 1);
                return;
            }

            this.suppressNormalizedPositionRefresh = true;

            try
            {
                this.SetContentAnchoredPosition(this.content.anchoredPosition + offset);
            } finally
            {
                this.suppressNormalizedPositionRefresh = false;
            }

            this.UpdateVisibleRectCache();
            this.ResetCriticalItems();
        }

        private RectTransform GetCriticalItem(byte type)
        {
            var index = this.criticalItemIndex[type];
            if (index >= 0 && index < this.dataCount)
            {
                return this.managedItems[index].item;
            }

            return null;
        }

        private void UpdateCriticalItems()
        {
            this.UpdateVisibleRectCache();

            var dirty = true;
            var loopCount = 0;
            var maxLoop = Mathf.Max(4, this.dataCount);

            while (dirty && loopCount++ < maxLoop)
            {
                dirty = false;

                for (var i = CriticalItemType.UpToHide; i <= CriticalItemType.DownToShow; i++)
                {
                    bool changed;

                    if (i <= CriticalItemType.DownToHide)
                    {
                        changed = this.CheckAndHideItem(i);
                    } else
                    {
                        changed = this.CheckAndShowItem(i);
                    }

                    dirty = dirty || changed;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (loopCount >= maxLoop)
            {
                Debug.LogWarning("ScrollView UpdateCriticalItems loop reached maxLoop.");
            }
#endif
        }

        private bool CheckAndHideItem(byte criticalItemType)
        {
            RectTransform item = this.GetCriticalItem(criticalItemType);
            var criticalIndex = this.criticalItemIndex[criticalItemType];
            if (item != null && !this.ShouldItemSeenAtIndex(criticalIndex))
            {
                this.RecycleOldItem(item);
                this.managedItems[criticalIndex].item = null;
                this.RemoveActiveItemIndex(criticalIndex);

                if (criticalItemType == CriticalItemType.UpToHide)
                {
                    // 最上隐藏了一个
                    this.criticalItemIndex[criticalItemType + 2] = Mathf.Max(criticalIndex, this.criticalItemIndex[criticalItemType + 2]);
                    this.criticalItemIndex[criticalItemType]++;
                } else
                {
                    // 最下隐藏了一个
                    this.criticalItemIndex[criticalItemType + 2] = Mathf.Min(criticalIndex, this.criticalItemIndex[criticalItemType + 2]);
                    this.criticalItemIndex[criticalItemType]--;
                }

                this.criticalItemIndex[criticalItemType] = Mathf.Clamp(this.criticalItemIndex[criticalItemType], 0, this.dataCount - 1);

                if (this.criticalItemIndex[CriticalItemType.UpToHide] > this.criticalItemIndex[CriticalItemType.DownToHide])
                {
                    // 偶然的情况 拖拽超出一屏
                    this.ResetCriticalItems();
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool CheckAndShowItem(byte criticalItemType)
        {
            RectTransform item = this.GetCriticalItem(criticalItemType);
            var criticalIndex = this.criticalItemIndex[criticalItemType];

            if (item == null && this.ShouldItemSeenAtIndex(criticalIndex))
            {
                RectTransform newItem = this.GetNewItem(criticalIndex);
                if (newItem == null) return false;
                this.OnGetItemForDataIndex(newItem, criticalIndex);
                this.managedItems[criticalIndex].item = newItem;
                this.AddActiveItemIndex(criticalIndex);

                if (criticalItemType == CriticalItemType.UpToShow)
                {
                    // 最上显示了一个
                    this.criticalItemIndex[criticalItemType - 2] = Mathf.Min(criticalIndex, this.criticalItemIndex[criticalItemType - 2]);
                    this.criticalItemIndex[criticalItemType]--;
                } else
                {
                    // 最下显示了一个
                    this.criticalItemIndex[criticalItemType - 2] = Mathf.Max(criticalIndex, this.criticalItemIndex[criticalItemType - 2]);
                    this.criticalItemIndex[criticalItemType]++;
                }

                this.criticalItemIndex[criticalItemType] = Mathf.Clamp(this.criticalItemIndex[criticalItemType], 0, this.dataCount - 1);

                if (this.criticalItemIndex[CriticalItemType.UpToShow] >= this.criticalItemIndex[CriticalItemType.DownToShow])
                {
                    // 偶然的情况 拖拽超出一屏
                    this.ResetCriticalItems();
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool ShouldItemSeenAtIndex(int index)
        {
            if (index < 0 || index >= this.dataCount)
            {
                return false;
            }

            this.EnsureItemRect(index);
            return this.visibleRectCache.Overlaps(this.managedItems[index].rect);
        }

        private void UpdateVisibleRectCache()
        {
            if (this.content == null)
            {
                this.visibleRectCache = default(Rect);
                return;
            }

            this.visibleRectCache = new Rect(this.refRect.position - this.content.anchoredPosition, this.refRect.size);
        }

        private bool CanBreakResetScanAfterVisibleRange()
        {
            return this.layoutType == ItemLayoutType.Vertical || this.layoutType == ItemLayoutType.Horizontal;
        }

        private bool IsItemPastVisibleRange(int index)
        {
            if (index < 0 || index >= this.dataCount)
            {
                return false;
            }

            this.EnsureItemRect(index);
            Rect itemRect = this.managedItems[index].rect;

            if (this.layoutType == ItemLayoutType.Vertical)
            {
                return itemRect.yMax < this.visibleRectCache.yMin;
            }

            if (this.layoutType == ItemLayoutType.Horizontal)
            {
                return itemRect.xMin > this.visibleRectCache.xMax;
            }

            return false;
        }

        #endregion

        #region 可见 Item 索引管理

        private void RecycleActiveItemsOutsideVisibleRect()
        {
            for (int i = this.activeItemIndexes.Count - 1; i >= 0; i--)
            {
                int index = this.activeItemIndexes[i];

                if (index < 0 || index >= this.dataCount || index >= this.managedItems.Count)
                {
                    this.RemoveActiveItemIndexAt(i);
                    continue;
                }

                RectTransform item = this.managedItems[index].item;
                if (item == null)
                {
                    this.RemoveActiveItemIndexAt(i);
                    continue;
                }

                this.EnsureItemRect(index);
                if (this.visibleRectCache.Overlaps(this.managedItems[index].rect))
                {
                    continue;
                }

                this.RecycleOldItem(item);
                this.managedItems[index].item = null;
                this.RemoveActiveItemIndexAt(i);
            }
        }

        private void AddActiveItemIndex(int index)
        {
            if (index < 0)
            {
                return;
            }

            for (int i = 0; i < this.activeItemIndexes.Count; i++)
            {
                if (this.activeItemIndexes[i] == index)
                {
                    return;
                }
            }

            this.activeItemIndexes.Add(index);
        }

        private void RemoveActiveItemIndex(int index)
        {
            for (int i = this.activeItemIndexes.Count - 1; i >= 0; i--)
            {
                if (this.activeItemIndexes[i] == index)
                {
                    this.RemoveActiveItemIndexAt(i);
                    return;
                }
            }
        }

        private void RemoveActiveItemIndexAt(int listIndex)
        {
            int last = this.activeItemIndexes.Count - 1;
            if (listIndex < 0 || listIndex > last)
            {
                return;
            }

            this.activeItemIndexes[listIndex] = this.activeItemIndexes[last];
            this.activeItemIndexes.RemoveAt(last);
        }

        #endregion

        #region Item 获取、绑定与回收

        private bool InitPool()
        {
            if (this.itemPool != null)
            {
                return true;
            }

            if (this.itemTemplate == null)
            {
                Debug.LogError("ScrollView itemTemplate is null. Assign itemTemplate or use SetItemGetAndRecycleFunc with an external pool.", this);
                return false;
            }

            if (this.content == null)
            {
                Debug.LogError("ScrollView content is null. Assign ScrollRect.content before UpdateData.", this);
                return false;
            }

            var poolNode = new GameObject("POOL");
            poolNode.SetActive(false);
            poolNode.transform.SetParent(this.transform, false);

            this.poolRoot = poolNode.transform;

            this.itemPool = new ScrollItemPool(
                this.itemTemplate,
                this.poolRoot,
                this.content,
                Mathf.Max(this.poolSize, 1),
                expandable: true,
                setInactiveWhenRecycle: false);

            return true;
        }

        private void OnGetItemForDataIndex(RectTransform item, int index)
        {
            this.SetDataForItemAtIndex(item, index);
            item.transform.SetParent(this.content, false);
        }

        private void SetDataForItemAtIndex(RectTransform item, int index)
        {
            if (this.updateFunc != null)
            {
                try
                {
                    this.updateFunc(index, item);
                } catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }

            this.SetPosForItemAtIndex(item, index);
        }

        private void SetPosForItemAtIndex(RectTransform item, int index)
        {
            this.EnsureItemRect(index);
            Rect r = this.managedItems[index].rect;
            item.localPosition = r.position;
            item.sizeDelta = r.size;
        }

        private Vector2 GetItemSize(int index)
        {
            if (this.itemSizeFunc != null)
            {
                try
                {
                    return this.itemSizeFunc(index);
                } catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }

            return this.defaultItemSize;
        }

        private RectTransform GetNewItem(int index)
        {
            RectTransform item;

            if (this.itemGetFunc != null)
            {
                try
                {
                    item = this.itemGetFunc(index);

                    if (item != null)
                    {
                        item.anchorMin = Vector2.up;
                        item.anchorMax = Vector2.up;
                        item.pivot = Vector2.zero;
                    }
                } catch (Exception e)
                {
                    Debug.LogException(e);
                    item = null;
                }
            } else
            {
                if (this.itemPool == null)
                {
                    Debug.LogError("ScrollView internal item pool is not initialized.", this);
                    return null;
                }

                item = this.itemPool.Get();
            }

            return item;
        }

        private void RecycleOldItem(RectTransform item)
        {
            if (item == null)
            {
                return;
            }

            if (this.itemRecycleFunc != null)
            {
                try
                {
                    this.itemRecycleFunc(item);
                } catch (Exception e)
                {
                    Debug.LogException(e);
                }
            } else
            {
                if (this.itemPool == null)
                {
                    return;
                }

                this.itemPool.Recycle(item);
            }
        }

        #endregion

        #region 初始化与布局缓存

        private bool InitScrollView()
        {
            if (this.content == null)
            {
                Debug.LogError("ScrollView content is null. Assign ScrollRect.content before UpdateData.", this);
                return false;
            }

            bool useInternalPool = this.itemGetFunc == null;
            if (useInternalPool && this.itemTemplate == null)
            {
                Debug.LogError("ScrollView itemTemplate is null. Assign itemTemplate or use SetItemGetAndRecycleFunc with an external pool.", this);
                return false;
            }

            // 根据设置来控制原ScrollRect的滚动方向
            var dir = (int)this.layoutType & flagScrollDirection;
            this.vertical = dir == 1;
            this.horizontal = dir == 0;

            this.content.pivot = Vector2.up;
            this.content.anchorMin = Vector2.up;
            this.content.anchorMax = Vector2.up;
            this.content.anchoredPosition = Vector2.zero;

            if (useInternalPool && !this.InitPool())
            {
                return false;
            }

            this.UpdateRefRect();
            this.initialized = true;
            return true;
        }

        // refRect 是 Content 回到 anchoredPosition = 0 时，viewport 在 Content 本地坐标下的固定可视区域
        private void UpdateRefRect()
        {
            if (this.content == null)
            {
                return;
            }

            if (!CanvasUpdateRegistry.IsRebuildingLayout())
            {
                Canvas.ForceUpdateCanvases();
            }

            this.viewRect.GetWorldCorners(viewWorldCorners);
            this.rectCorners[0] = this.content.transform.InverseTransformPoint(viewWorldCorners[0]);
            this.rectCorners[1] = this.content.transform.InverseTransformPoint(viewWorldCorners[2]);

            Vector2 position = this.rectCorners[0];
            Vector2 size = this.rectCorners[1] - this.rectCorners[0];

            // InverseTransformPoint 会受到当前 content.anchoredPosition 影响。
            // 而后续可视区域又会使用 refRect.position - content.anchoredPosition。
            // 如果这里不把当前滚动偏移补回去，ScrollTo / 尺寸刷新后会出现双重偏移，
            // 大索引时直接得到几十万、几百万级别的错误可视区域。
            position += this.content.anchoredPosition;

            this.refRect = new Rect(position, size);
            this.lastRefRectSize = this.viewRect.rect.size;
            this.hasRefRect = true;
        }

        private void RefreshRefRectIfNeeded(bool refreshVisibleItems)
        {
            if (this.content == null)
            {
                return;
            }

            Vector2 currentSize = this.viewRect.rect.size;
            if (this.hasRefRect && Approximately(currentSize, this.lastRefRectSize))
            {
                return;
            }

            this.UpdateRefRect();
            this.MarkAllItemRectsDirty();

            if (refreshVisibleItems && this.willUpdateData == 0)
            {
                this.ResetCriticalItems();
            }
        }

        private void MarkItemRectsDirty(int fromInclusive, int toExclusive)
        {
            fromInclusive = Mathf.Max(0, fromInclusive);
            toExclusive = Mathf.Min(toExclusive, this.managedItems.Count);

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                this.managedItems[i].rectDirty = true;
            }
        }

        private void RecycleActiveItemsOutOfDataRange(int activeCount)
        {
            for (int i = this.activeItemIndexes.Count - 1; i >= 0; i--)
            {
                int index = this.activeItemIndexes[i];
                if (index < 0 || index < activeCount)
                {
                    continue;
                }

                if (index < this.managedItems.Count && this.managedItems[index].item != null)
                {
                    this.RecycleOldItem(this.managedItems[index].item);
                    this.managedItems[index].item = null;
                }

                this.RemoveActiveItemIndexAt(i);
            }
        }

        private void TrimManagedItemCacheIfNeeded(int activeCount)
        {
            if (!this.trimManagedItemCacheOnShrink)
            {
                return;
            }

            int extra = this.managedItems.Count - activeCount;
            if (extra <= this.trimManagedItemCacheExtra)
            {
                return;
            }

            this.TrimManagedItemCacheTo(activeCount + this.trimManagedItemCacheExtra);
        }

        private void TrimManagedItemCacheTo(int targetCount)
        {
            targetCount = Mathf.Clamp(targetCount, 0, this.managedItems.Count);
            if (targetCount >= this.managedItems.Count)
            {
                return;
            }

            for (int i = this.managedItems.Count - 1; i >= targetCount; i--)
            {
                if (this.managedItems[i].item != null)
                {
                    this.RecycleOldItem(this.managedItems[i].item);
                    this.managedItems[i].item = null;
                }

                this.RemoveActiveItemIndex(i);
            }

            this.managedItems.RemoveRange(targetCount, this.managedItems.Count - targetCount);

            if (this.managedItems.Capacity > targetCount + this.trimManagedItemCacheExtra * 2)
            {
                this.managedItems.TrimExcess();
            }
        }

        private void MarkAllItemRectsDirty()
        {
            for (int i = 0; i < this.managedItems.Count; i++)
            {
                this.managedItems[i].rectDirty = true;
            }
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private void MovePos(ref Vector2 pos, Vector2 size)
        {
            // 注意 所有的rect都是左下角为基准
            switch (this.layoutType)
            {
                case ItemLayoutType.Vertical:
                    // 垂直方向 向下移动
                    pos.y -= size.y;
                    break;
                case ItemLayoutType.Horizontal:
                    // 水平方向 向右移动
                    pos.x += size.x;
                    break;
                case ItemLayoutType.VerticalThenHorizontal:
                    pos.y -= size.y;
                    if (pos.y - size.y < -this.refRect.height)
                    {
                        pos.y = 0;
                        pos.x += size.x;
                    }

                    break;
                case ItemLayoutType.HorizontalThenVertical:

                    pos.x += size.x;
                    if (pos.x + size.x > this.refRect.width)
                    {
                        pos.x = 0;
                        pos.y -= size.y;
                    }

                    break;
                default:
                    break;
            }
        }

        #endregion

        #region 内部类型

        protected static class CriticalItemType
        {
            public const byte UpToHide = 0;
            public const byte DownToHide = 1;
            public const byte UpToShow = 2;
            public const byte DownToShow = 3;
        }

        private class ScrollItemWithRect
        {
            // scroll item 身上的 RectTransform组件
            public RectTransform item;

            // scroll item 在scrollview中的位置
            public Rect rect;

            // rect 是否需要更新
            public bool rectDirty = true;
        }

        #endregion
    }
}
