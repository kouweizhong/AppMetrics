﻿// Copyright (c) Allan Hardy. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using App.Metrics.Abstractions.Clock;
using App.Metrics.Abstractions.ReservoirSampling;
using App.Metrics.Apdex;
using App.Metrics.Apdex.Interfaces;
using App.Metrics.Core.Interfaces;
using App.Metrics.Data;
using App.Metrics.Internal;

namespace App.Metrics.Core
{
    /// <summary>
    ///     <see href="https://en.wikipedia.org/wiki/Apdex">Apdex</see> Metric allows us to measure an apdex score which is a
    ///     ratio of the number of satisfied and tolerating requests to the total requests made. Each satisfied request counts
    ///     as one request, while each tolerating request counts as half a satisfied request.
    /// </summary>
    /// <seealso>
    ///     <cref>App.Metrics.Facts.Metrics.IApdexMetric</cref>
    /// </seealso>
    public sealed class ApdexMetric : IApdexMetric
    {
        private readonly bool _allowWarmup;
        private readonly IApdexProvider _apdexProvider;
        private readonly IClock _clock;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ApdexMetric" /> class.
        /// </summary>
        /// <param name="reservoir">The reservoir to user for sampling.</param>
        /// <param name="apdexTSeconds">The apdex t seconds value between 0 and 1.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        /// <param name="allowWarmup">
        ///     if set to <c>true</c> allows the service to warmup before starting to calculate the apdex,
        ///     the score will intitially be 1 until enough samples have been recorded.
        /// </param>
        public ApdexMetric(Lazy<IReservoir> reservoir, double apdexTSeconds, IClock clock, bool allowWarmup)
            : this(new ApdexProvider(reservoir, apdexTSeconds), clock, allowWarmup) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ApdexMetric" /> class.
        /// </summary>
        /// <param name="reservoir">The reservoir to user for sampling.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        /// <param name="allowWarmup">
        ///     if set to <c>true</c> allows the service to warmup before starting to calculate the apdex,
        ///     the score will intitially be 1 until enough samples have been recorded.
        /// </param>
        public ApdexMetric(Lazy<IReservoir> reservoir, IClock clock, bool allowWarmup)
            : this(new ApdexProvider(reservoir, Constants.ReservoirSampling.DefaultApdexTSeconds), clock, allowWarmup) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ApdexMetric" /> class.
        /// </summary>
        /// <param name="apdexProvider">The apdexProvider implementation to use for sampling values to generate the apdex score.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        /// <param name="allowWarmup">
        ///     if set to <c>true</c> allows the service to warmup before starting to calculate the apdex,
        ///     the score will intitially be 1 until enough samples have been recorded.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     clock and apdexProvider are required.
        /// </exception>
        public ApdexMetric(IApdexProvider apdexProvider, IClock clock, bool allowWarmup)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            if (apdexProvider == null)
            {
                throw new ArgumentNullException(nameof(apdexProvider));
            }

            _apdexProvider = apdexProvider;
            _clock = clock;
            _allowWarmup = allowWarmup;
        }

        ~ApdexMetric() { Dispose(false); }

        /// <inheritdoc />
        public ApdexValue Value => GetValue();

        /// <inheritdoc />
        public long CurrentTime() { return _clock.Nanoseconds; }

        [AppMetricsExcludeFromCodeCoverage]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [AppMetricsExcludeFromCodeCoverage]
        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free any other managed objects here.
                }
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public long EndRecording() { return _clock.Nanoseconds; }

        /// <inheritdoc />
        public ApdexValue GetValue(bool resetMetric = false)
        {
            var snapshot = _apdexProvider.GetSnapshot(resetMetric);

            var totalSamples = snapshot.SatisfiedSize + snapshot.ToleratingSize + snapshot.FrustratingSize;

            // ReSharper disable ArrangeRedundantParentheses
            var apdex = (snapshot.SatisfiedSize + ((double)snapshot.ToleratingSize / 2)) / totalSamples;

            // ReSharper restore ArrangeRedundantParentheses
            if (resetMetric)
            {
                Reset();
            }

            return new ApdexValue(apdex, snapshot.SatisfiedSize, snapshot.ToleratingSize, snapshot.FrustratingSize, totalSamples, _allowWarmup);
        }

        /// <inheritdoc />
        public ApdexContext NewContext() { return new ApdexContext(this); }

        /// <inheritdoc />
        public void Reset() { _apdexProvider.Reset(); }

        /// <inheritdoc />
        public long StartRecording() { return _clock.Nanoseconds; }

        /// <inheritdoc />
        public void Track(long duration)
        {
            if (duration < 0)
            {
                return;
            }

            _apdexProvider.Update(duration);
        }

        /// <inheritdoc />
        public void Track(Action action)
        {
            var start = _clock.Nanoseconds;
            try
            {
                action();
            }
            finally
            {
                Track(_clock.Nanoseconds - start);
            }
        }

        // <inheritdoc />
        public T Track<T>(Func<T> action)
        {
            var start = _clock.Nanoseconds;
            try
            {
                return action();
            }
            finally
            {
                Track(_clock.Nanoseconds - start);
            }
        }
    }
}