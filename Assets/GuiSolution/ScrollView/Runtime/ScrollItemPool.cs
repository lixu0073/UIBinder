namespace GuiSolution.ScrollView
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// ScrollView 内部 Item 对象池
    ///
    /// 设计目标：
    /// 1. Get / Recycle 路径不产生托管 GC
    /// 2. 支持重复回收检测
    /// 3. Dispose 时销毁所有由池创建的 Item，避免 active item 未回收时残留
    /// 4. 支持按需裁剪缓存，避免大列表用完后长期占用内存
    /// </summary>
    public sealed class ScrollItemPool
    {
        private readonly Stack<RectTransform> stack;
        private readonly HashSet<RectTransform> pooledSet;
        private readonly HashSet<RectTransform> allSet;
        private readonly List<RectTransform> allItems;

        private readonly RectTransform template;
        private readonly Transform poolRoot;
        private readonly Transform activeRoot;

        private readonly int preloadCount;
        private readonly bool expandable;
        private readonly bool setInactiveWhenRecycle;
        private readonly int maxCachedCount;

        private int usedCount;
        private bool disposed;

        #region 属性

        public int CreatedCount { get { return this.allSet.Count; } }
        public int UsedCount { get { return this.usedCount; } }
        public int CachedCount { get { return this.stack.Count; } }
        public int MaxCachedCount { get { return this.maxCachedCount; } }
        public bool IsDisposed { get { return this.disposed; } }

        #endregion

        #region 构造与生命周期

        public ScrollItemPool(RectTransform template, Transform poolRoot, Transform activeRoot, int preloadCount,
            bool expandable = true, bool setInactiveWhenRecycle = false, int maxCachedCount = -1)
        {
            this.template = template;
            this.poolRoot = poolRoot;
            this.activeRoot = activeRoot;
            this.preloadCount = Mathf.Max(0, preloadCount);
            this.expandable = expandable;
            this.setInactiveWhenRecycle = setInactiveWhenRecycle;
            this.maxCachedCount = maxCachedCount < 0 ? -1 : maxCachedCount;

            int capacity = Mathf.Max(this.preloadCount, 1);
            this.stack = new Stack<RectTransform>(capacity);
            this.pooledSet = new HashSet<RectTransform>();
            this.allSet = new HashSet<RectTransform>();
            this.allItems = new List<RectTransform>(capacity);

            this.Preload();
        }

        #endregion

        #region 获取与回收

        public RectTransform Get()
        {
            if (this.disposed)
            {
                return null;
            }

            RectTransform item = this.PopValidCachedItem();

            if (item == null)
            {
                if (!this.expandable && this.CreatedCount >= this.preloadCount)
                {
                    return null;
                }

                item = this.CreateNewItem();
            }

            if (item == null)
            {
                return null;
            }

            this.usedCount++;

            if (this.activeRoot != null)
            {
                item.SetParent(this.activeRoot, false);
            }

            this.ApplyDefaultTransform(item);

            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
            }

            return item;
        }

        public void Recycle(RectTransform item)
        {
            if (this.disposed || item == null)
            {
                return;
            }

            if (!this.allSet.Contains(item))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"ScrollItemPool recycle ignored unmanaged item: {item.name}");
#endif
                return;
            }

            if (this.pooledSet.Contains(item))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"ScrollItemPool duplicate recycle: {item.name}");
#endif
                return;
            }

            this.usedCount = Mathf.Max(0, this.usedCount - 1);

            if (this.maxCachedCount >= 0 && this.stack.Count >= this.maxCachedCount)
            {
                this.DestroyManagedItem(item);
                return;
            }

            if (this.poolRoot != null)
            {
                item.SetParent(this.poolRoot, false);
            }

            this.ApplyDefaultTransform(item);

            if (this.setInactiveWhenRecycle && item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(false);
            }

            this.pooledSet.Add(item);
            this.stack.Push(item);
        }

        /// <summary>
        /// 只销毁当前池内缓存的 Item
        ///
        /// 已经被 Get 出去的 active item 不会销毁
        /// </summary>
        public void Clear()
        {
            this.TrimCache(0);
        }

        /// <summary>
        /// 裁剪池内缓存数量
        ///
        /// keepCount 小于 0 时不处理
        /// </summary>
        public void TrimCache(int keepCount)
        {
            if (this.disposed || keepCount < 0)
            {
                return;
            }

            while (this.stack.Count > keepCount)
            {
                RectTransform item = this.stack.Pop();
                if (item == null)
                {
                    continue;
                }

                this.pooledSet.Remove(item);
                this.DestroyManagedItem(item);
            }
        }

        #endregion

        #region 构造与生命周期

        /// <summary>
        /// 销毁所有由池创建的 Item
        ///
        /// 包括池内缓存和已经被 Get 出去但尚未 Recycle 的 active item
        /// </summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            for (int i = 0; i < this.allItems.Count; i++)
            {
                RectTransform item = this.allItems[i];
                if (item != null)
                {
                    Object.Destroy(item.gameObject);
                }
            }

            this.stack.Clear();
            this.pooledSet.Clear();
            this.allSet.Clear();
            this.allItems.Clear();
            this.usedCount = 0;
        }

        #endregion

        #region 内部创建与销毁

        private RectTransform PopValidCachedItem()
        {
            RectTransform item = null;

            while (this.stack.Count > 0)
            {
                item = this.stack.Pop();

                if (item == null)
                {
                    continue;
                }

                this.pooledSet.Remove(item);

                if (!this.allSet.Contains(item))
                {
                    item = null;
                    continue;
                }

                break;
            }

            return item;
        }

        private void Preload()
        {
            for (int i = 0; i < this.preloadCount; i++)
            {
                RectTransform item = this.CreateNewItem();
                if (item == null)
                {
                    continue;
                }

                if (this.poolRoot != null)
                {
                    item.SetParent(this.poolRoot, false);
                }

                this.ApplyDefaultTransform(item);

                if (this.setInactiveWhenRecycle && item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(false);
                }

                this.pooledSet.Add(item);
                this.stack.Push(item);
            }
        }

        private RectTransform CreateNewItem()
        {
            if (this.template == null)
            {
                return null;
            }

            GameObject itemObj = Object.Instantiate(this.template.gameObject);
            RectTransform item = itemObj.GetComponent<RectTransform>();

            if (item == null)
            {
                Object.Destroy(itemObj);
                return null;
            }

            this.ApplyDefaultTransform(item);

            this.allItems.Add(item);
            this.allSet.Add(item);

            return item;
        }

        private void DestroyManagedItem(RectTransform item)
        {
            if (item == null)
            {
                return;
            }

            this.pooledSet.Remove(item);
            this.allSet.Remove(item);

            int index = this.allItems.IndexOf(item);
            if (index >= 0)
            {
                int lastIndex = this.allItems.Count - 1;
                this.allItems[index] = this.allItems[lastIndex];
                this.allItems.RemoveAt(lastIndex);
            }

            Object.Destroy(item.gameObject);
        }

        private void ApplyDefaultTransform(RectTransform item)
        {
            if (item == null)
            {
                return;
            }

            item.anchorMin = Vector2.up;
            item.anchorMax = Vector2.up;
            item.pivot = Vector2.zero;
            item.localScale = Vector3.one;
            item.localRotation = Quaternion.identity;
        }

        #endregion

        #region 调试

        public override string ToString()
        {
            return $"ScrollItemPool: created=[{this.CreatedCount}], used=[{this.usedCount}], cached=[{this.stack.Count}], maxCached=[{this.maxCachedCount}]";
        }

        #endregion
    }
}
