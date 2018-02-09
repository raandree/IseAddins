﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IseAddons
{
    static class Extensions
    {
        public delegate TOut Action2<TIn, TOut>(TIn element);

        public static IEnumerable<TOut> ForEach<TIn, TOut>(this IEnumerable<TIn> source, Action2<TIn, TOut> action)
        {
            if (source == null) { throw new ArgumentException(); }
            if (action == null) { throw new ArgumentException(); }

            foreach (TIn element in source)
            {
                TOut result = action(element);
                yield return result;
            }
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) { throw new ArgumentException(); }
            if (action == null) { throw new ArgumentException(); }

            foreach (T element in source)
            {
                action(element);
                yield return element;
            }
        }

        
    }
}