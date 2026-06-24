using System.Globalization;
using TreblePlayer.Models;
using TreblePlayer.DTOs;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TreblePlayer.Data;

public static class QueryExtensions
{
    public static IOrderedQueryable<T> ApplySort<T>(
        this IQueryable<T> query, List<SortSpecification> specs,
        Dictionary<string, Expression<Func<T, object>>> customMappings = null)
        where T : class, ITrackCollection
    {
        IOrderedQueryable<T> orderedQuery = null;

        foreach (var spec in specs)
        {
            if (customMappings != null && customMappings.TryGetValue(spec.Field, out var expression))
            {
                if (orderedQuery == null)
                {
                    orderedQuery = spec.Direction == SortDirection.Ascending ? query.OrderBy(expression) : query.OrderByDescending(expression);
                }
                else
                {
                    orderedQuery = spec.Direction == SortDirection.Ascending ? orderedQuery.ThenBy(expression) : orderedQuery.ThenByDescending(expression);
                }
            }

            else
            {
                if (orderedQuery == null)
                {
                    orderedQuery = spec.Direction == SortDirection.Ascending ?
                        query.OrderBy(x => EF.Property<object>(x, spec.Field)) :
                        query.OrderByDescending(x => EF.Property<object>(x, spec.Field));
                }
                else
                {
                    orderedQuery = spec.Direction == SortDirection.Ascending ?
                        orderedQuery.ThenBy(x => EF.Property<object>(x, spec.Field)) :
                        orderedQuery.ThenByDescending(x => EF.Property<object>(x, spec.Field));
                }
            }
        }
        // fallback case
        // return orderedQuery ?? query.OrderBy(x => x);
        return orderedQuery ?? query.OrderBy(x => 0); // above doesnt work cuz if T is an Album
        // then EF Core wont know how to translate ordering by a whole compelx object in sql and throw a runtime error
    }
}
