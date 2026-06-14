namespace GuiSolution.UIBinder
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public static class UIBindGeneratedUtil
    {
        private const string GeneratedLogPrefix = "[UIBindGenerated]";
        private const string AutoBindLogPrefix = "<color=lime>[UIAutoBind]</color>";

        private static readonly Dictionary<int, RootCache> Caches = new Dictionary<int, RootCache>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCaches()
        {
            Caches.Clear();
        }

        public static void ClearAllCaches()
        {
            Caches.Clear();
        }

        public static void LogMissingComponent(MonoBehaviour owner, string pathOrName, string memberName, string componentNames)
        {
            if (owner == null) return;

            Debug.LogError(
                $"{GeneratedLogPrefix} {owner.GetType().Name}.{memberName}: node '{pathOrName}' is missing component {componentNames}.",
                owner);
        }

        public static void ClearCache(MonoBehaviour owner)
        {
            if (owner == null) return;
            ClearCache(owner.transform);
        }

        public static void ClearCache(Transform root)
        {
            if (root == null) return;

            Caches.Remove(root.GetInstanceID());
        }

        public static Transform FindByPathOrName(Transform root, string pathOrName)
        {
            return FindByPathOrNameInternal(root, pathOrName, null, null, null, GeneratedLogPrefix, null);
        }

        public static Transform FindByPathOrName(MonoBehaviour owner, string pathOrName,
            string memberName, params Type[] expectedComponentTypes)
        {
            if (owner == null) return null;

            return FindByPathOrNameInternal(owner.transform, pathOrName, owner.GetType().Name,
                memberName, owner, GeneratedLogPrefix, expectedComponentTypes);
        }

        public static Transform FindAutoBindTarget(Transform root, string pathOrName, string className,
            string memberName, params Type[] expectedComponentTypes)
        {
            if (string.IsNullOrEmpty(pathOrName))
            {
                pathOrName = memberName;
            }

            return FindByPathOrNameInternal(root, pathOrName, className,
                memberName, root, AutoBindLogPrefix, expectedComponentTypes);
        }

        public static T GetComponentByPathOrName<T>(MonoBehaviour owner, string pathOrName, string memberName) where T : Component
        {
            if (owner == null) return null;

            Transform target = FindByPathOrName(owner, pathOrName, memberName, typeof(T));
            if (target == null) return null;

            T comp = target.GetComponent<T>();
            if (comp == null)
            {
                Debug.LogError(
                    $"{GeneratedLogPrefix} {owner.GetType().Name}.{memberName}:" +
                    $" node '{GetDisplayPath(owner.transform, target)}' is missing component {typeof(T).Name}.",
                    owner);
            }

            return comp;
        }

        private static Transform FindByPathOrNameInternal(Transform root, string pathOrName, string ownerName,
            string memberName, UnityEngine.Object logContext, string logPrefix, Type[] expectedComponentTypes)
        {
            if (root == null)
            {
                LogError(logPrefix, ownerName, memberName, "RootTransform is null.", logContext);
                return null;
            }

            if (string.IsNullOrEmpty(pathOrName))
            {
                LogError(logPrefix, ownerName, memberName, "Target path/name is empty.", logContext);
                return null;
            }

            string query = NormalizeQuery(root, pathOrName);
            if (string.IsNullOrEmpty(query))
            {
                return root;
            }

            Transform target = root.Find(query);
            if (target != null)
            {
                return target;
            }

            RootCache cache = GetOrBuildCache(root);
            if (cache.ByPath.TryGetValue(query, out target))
            {
                return target;
            }

            if (query.IndexOf('/') >= 0)
            {
                target = FindBestBySuffixPath(cache, query, expectedComponentTypes, ownerName,
                    memberName, pathOrName, logContext, logPrefix);

                if (target != null) return target;
            }

            string lastName = GetLastPathName(query);
            target = FindBestByName(cache, lastName, expectedComponentTypes, ownerName,
                memberName, pathOrName, logContext, logPrefix);

            if (target != null) return target;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            target = FindBestByFuzzyName(cache, lastName, expectedComponentTypes, ownerName,
                memberName, pathOrName, logContext, logPrefix);

            if (target != null) return target;
#endif

            LogError(logPrefix, ownerName, memberName, $"Target path/name '{pathOrName}' not found.", logContext);
            return null;
        }

        private static string NormalizeQuery(Transform root, string pathOrName)
        {
            string query = pathOrName.Replace('\\', '/').Trim('/');
            if (query == root.name)
            {
                return string.Empty;
            }

            string rootPrefix = root.name + "/";
            if (query.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                query = query.Substring(rootPrefix.Length);
            }

            return query;
        }

        private static string GetLastPathName(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        private static RootCache GetOrBuildCache(Transform root)
        {
            int id = root.GetInstanceID();
            if (Caches.TryGetValue(id, out RootCache cache) && cache != null && cache.Root == root)
            {
                return cache;
            }

            cache = new RootCache(root);
            Caches[id] = cache;
            return cache;
        }

        private static Transform FindBestByName(RootCache cache, string nodeName, Type[] expectedTypes,
            string ownerName, string memberName, string originalQuery, UnityEngine.Object logContext, string logPrefix)
        {
            if (cache == null || string.IsNullOrEmpty(nodeName)) return null;
            if (!cache.ByName.TryGetValue(nodeName, out List<Transform> list) || list == null || list.Count == 0) return null;

            return PickBestCandidate(list, cache.Root, expectedTypes, ownerName,
                memberName, originalQuery, $"name '{nodeName}'", logContext, logPrefix);
        }

        private static Transform FindBestBySuffixPath(RootCache cache, string suffixPath, Type[] expectedTypes,
            string ownerName, string memberName, string originalQuery, UnityEngine.Object logContext, string logPrefix)
        {
            if (cache == null || string.IsNullOrEmpty(suffixPath) || suffixPath.IndexOf('/') < 0) return null;

            Transform picked = PickBestCandidateBySuffixPath(cache, suffixPath, expectedTypes, ownerName,
                memberName, originalQuery, logContext, logPrefix);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (picked != null)
            {
                LogWarning( logPrefix, ownerName, memberName,
                    $"Path '{originalQuery}' was matched by suffix path '{GetDisplayPath(cache.Root, picked)}'. It is recommended to use the full path.",
                    logContext);
            }
#endif

            return picked;
        }

        private static Transform PickBestCandidateBySuffixPath(RootCache cache, string suffixPath, Type[] expectedTypes,
            string ownerName, string memberName, string originalQuery, UnityEngine.Object logContext, string logPrefix)
        {
            Transform first = null;
            int totalCount = 0;

            Transform firstMatched = null;
            int matchedCount = 0;
            bool hasExpectedTypes = expectedTypes != null && expectedTypes.Length > 0;

            foreach (KeyValuePair<string, Transform> kv in cache.ByPath)
            {
                string fullPath = kv.Key;
                if (!fullPath.EndsWith("/" + suffixPath, StringComparison.Ordinal)) continue;

                Transform t = kv.Value;
                if (t == null) continue;

                totalCount++;
                if (first == null)
                {
                    first = t;
                }

                if (hasExpectedTypes && HasAnyExpectedComponent(t, expectedTypes))
                {
                    matchedCount++;
                    if (firstMatched == null)
                    {
                        firstMatched = t;
                    }
                }
            }

            Transform picked = matchedCount > 0 ? firstMatched : first;
            int finalCount = matchedCount > 0 ? matchedCount : totalCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (picked != null && finalCount > 1)
            {
                LogWarning( logPrefix, ownerName, memberName,
                    $"'{originalQuery}' matched {finalCount} nodes by suffix path '{suffixPath}'. " +
                    $"Using '{GetDisplayPath(cache.Root, picked)}'. It is recommended to use the full path.",
                    logContext);
            }
#endif

            return picked;
        }

        private static Transform PickBestCandidate(List<Transform> candidates, Transform root, Type[] expectedTypes,
            string ownerName, string memberName, string originalQuery, string reason,
            UnityEngine.Object logContext, string logPrefix)
        {
            if (candidates == null || candidates.Count == 0) return null;

            Transform first = null;
            int totalCount = 0;

            Transform firstMatched = null;
            int matchedCount = 0;
            bool hasExpectedTypes = expectedTypes != null && expectedTypes.Length > 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform t = candidates[i];
                if (t == null) continue;

                totalCount++;
                if (first == null)
                {
                    first = t;
                }

                if (hasExpectedTypes && HasAnyExpectedComponent(t, expectedTypes))
                {
                    matchedCount++;
                    if (firstMatched == null)
                    {
                        firstMatched = t;
                    }
                }
            }

            Transform picked = matchedCount > 0 ? firstMatched : first;
            int finalCount = matchedCount > 0 ? matchedCount : totalCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (picked != null && finalCount > 1)
            {
                LogWarning( logPrefix, ownerName, memberName,
                    $"'{originalQuery}' matched {finalCount} nodes by {reason}. " +
                    $"Using '{GetDisplayPath(root, picked)}'. It is recommended to use the full path.",
                    logContext);
            }
#endif

            return picked;
        }

        private static bool HasAnyExpectedComponent(Transform t, Type[] expectedTypes)
        {
            if (t == null) return false;
            if (expectedTypes == null || expectedTypes.Length == 0) return true;

            for (int i = 0; i < expectedTypes.Length; i++)
            {
                Type type = expectedTypes[i];
                if (type == null) continue;
                if (t.GetComponent(type) != null) return true;
            }

            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static Transform FindBestByFuzzyName(RootCache cache, string nodeName, Type[] expectedTypes,
            string ownerName, string memberName, string originalQuery, UnityEngine.Object logContext, string logPrefix)
        {
            if (cache == null || string.IsNullOrEmpty(nodeName)) return null;

            Transform best = null;
            float bestScore = 0f;

            int targetLength = nodeName.Length;
            int[] previousRow = new int[targetLength + 1];
            int[] currentRow = new int[targetLength + 1];

            for (int i = 0; i < cache.Nodes.Length; i++)
            {
                Transform t = cache.Nodes[i];
                if (t == null) continue;
                if (!HasAnyExpectedComponent(t, expectedTypes)) continue;

                float score = CalculateStringSimilarity(t.name, nodeName, previousRow, currentRow);
                if (score > 0.85f && score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            if (best != null)
            {
                LogWarning( logPrefix, ownerName, memberName,
                    $"Path '{originalQuery}' not found. Fuzzy matched '{GetDisplayPath(cache.Root, best)}'. Similarity: {bestScore:P0}.",
                    logContext);
            }

            return best;
        }
#endif

        private static string GetDisplayPath(Transform root, Transform target)
        {
            string path = GetRelativePath(root, target);
            return string.IsNullOrEmpty(path) && target != null ? target.name : path;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            if (target == root) return string.Empty;

            var names = new Stack<string>();
            Transform cur = target;
            while (cur != null && cur != root)
            {
                names.Push(cur.name);
                cur = cur.parent;
            }

            return string.Join("/", names.ToArray());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float CalculateStringSimilarity(string source, string target, int[] previousRow, int[] currentRow)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0f;

            int sourceLength = source.Length;
            int targetLength = target.Length;

            if (previousRow == null || currentRow == null ||
                previousRow.Length < targetLength + 1 || currentRow.Length < targetLength + 1)
            {
                return 0f;
            }

            for (int j = 0; j <= targetLength; j++)
            {
                previousRow[j] = j;
            }

            for (int i = 1; i <= sourceLength; i++)
            {
                currentRow[0] = i;

                for (int j = 1; j <= targetLength; j++)
                {
                    bool same = char.ToLowerInvariant(source[i - 1]) == char.ToLowerInvariant(target[j - 1]);
                    int cost = same ? 0 : 1;

                    currentRow[j] = Math.Min(
                        Math.Min(previousRow[j] + 1, currentRow[j - 1] + 1),
                        previousRow[j - 1] + cost);
                }

                int[] temp = previousRow;
                previousRow = currentRow;
                currentRow = temp;
            }

            return 1f - (float)previousRow[targetLength] / Math.Max(sourceLength, targetLength);
        }
#endif

        private static void LogError(string logPrefix, string ownerName, string memberName, string message, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(ownerName) && string.IsNullOrEmpty(memberName)) return;

            Debug.LogError($"{logPrefix} {FormatMember(ownerName, memberName)}: {message}", context);
        }

        private static void LogWarning(string logPrefix, string ownerName, string memberName, string message, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(ownerName) && string.IsNullOrEmpty(memberName)) return;

            Debug.LogWarning($"{logPrefix} {FormatMember(ownerName, memberName)}: {message}", context);
        }

        private static string FormatMember(string ownerName, string memberName)
        {
            if (string.IsNullOrEmpty(ownerName)) return memberName ?? string.Empty;
            if (string.IsNullOrEmpty(memberName)) return ownerName;
            return ownerName + "." + memberName;
        }

        private sealed class RootCache
        {
            public readonly Transform Root;
            public readonly Transform[] Nodes;
            public readonly Dictionary<string, List<Transform>> ByName = new Dictionary<string, List<Transform>>(128);
            public readonly Dictionary<string, Transform> ByPath = new Dictionary<string, Transform>(128);

            public RootCache(Transform root)
            {
                Root = root;
                Nodes = root.GetComponentsInChildren<Transform>(true);

                for (int i = 0; i < Nodes.Length; i++)
                {
                    Transform t = Nodes[i];
                    if (t == null) continue;

                    if (!ByName.TryGetValue(t.name, out List<Transform> list))
                    {
                        list = new List<Transform>(1);
                        ByName.Add(t.name, list);
                    }

                    list.Add(t);

                    string path = GetRelativePath(root, t);
                    if (!string.IsNullOrEmpty(path) && !ByPath.ContainsKey(path))
                    {
                        ByPath.Add(path, t);
                    }
                }
            }
        }
    } 
}
