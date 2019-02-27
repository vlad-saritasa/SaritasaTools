﻿// Copyright (c) 2015-2019, Saritasa. All rights reserved.
// Licensed under the BSD license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if NET40 || NET452 || NET461
using System.Runtime.Serialization;
#endif

namespace Saritasa.Tools.Common.Utils
{
    /// <summary>
    /// Provides methods to control execution flow.
    /// </summary>
    public static partial class FlowUtils
    {
        /// <summary>
        /// Consumer code can throw the exception to skip item memoization.
        /// </summary>
#if NET40 || NET452 || NET461 || NETSTANDARD2_0
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2240:ImplementISerializableCorrectly", Justification = "GetObjectData is not needed")]
        [Serializable]
#endif
        public sealed class SkipMemoizeException<TResult> : Exception
        {
            private readonly TResult result;

            /// <summary>
            /// Returned result.
            /// </summary>
            public TResult Result => result;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="result">Result from memoize delegate.</param>
            public SkipMemoizeException(TResult result)
            {
                this.result = result;
            }

#if NET40 || NET452 || NET461
            /// <summary>
            /// Constructor for deserialization.
            /// </summary>
            /// <param name="info">Stores all the data needed to serialize or deserialize an object.</param>
            /// <param name="context">Describes the source and destination of a given serialized stream,
            /// and provides an additional caller-defined context.</param>
            private SkipMemoizeException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
#endif
        }

        /// <summary>
        /// Cache strategy delegate determines when value must be invalidated.
        /// </summary>
        /// <typeparam name="TKey">Cache function key type.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="cache">Cache storage used.</param>
        /// <param name="notInCache"><c>True</c> if key was not in cache.</param>
        /// <returns><c>True</c> if value must be evaluated and cached again.</returns>
        public delegate bool CacheStrategy<TKey, TResult>(TKey key, IDictionary<TKey, TResult> cache, bool notInCache);

        /// <summary>
        /// Cache strategy delegate determines when value must be invalidated without key.
        /// Allows to use with delegates with no arguments.
        /// </summary>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="cache">Cache storage used.</param>
        /// <param name="notInCache"><c>True</c> if key was not in cache.</param>
        /// <returns><c>True</c> if value must be evaluated and cached again.</returns>
        public delegate bool CacheStrategy<TResult>(IDictionary<int, TResult> cache, bool notInCache);

        #region MaxAgeCacheStrategy

        /// <summary>
        /// Cache strategy based on age validation. If item exceeds specific time of life it should be
        /// invalidated.
        /// </summary>
        /// <typeparam name="TKey">Cache key type.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxAge">Maximum age to live.</param>
        /// <param name="timestampsStorage">Storage to be used for timestamps. By default Dictionary is used.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<TKey, TResult> CreateMaxAgeCacheStrategy<TKey, TResult>(
            TimeSpan maxAge,
            IDictionary<TKey, DateTime> timestampsStorage = null)
        {
            if (timestampsStorage == null)
            {
                timestampsStorage = new Dictionary<TKey, DateTime>();
            }
            object lockObj = new object();

            return (key, dict, notInCache) =>
            {
                DateTime dt;
                bool cached;
                lock (lockObj)
                {
                    cached = timestampsStorage.TryGetValue(key, out dt);
                    if (!cached)
                    {
                        timestampsStorage[key] = DateTime.Now;
                    }
                }
                var isExpired = cached && (DateTime.Now - dt) >= maxAge;
                Debug.WriteLineIf(isExpired, $"CreateMaxAgeCacheStrategy: Key {key} expired.");
                return isExpired;
            };
        }

