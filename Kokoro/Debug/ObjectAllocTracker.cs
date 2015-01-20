﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kokoro.Debug
{
    /// <summary>
    /// Keeps track of object allocations (ONLY WORKS IN DEBUG BUILDS)
    /// </summary>
    public static class ObjectAllocTracker
    {
#if DEBUG
        private static string GetParameterName2<T>(T item) where T : class
        {
            if (item == null)
                return string.Empty;

            return typeof(T).GetProperties()[0].Name;
        }
#endif

        static string log = "";

        /// <summary>
        /// Log a newly created object
        /// </summary>
        /// <param name="obj">The new object</param>
        /// <param name="id">The associated ID, if any</param>
        public static void NewCreated(object obj, int id = -1, string info = "")
        {
#if DEBUG
            log += "[NEW][" + GetParameterName2(obj) + "][ID:" + id + "]" + info + "\n";
#endif
        }

        /// <summary>
        /// Log a destroyed object
        /// </summary>
        /// <param name="obj">The destroyed object</param>
        /// <param name="id">The associated ID, if any</param>
        public static void ObjectDestroyed(object obj, int id = -1, string info = "")
        {
#if DEBUG
            log += "[DESTROY][" + GetParameterName2(obj) + "][ID:" + id + "]" + info + "\n";
#endif
        }

        /// <summary>
        /// Mark the end of a single loop
        /// </summary>
        /// <param name="timeTaken">The time taken for this loop</param>
        public static void MarkGameLoop(long timeTaken)
        {
#if DEBUG
            log += "[LOOP]" + timeTaken;
#endif
        }

    }
}