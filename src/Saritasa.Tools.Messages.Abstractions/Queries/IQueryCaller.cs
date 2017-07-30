﻿// Copyright (c) 2015-2017, Saritasa. All rights reserved.
// Licensed under the BSD license. See LICENSE file in the project root for full license information.

using System;
using System.Linq.Expressions;

namespace Saritasa.Tools.Messages.Abstractions.Queries
{
    /// <summary>
    /// The interface should be used to make actual calls to query methods.
    /// </summary>
    /// <typeparam name="TQuery">Query object type.</typeparam>
    public interface IQueryCaller<TQuery> where TQuery : class
    {
        /// <summary>
        /// Calls delegate from expression.
        /// </summary>
        /// <typeparam name="TResult">Query result.</typeparam>
        /// <param name="expression">Query method call expression.</param>
        /// <returns>Query result.</returns>
        TResult With<TResult>(Expression<Func<TQuery, TResult>> expression);
    }
}
