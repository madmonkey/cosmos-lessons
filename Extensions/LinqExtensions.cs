using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DCI.SystemEvents.Extensions
{
    static class LinqExtensions
    {
        public static IQueryable<TSource> WhereIf<TSource>(
            this IQueryable<TSource> source,
            bool condition,
            Expression<Func<TSource, bool>> predicate)
        {
            return condition ? source.Where(predicate) : source;
        }
        public static List<T> Append<T>(this List<T> list, params T[] values)
        {
            list.AddRange(values);
            return list;
        }

        public static List<T> AppendIf<T>(this List<T> list, bool condition, params T[] values)
        {
            return condition ? list.Append(values) : list;
        }

    }
}