        /// <summary>
        /// Cache strategy based on age validation. If item exceed specific time on life it shoule be
        /// invalidated. Overload for delegates with no arguments.
        /// </summary>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxAge">Maximum age to live.</param>
        /// <param name="timestampsStorage"></param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<int, TResult> CreateMaxAgeCacheStrategy<TResult>(
            TimeSpan maxAge, IDictionary<int, DateTime> timestampsStorage = null) => CreateMaxAgeCacheStrategy<int, TResult>(maxAge, timestampsStorage);

        /// <summary>
        /// Cache strategy based on age validation. If item exceed specific time on life it shoule be
        /// invalidated. Overload for delegates with 2 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxAge">Maximum age to live.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<Tuple<T1, T2>, TResult> CreateMaxAgeCacheStrategy<T1, T2, TResult>(TimeSpan maxAge)
            => CreateMaxAgeCacheStrategy<Tuple<T1, T2>, TResult>(maxAge);

        /// <summary>
        /// Cache strategy based on age validation. If item exceed specific time on life it shoule be
        /// invalidated. Overload for delegates with 3 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="T3">Type component for key.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxAge">Maximum age to live.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<Tuple<T1, T2, T3>, TResult> CreateMaxAgeCacheStrategy<T1, T2, T3, TResult>(TimeSpan maxAge)
            => CreateMaxAgeCacheStrategy<Tuple<T1, T2, T3>, TResult>(maxAge);

        #endregion

        #region MaxCountCacheStrategy

        /// <summary>
        /// Cache strategy invalidation based on max count if items cached. If it exceeds maxCount the
        /// removeCount last items will be removed. If purge is <c>true</c> then whole cache will be cleared.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxCount">Max items to cache.</param>
        /// <param name="removeCount">How many items to remove from cache, default is 1.</param>
        /// <param name="purge">Should whole cache be cleared. If <c>true</c> the removeCount parameter is ignored. Once maxCount size exceed
        /// we clear all cached keys and start memoization from scratch. <c>False</c> by default.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<TKey, TResult> CreateMaxCountCacheStrategy<TKey, TResult>(
            int maxCount,
            int removeCount = 1,
            bool purge = false)
        {
            if (maxCount < removeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount),
                    string.Format(Properties.Strings.ArgumentMustBeGreaterThan, nameof(maxCount), nameof(removeCount)));
            }

            var keysStorage = new TKey[maxCount];
            var keysStorageIndex = 0;
            object lockObj = new object();

            return (TKey key, IDictionary<TKey, TResult> dict, bool notInCache) =>
            {
                lock (lockObj)
                {
                    if (notInCache && !purge)
                    {
                        if (keysStorageIndex < keysStorage.Length)
                        {
                            keysStorage[keysStorageIndex++] = key;
                        }
                    }
                    if (dict.Count > maxCount)
                    {
                        if (purge)
                        {
                            Debug.WriteLine("CreateMaxCountCacheStrategy: Purge keys storage.");
                            Array.Clear(keysStorage, 0, keysStorage.Length);
                            keysStorageIndex = 0;
                            dict.Clear();
                        }
                        else
                        {
                            // ReSharper disable once ForCanBeConvertedToForeach
                            for (int i = 0; i < removeCount; i++)
                            {
                                Debug.WriteLine($"CreateMaxCountCacheStrategy: Remove key {keysStorage[i]} with index {i} from keys storage.");
                                dict.Remove(keysStorage[i]);
                            }
                            Array.Copy(keysStorage, removeCount, keysStorage, 0, keysStorage.Length - removeCount);
                            Array.Clear(keysStorage, keysStorage.Length - removeCount, removeCount);
                            keysStorageIndex -= removeCount;
                        }
                    }
                }
                return false;
            };
        }

        /// <summary>
        /// Cache strategy invalidation based on max count if items cached. If it exceeds maxCount the
        /// removeCount last items will be removed. If purge is true then whole cached will be cleared.
        /// Overload for delegates with no arguments.
        /// </summary>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxCount">Max items to cache.</param>
        /// <param name="removeCount">How many items to remove from cache, default is 1.</param>
        /// <param name="purge">Should whole cache be cleared. If <c>true</c> the removeCount parameter is ignored. Once maxCount size exceed
        /// we clear all cached keys and start memoization from scratch. <c>False</c> by default.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<int, TResult> CreateMaxCountCacheStrategy<TResult>(
            int maxCount,
            int removeCount = 1,
            bool purge = false) => CreateMaxCountCacheStrategy<int, TResult>(maxCount, removeCount, purge);

        /// <summary>
        /// Cache strategy invalidation based on max count if items cached. If it exceeds maxCount the
        /// removeCount last items will be removed. If purge is true then whole cached will be cleared.
        /// Overload for delegates with 2 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxCount">Max items to cache.</param>
        /// <param name="removeCount">How many items to remove from cache, default is 1.</param>
        /// <param name="purge">Should whole cache be cleared. If <c>true</c> the removeCount parameter is ignored. Once maxCount size exceed
        /// we clear all cached keys and start memoization from scratch. <c>False</c> by default.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<Tuple<T1, T2>, TResult> CreateMaxCountCacheStrategy<T1, T2, TResult>(
            int maxCount,
            int removeCount = 1,
            bool purge = false) => CreateMaxCountCacheStrategy<Tuple<T1, T2>, TResult>(maxCount, removeCount, purge);

        /// <summary>
        /// Cache strategy invalidation based on max count if items cached. If it exceeds maxCount the
        /// removeCount last items will be removed. If purge is true then whole cached will be cleared.
        /// Overload for delegates with 3 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="T3">Type component for key.</typeparam>
        /// <typeparam name="TResult">Cache function result type.</typeparam>
        /// <param name="maxCount">Max items to cache.</param>
        /// <param name="removeCount">How many items to remove from cache, default is 1.</param>
        /// <param name="purge">Should whole cache be cleared. If <c>true</c> the removeCount parameter is ignored. Once maxCount size exceed
        /// we clear all cached keys and start memoization from scratch. <c>False</c> by default.</param>
        /// <returns>Cache strategy instance delegate.</returns>
        public static CacheStrategy<Tuple<T1, T2, T3>, TResult> CreateMaxCountCacheStrategy<T1, T2, T3, TResult>(
            int maxCount,
            int removeCount = 1,
            bool purge = false) => CreateMaxCountCacheStrategy<Tuple<T1, T2, T3>, TResult>(maxCount, removeCount, purge);

        #endregion

        /// <summary>
        /// Returns a memoized version of a referentially transparent function. The memoized version of the
        /// function keeps a cache of the mapping of arguments to their results. When method is called with the same
        /// arguments the memoized result is used. The method is thread safe.
        /// </summary>
        /// <typeparam name="TKey">First argument type.</typeparam>
        /// <typeparam name="TResult">Function result type.</typeparam>
        /// <param name="func">The function to memoize.</param>
        /// <param name="strategies">Strategies to apply. By default limitless strategy will be used.</param>
        /// <param name="cache">Dictionary to use for caching. If not specified the standard Dictionary will be used.</param>
        /// <returns>Delegate with memoize.</returns>
        public static Func<TKey, TResult> Memoize<TKey, TResult>(
            Func<TKey, TResult> func,
            CacheStrategy<TKey, TResult> strategies = null,
            IDictionary<TKey, TResult> cache = null)
        {
            if (cache == null)
            {
                cache = new Dictionary<TKey, TResult>();
            }
            if (strategies == null)
            {
                strategies = (key, dict, notInCache) => false;
            }
            var cacheLock = new System.Threading.ReaderWriterLockSlim();

            return key =>
            {
                cacheLock.EnterUpgradeableReadLock();

                try
                {
                    return MemoizeInternal(key, cacheLock, func, strategies, cache);
                }
                finally
                {
                    cacheLock.ExitUpgradeableReadLock();
                }
            };
        }

        private static TResult MemoizeInternal<TKey, TResult>(
            TKey key,
            System.Threading.ReaderWriterLockSlim cacheLock,
            Func<TKey, TResult> func,
            CacheStrategy<TKey, TResult> strategies,
            IDictionary<TKey, TResult> cache)
        {
            bool ExecuteStrategiesReturnIfCacheUpdateRequired(bool notInCache)
            {
                foreach (CacheStrategy<TKey, TResult> strategy in strategies.GetInvocationList())
                {
                    // We have to go thru whole list because some strategies may refresh cache.
                    bool cacheUpdateRequired = strategy(key, cache, notInCache: false);
                    if (cacheUpdateRequired)
                    {
                        return true;
                    }
                }
                return false;
            }

            // If result is already in cache then no need to refresh it - just skip.
            bool inCache = cache.TryGetValue(key, out TResult result), needResultUpdate = false, strategiesAlreadyApplied = false;
            Debug.WriteLine($"Memoize: Start memoize with key = {key}, inCache = {inCache}.");

            if (inCache)
            {
                needResultUpdate = ExecuteStrategiesReturnIfCacheUpdateRequired(notInCache: false);
                if (!needResultUpdate)
                {
                    return result;
                }
                strategiesAlreadyApplied = true;
            }

            // Call user func.
            cacheLock.EnterWriteLock();
            try
            {
                Debug.WriteLine($"Memoize: Evaluating result for key {key}.");
                result = func(key);
                cache[key] = result;
                Debug.WriteLine($"Memoize: Evaluated result for key {key}.");
            }
            catch (SkipMemoizeException<TResult> exc)
            {
                strategiesAlreadyApplied = true;
                result = exc.Result;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }

            // If we didn't call strategies yet.
            if (!strategiesAlreadyApplied)
            {
                needResultUpdate = ExecuteStrategiesReturnIfCacheUpdateRequired(notInCache: true);
            }

            return result;
        }

        /// <summary>
        /// Returns a memoized version of a referentially transparent function. The memoized version of the
        /// function keeps a cache of the mapping from arguments to results and, when calls with the same
        /// arguments are repeated often, has higher performance at the expense of higher memory use.
        /// Overload for delegates with no arguments.
        /// </summary>
        /// <typeparam name="TResult">Function result type.</typeparam>
        /// <param name="func">The function to memoize.</param>
        /// <param name="strategies">Strategies to apply. By default limitless strategy will be used.</param>
        /// <param name="cache">Dictionary to use for caching. If not specified the standard Dictionary will be used which
        /// is not thread safe.</param>
        /// <returns>Delegate the able to cache.</returns>
            public static Func<TResult> Memoize<TResult>(
            Func<TResult> func,
            CacheStrategy<int, TResult> strategies = null,
            IDictionary<int, TResult> cache = null)
        {
            var func2 = new Func<int, TResult>(arg => func());
            var memoized = Memoize(func2, strategies, cache);
            return () => memoized(0);
        }

        /// <summary>
        /// Returns a memoized version of a referentially transparent function. The memoized version of the
        /// function keeps a cache of the mapping from arguments to results and, when calls with the same
        /// arguments are repeated often, has higher performance at the expense of higher memory use.
        /// Overload for delegates with 2 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="TResult">Function result type.</typeparam>
        /// <param name="func">The function to memoize.</param>
        /// <param name="strategies">Strategies to apply. By default limitless strategy will be used.</param>
        /// <param name="cache">Dictionary to use for caching. If not specified the standard Dictionary will be used which
        /// is not thread safe.</param>
        /// <returns>Delegate the able to cache.</returns>
        public static Func<T1, T2, TResult> Memoize<T1, T2, TResult>(
            Func<T1, T2, TResult> func,
            CacheStrategy<Tuple<T1, T2>, TResult> strategies = null,
            IDictionary<Tuple<T1, T2>, TResult> cache = null)
        {
            var func2 = new Func<Tuple<T1, T2>, TResult>(arg => func(arg.Item1, arg.Item2));
            var memoized = Memoize(func2, strategies, cache);
            return (arg1, arg2) => memoized(new Tuple<T1, T2>(arg1, arg2));
        }

        /// <summary>
        /// Returns a memoized version of a referentially transparent function. The memoized version of the
        /// function keeps a cache of the mapping from arguments to results and, when calls with the same
        /// arguments are repeated often, has higher performance at the expense of higher memory use.
        /// Overload for delegates with 3 arguments.
        /// </summary>
        /// <typeparam name="T1">Type component for key.</typeparam>
        /// <typeparam name="T2">Type component for key.</typeparam>
        /// <typeparam name="T3">Type component for key.</typeparam>
        /// <typeparam name="TResult">Function result type.</typeparam>
        /// <param name="func">The function to memoize.</param>
        /// <param name="strategies">Strategies to apply. By default limitless strategy will be used.</param>
        /// <param name="cache">Dictionary to use for caching. If not specified the standard Dictionary will be used which
        /// is not thread safe.</param>
        /// <returns>Delegate the able to cache.</returns>
        public static Func<T1, T2, T3, TResult> Memoize<T1, T2, T3, TResult>(
            Func<T1, T2, T3, TResult> func,
            CacheStrategy<Tuple<T1, T2, T3>, TResult> strategies = null,
            IDictionary<Tuple<T1, T2, T3>, TResult> cache = null)
        {
            var func2 = new Func<Tuple<T1, T2, T3>, TResult>(arg => func(arg.Item1, arg.Item2, arg.Item3));
            var memoized = Memoize(func2, strategies, cache);
            return (arg1, arg2, arg3) => memoized(new Tuple<T1, T2, T3>(arg1, arg2, arg3));
        }
    }
}
